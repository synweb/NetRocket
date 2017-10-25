using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetRocket.Tests
{
    public static class Utils
    {
        public static int GetFreePort()
        {
            IPAddress ipAddress = Dns.GetHostEntry("localhost").AddressList[0];
            int portCounter = 9055;
            while (portCounter < 65535)
            {
                try
                {
                    var tcpListener = new TcpListener(ipAddress, portCounter);
                    tcpListener.Start();
                    tcpListener.Stop();
                    return portCounter;
                }
                catch (SocketException ex)
                {
                    portCounter++;
                }
            }
            throw new NetworkInformationException();
        }
    }
}
