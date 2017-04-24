using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.Security;
using IMAPImplementation;

namespace EmailClient
{
    public struct Email
    {
        public String Title { get; set; }
        public int ID { get; set; }

        public String Sender { get; set; }

        public String Text { get; set; }
    }

    public struct EmailInbox
    {
        public EmailInbox(bool isSelectable, String name, List<EmailInbox> childs)
        {
            IsSelectable = isSelectable;
            Name = name;
            Childs = childs;
            Flags = null;
        }
        public EmailInbox(bool isSelectable, String name)
        {
            IsSelectable = isSelectable;
            Name = name;
            Childs = null;
            Flags = null;
        }
        public bool IsSelectable { get; set; }
        public String Name { get; set; }
        public List<EmailInbox> Childs { get; set; }
        public string Flags { get; internal set; }
    }

    public class IMAPClient
    {
        IMAPClientConnection m_connection = null;
        public IMAPClient(String serverDomainName)
        {
            m_connection = new IMAPClientConnection(serverDomainName);
        }

        public bool Connect(String username, String password)
        {
            if (!m_connection.TryCreateConnectionWithServer())
                return false;
            if (!m_connection.AuthenticateUser(username, password))
                return false;

            return true;
        }

        public bool Disconnect()
        {
            return m_connection.Disconnect();
        }

        public List<EmailInbox> GetInboxList()
        {
            List<EmailInbox> emailInboxes = new List<EmailInbox>();
            char delimiter;
            List<String> inboxes = m_connection.GetRootInboxList(out delimiter);
            if (inboxes == null)
                return emailInboxes;

            foreach(String inbox in inboxes)
            {
                EmailInbox emailInbox = new EmailInbox();
                ParseInboxInfo(inbox, ref emailInbox);
                if (emailInbox.Flags.Contains(@"\Noselect"))
                {
                    List<String> subInboxes = m_connection.GetInboxList(emailInbox.Name, delimiter);
                    if (subInboxes == null)
                        continue;

                    foreach (String subInbox in subInboxes)
                    {
                        EmailInbox emailSubInbox = new EmailInbox();
                        ParseInboxInfo(subInbox, ref emailSubInbox);
                        emailSubInbox.Name = emailSubInbox.Name.Substring(emailSubInbox.Name.IndexOf(delimiter)+1);
                        emailInboxes.Add(emailSubInbox);
                    }
                }
                else
                {
                    emailInboxes.Add(emailInbox);
                }
            }
            return emailInboxes;
        }

        internal void GetUpdateEmailWithText(ref Email selectedEmail)
        {
            selectedEmail.Text = m_connection.GetEmailText(selectedEmail.ID);
        }

        public List<Email> GetEmailList(String selectedInbox)
        {
            List<Email> emails = new List<Email>();
            if (!m_connection.SelectInbox(selectedInbox))
                return emails;

            List<String> returnedEmails = m_connection.GetEmailList();
            if (returnedEmails == null)
                return emails;
            Email newEmail = new Email();
            foreach (String emailinfo in returnedEmails)
            {
                if (emailinfo.StartsWith("* "))
                {
                    emails.Add(newEmail);
                    newEmail = new Email();
                }
                ParseEmailInfo(emailinfo, ref newEmail);
            }
            emails.RemoveAt(0);
            return emails;
        }

        private void ParseEmailInfo(String emailInfo, ref Email email)
        {
            String temporary;
            if (emailInfo.Contains("UID"))
            {
                temporary = emailInfo.Substring(emailInfo.IndexOf("UID")+4);
                email.ID = Int32.Parse(temporary.Substring(0, temporary.IndexOf(' ')));
            }
            if (emailInfo.Contains("Subject:"))
            {
                temporary = emailInfo.Substring(emailInfo.IndexOf("Subject:")+8);
                email.Title = temporary.Substring(0);
            }
            if (emailInfo.Contains("From:"))
            {
                temporary = emailInfo.Substring(emailInfo.IndexOf("From:")+5);
                email.Sender = temporary.Substring(0);
            }
        }

        private void ParseInboxInfo(String inbox, ref EmailInbox emailInbox)
        {
            String inboxInfo = inbox.Substring(inbox.IndexOf('('));
            emailInbox.Flags = inboxInfo.Substring(1, inboxInfo.IndexOf(')') - 1);
            inboxInfo = inboxInfo.Substring(inboxInfo.IndexOf('\"'));
            emailInbox.Name = inboxInfo.Split(' ')[1].Trim('\"');
        }
    }


    class IMAPClientConnection
    {
        private const int STREAM_SIZE = 2048;
        TcpClient m_tcpConnection = null;
        String m_serverDomain = null;
        SslStream m_connectionStream = null;
        long m_tagNumber = 1;

        public IMAPClientConnection(String serverDomainName)
        {
            m_serverDomain = serverDomainName;
        }

        public bool TryCreateConnectionWithServer()
        {
            TcpClient connection1 = new TcpClient();
            connection1.ConnectAsync(m_serverDomain, 143);
            System.Threading.Thread.Sleep(1000);

            TcpClient connection2 = new TcpClient();
            int numberOfTries = 0;
            while (true)
            {
                numberOfTries++;
                if (connection1.Connected)
                {
                    NetworkStream stream = connection1.GetStream();
                    List<String> response = GetResponse(stream, null);
                    if (!response.ElementAt(response.Count-1).Contains("OK"))
                        return false;
                    if (TryStartWithTSL(stream))
                    {
                        m_connectionStream = new SslStream(stream);
                        m_connectionStream.AuthenticateAsClient(m_serverDomain);
                        m_tcpConnection = connection1;
                        return true;
                    }
                    else
                    {
                        connection1.Close();
                    }
                }

                if (numberOfTries == 1)
                    connection2.ConnectAsync(m_serverDomain, 993);

                if (connection2.Connected && !connection1.Connected)
                {
                    connection1.Close();
                    SslStream stream = new SslStream(connection2.GetStream());
                    stream.AuthenticateAsClient(m_serverDomain);
                    List<String> response = GetResponse(stream, null);
                    if (!response.ElementAt(response.Count - 1).Contains("OK"))
                        return false;

                    m_connectionStream = stream;
                    m_tcpConnection = connection2;
                    return true;
                }

                if (numberOfTries == -1)
                    return false;
            }
        }

        private bool TryStartWithTSL(NetworkStream stream)
        {
            String commandId = SendCommand(stream, "CAPABILITY");
            List<String> response = GetResponse(stream, commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK") || !response.ElementAt(response.Count - 1).Contains("STARTTLS"))
                return false;

            commandId = SendCommand(stream, "STARTTLS");
            response = GetResponse(stream, commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return false;

            return true;
        }

        public String GetNextCommandId()
        {
            return "V" + m_tagNumber++;
        }

        public String SendCommand(String command)
        {
            if(m_tcpConnection == null || m_connectionStream == null || !m_tcpConnection.Connected)
                return null;

            String commandId = GetNextCommandId();
            String serverCommand = commandId + " " + command + "\r\n";
            //StreamWriter sw = new StreamWriter(m_connectionStream);
            m_connectionStream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);//Write(serverCommand, 0, serverCommand.Length);
            m_connectionStream.Flush();
            return commandId;
        }

        private String SendCommand(Stream stream, String command)
        {
            String commandId = GetNextCommandId();
            String serverCommand = commandId + " " + command + "\r\n";
            //StreamWriter sw = new StreamWriter(stream);
            stream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);//Write(serverCommand, 0, serverCommand.Length);
            stream.Flush();
            return commandId;
        }

        public List<String> GetResponse(String commandId)
        {
            if (m_connectionStream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            StreamReader sr = new StreamReader(m_connectionStream);
            List<String> response = new List<String>();
            String responseString;
            do
            {
                responseString = sr.ReadLine();
                response.Add(responseString);
            } while (commandId != null && (!responseString.Contains(commandId) || (!responseString.Contains("OK") && !responseString.Contains("NO") && !responseString.Contains("BAD"))));
            return response;
        }

        public List<String> GetResponse(Stream stream, String commandId)
        {
            if (stream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            StreamReader sr = new StreamReader(stream);
            List<String> response = new List<string>();
            String responseString;
            do
            {
                responseString = sr.ReadLine();
                response.Add(responseString);
            } while (commandId != null && (!responseString.Contains(commandId) || (!responseString.Contains("OK") && !responseString.Contains("NO") && !responseString.Contains("BAD"))));

            return response;
        }

        public bool AuthenticateUser(string username, string password)
        {
            if (m_tcpConnection == null || m_connectionStream == null)
                return false;

            String commandId = SendCommand(m_connectionStream, "CAPABILITY");
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return false;

            foreach (String responseLine in response)
            {
                if (responseLine.Contains("AUTH=PLAIN"))
                {
                    commandId = SendCommand("LOGIN " + username + " " + password);
                    response = GetResponse(commandId);
                    if (!response.ElementAt(response.Count - 1).Contains("OK"))
                        return false;

                    return true;
                }
            }
            return false;
        }

        public bool Disconnect()
        {
            if (m_tcpConnection == null || m_connectionStream == null)
                return false;

            String commandId = SendCommand("LOGOUT");
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("BYE") || !response.ElementAt(response.Count - 1).Contains("OK"))
                return false;

            m_tcpConnection.Close();
            m_tcpConnection = null;
            m_connectionStream = null;

            return true;
        }

        public List<String> GetRootInboxList(out char hierarchyDelimiter)
        {
            List<String> response;
            String commandId;
            commandId = SendCommand("LIST \"\" \"\"");
            response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
            {
                hierarchyDelimiter = '\0';
                return null;
            }

            String responseLine = response.ElementAt(0);
            responseLine = responseLine.Substring(responseLine.IndexOf(')') + 2);
            hierarchyDelimiter = responseLine.ToCharArray()[1];

            commandId = SendCommand("LIST \"\" %");

            response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return null;

            response.RemoveAt(response.Count - 1);
            return response;
        }

        public List<String> GetInboxList(String rootName, char hierarchyDelimiter)
        {
            String commandId = SendCommand("LIST \"\" \"" + rootName + hierarchyDelimiter + "%\"");
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return null;

            response.RemoveAt(response.Count - 1);
            return response;
        }

        internal bool SelectInbox(String selectedInbox)
        {
            String commandId = SendCommand("SELECT " + selectedInbox);
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return false;

            return true;
        }

        internal List<String> GetEmailList()
        {
            String commandId = SendCommand("FETCH 1:* (UID BODY.PEEK[HEADER.FIELDS (From Subject)])");
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return null;

            response.RemoveAt(response.Count - 1);

            return response;
        }

        internal String GetEmailText(int ID)
        {
            String commandId = SendCommand("FETCH "+ID+" BODY[text]");
            List<String> response = GetResponse(commandId);
            if (!response.ElementAt(response.Count - 1).Contains("OK"))
                return null;

            response.RemoveAt(response.Count - 1);
            if(response.Count>0)
                response.RemoveAt(0);

            StringBuilder result = new StringBuilder();
            foreach(String responseLine in response)
            {
                result.Append(responseLine);
            }
            return result.ToString();
        }

        [Flags]
        public enum ResponseOptions { MessageId, From, Subject, To, Text }
    }
}