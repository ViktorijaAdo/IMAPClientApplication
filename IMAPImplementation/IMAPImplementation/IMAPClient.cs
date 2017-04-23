using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.Security;

namespace EmailClient
{
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
        IMAPClientConnection connection = null;
        public IMAPClient(String serverDomainName)
        {
            connection = new IMAPClientConnection(serverDomainName);
        }

        public bool Connect(String username, String password)
        {
            if (!connection.TryCreateConnectionWithServer())
                return false;
            if (!connection.AuthenticateUser(username, password))
                return false;

            return true;
        }

        public bool Disconnect()
        {
            return connection.Disconnect();
        }

        public List<EmailInbox> GetInboxList()
        {
            char delimiter;
            List<String> inboxes = new List<String>(connection.GetRootInboxList(out delimiter).Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
            List<EmailInbox> emailInboxes = new List<EmailInbox>();
            foreach(string inbox in inboxes)
            {
                EmailInbox emailInbox = new EmailInbox();
                ParseInboxInfo(inbox, ref emailInbox);
                if (emailInbox.Flags.Contains(@"\Noselect"))
                {
                    List<String> subInboxes = new List<String>(connection.GetInboxList(emailInbox.Name, delimiter).Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
                    foreach (string subInbox in subInboxes)
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
                    String response = GetResponse(stream);
                    if (!response.Contains("OK"))
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
                    String response = GetResponse(stream);
                    if (!response.Contains("OK"))
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
            SendCommand(stream, "CAPABILITY");
            String response = GetResponse(stream);
            if (!response.Contains("STARTTLS") || !response.Contains("OK"))
                return false;

            SendCommand(stream, "STARTTLS");
            if (!GetResponse(stream).Contains("OK"))
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
            m_connectionStream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);
            m_connectionStream.Flush();
            return commandId;
        }

        private String SendCommand(Stream stream, String command)
        {
            String commandId = GetNextCommandId();
            String serverCommand = commandId + " " + command + "\r\n";
            stream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);
            stream.Flush();
            return commandId;
        }

        public String GetResponse()
        {
            if (m_connectionStream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            StringBuilder response = new StringBuilder();
            String responseString;
            do
            {
                int symbolsCount = m_connectionStream.Read(buffer, 0, STREAM_SIZE);
                String bufferString = Encoding.UTF7.GetString(buffer, 0, symbolsCount);
                response.Append(bufferString);
                responseString = response.ToString();
            } while (!responseString.Contains("OK") && !responseString.Contains("NO") && !responseString.Contains("BAD"));
            return responseString;
        }

        public String GetResponse(Stream stream)
        {
            if (stream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            StringBuilder response = new StringBuilder();
            String responseString;
            do
            {
                stream.Read(buffer, 0, STREAM_SIZE);
                response.Append(Encoding.UTF7.GetString(buffer));
                responseString = response.ToString();
            } while (!responseString.Contains("OK") && !responseString.Contains("NO") && !responseString.Contains("BAD"));

            return response.ToString();
        }

        public bool AuthenticateUser(string username, string password)
        {
            if (m_tcpConnection == null || m_connectionStream == null)
                return false;

            SendCommand(m_connectionStream, "CAPABILITY");
            String response = GetResponse();
            if (!response.Contains("AUTH=PLAIN") || !response.Contains("OK"))
                return false;

            SendCommand("LOGIN " + username + " " + password);
            response = GetResponse();
            if (!response.Contains("OK"))
                return false;

            return true;
        }

        public bool Disconnect()
        {
            if (m_tcpConnection == null || m_connectionStream == null)
                return false;

            SendCommand("LOGOUT");
            String response = GetResponse();
            if (!response.Contains("BYE") || !response.Contains("OK"))
                return false;

            m_tcpConnection.Close();
            m_tcpConnection = null;
            m_connectionStream = null;

            return true;
        }

        public String GetRootInboxList(out char hierarchyDelimiter)
        {
            SendCommand("LIST \"\" \"\"");
            String delimiterResponse = GetResponse();
            delimiterResponse = delimiterResponse.Substring(delimiterResponse.IndexOf(')') + 2);
            hierarchyDelimiter = delimiterResponse.ToCharArray()[1];
            String commandId = SendCommand("LIST \"\" %");
            String response = GetResponse();
            return response.Substring(0, response.IndexOf(commandId));
        }

        public String GetInboxList(String rootName, char hierarchyDelimiter)
        {
            String commandId = SendCommand("LIST \"\" \"" + rootName + hierarchyDelimiter + "%\"");
            String response = GetResponse();
            return response.Substring(0, response.IndexOf(commandId));
        }
    }
}
