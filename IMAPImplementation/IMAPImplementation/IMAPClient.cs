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

    }


    class IMAPClientConnection
    {
        private const int STREAM_SIZE = 2048;
        TcpClient m_tcpConnection = null;
        String m_serverDomain = null;
        SslStream m_connectionStream = null;
        long tagNumber = 1;

        public IMAPClientConnection(String serverDomainName)
        {
            m_serverDomain = serverDomainName;
        }

        public bool TryCreateConnectionWithServer()
        {
            TcpClient connection1 = new TcpClient();
            connection1.ConnectAsync(m_serverDomain, 143);
            System.Threading.Thread.Sleep(1000);
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
            }
            TcpClient connection2 = new TcpClient(m_serverDomain, 993);

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
            return false;
        }

        private bool TryStartWithTSL(Stream stream)
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

        public int SendCommand(String command)
        {
            if(m_tcpConnection == null || m_connectionStream == null || !m_tcpConnection.Connected)
                return 1;

            String serverCommand = "V" + tagNumber++.ToString() + " " + command + "\r\n";
            m_connectionStream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);
            m_connectionStream.Flush();
            return 0;
        }

        private int SendCommand(Stream stream, String command)
        {
            String serverCommand = "V" + tagNumber++.ToString() + " " + command + "\r\n";
            stream.Write(Encoding.ASCII.GetBytes(serverCommand), 0, serverCommand.Length);
            stream.Flush();
            return 0;
        }

        public String GetResponse()
        {
            if (m_connectionStream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            int symbolsCount = m_connectionStream.Read(buffer, 0, STREAM_SIZE);
            StringBuilder response = new StringBuilder(Encoding.ASCII.GetString(buffer));
            while (symbolsCount == STREAM_SIZE)
            {
                symbolsCount = m_connectionStream.Read(buffer, 0, STREAM_SIZE);
                response.Append(Encoding.ASCII.GetString(buffer));
            }
            return response.ToString();
        }

        public String GetResponse(Stream stream)
        {
            if (stream == null)
                return null;

            byte[] buffer = new byte[STREAM_SIZE];
            int symbolsCount = stream.Read(buffer, 0, STREAM_SIZE);
            StringBuilder response = new StringBuilder(Encoding.ASCII.GetString(buffer));
            while (symbolsCount == STREAM_SIZE)
            {
                symbolsCount = stream.Read(buffer, 0, STREAM_SIZE);
                response.Append(Encoding.ASCII.GetString(buffer));
            }
            return response.ToString();
        }

        internal bool AuthenticateUser(string username, string password)
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
    }
}
