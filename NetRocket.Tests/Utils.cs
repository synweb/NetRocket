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
        public static RocketServer CreateServer()
        {
            lock (_createServerLock)
            {
                var host = "127.0.0.1";
                var port = GetFreePort();
                return new RocketServer(host, port);
            }
        }

        private static readonly object _createServerLock = new object();

        public static int GetFreePort()
        {
            IPAddress ipAddress = Dns.GetHostEntry("localhost").AddressList[0];
            // тесты многопоточно запрашивают свободный порт
            // из-за этого один порт может быть выдан сразу нескольким
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
                catch (Exception ex)
                {
                    portCounter++;
                }
            }
            throw new NetworkInformationException();
        }


        public static string ByteArrayToString(byte[] byteArray)
        {
            StringBuilder hex = new StringBuilder(byteArray.Length * 2);
            foreach (byte b in byteArray)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");
            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < (hex.Length >> 1); ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }
            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}
