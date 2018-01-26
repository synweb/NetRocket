using System;
using System.Threading.Tasks;

namespace NetRocket.DemoServer
{
    class Program
    {
        private static string _serverIp = "127.0.0.1";
        private static int _serverPort = 18020;

        static async Task Main(string[] args)
        {
            int? receivedInt = null;

            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "receiveString";
                server.RegisterMethod<string>(networkMethodName, (x) =>
                {
                    Console.WriteLine($"Received: {x}");
                });
                await server.Start();
                Console.WriteLine($"Listening on {_serverIp}:{_serverPort}");
                Console.ReadLine();
            }
        }
    }
}
