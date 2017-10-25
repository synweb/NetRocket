using System;
using System.Collections.Generic;
using System.Text;

namespace NetRocket.Exceptions
{
    public class ServerUnavailableException: Exception
    {
        public ServerUnavailableException(string host, int port, int connectionAttempts)
        {
            Host = host;
            Port = port;
            ConnectionAttempts = connectionAttempts;
        }

        public ServerUnavailableException()
        {
            
        }

        public string Host { get; set; }
        public int Port { get; set; }
        public int ConnectionAttempts { get; set; }
    }
}
