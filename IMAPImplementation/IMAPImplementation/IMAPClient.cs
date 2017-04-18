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
    struct EmailInbox
    {
        public EmailInbox(bool isSelectable, String name, List<EmailInbox> childs)
        {
            this.isSelectable = isSelectable;
            this.name = name;
            this.childs = childs;
        }
        public EmailInbox(bool isSelectable, String name)
        {
            this.isSelectable = isSelectable;
            this.name = name;
            this.childs = null;
        }
        public bool isSelectable;
        public String name;
        public List<EmailInbox> childs;
    }

    class IMAPClient
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

        public List<String>GetInboxList()
        {
            List<String> inboxes = new List<String>(connection.GetInboxList().Split('*'));
            List<EmailInbox> emailInboxes = new List<EmailInbox>();
            foreach(string inbox in inboxes)
            {
                EmailInbox emailInbox = new EmailInbox();
                emailInbox.isSelectable = inbox.Contains(@"\NoSelect") ? false : true;
                String name = inbox.Substring(inbox.IndexOf("\"/\"") + 4);
                emailInbox.name = name;

                emailInboxes.Add(emailInbox);
            }
            return inboxes;
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
            int symbolsCount = m_connectionStream.Read(buffer, 0, STREAM_SIZE);
            StringBuilder response = new StringBuilder(Encoding.UTF7.GetString(buffer));
            while (symbolsCount == STREAM_SIZE)
            {
                symbolsCount = m_connectionStream.Read(buffer, 0, STREAM_SIZE);
                response.Append(Encoding.UTF7.GetString(buffer));
            }
            return response.ToString();
        }

        public String GetResponse(Stream stream)
        {
            if (stream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            int symbolsCount = stream.Read(buffer, 0, STREAM_SIZE);
            StringBuilder response = new StringBuilder(Encoding.UTF7.GetString(buffer));
            while (symbolsCount == STREAM_SIZE)
            {
                symbolsCount = stream.Read(buffer, 0, STREAM_SIZE);
                response.Append(Encoding.UTF7.GetString(buffer));
            }
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

        public String GetInboxList()
        {
            String commandId = SendCommand("LIST \"\" *");
            String response = GetResponse();
            return response.Substring(0, response.IndexOf(commandId));
        }
    }
}
