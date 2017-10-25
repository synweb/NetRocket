using System;
using System.Threading.Tasks;

namespace NetRocket.Demo
{
    class Program
    {
        private static string _serverIp = "127.0.0.1";
        private static int _serverPort = 18020;

        static async Task Main(string[] args)
        {
            var randomInt = new Random((int) DateTime.Now.Ticks).Next();
            int? receivedInt = null;

            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "writeInt";
                server.RegisterMethod<int>(networkMethodName, (x) => { receivedInt = x; });
                await server.Start();
                using (var client = new RocketClient(_serverIp, _serverPort, "client", "key"))
                {
                    await client.Connect();
                    await client.CallServerMethod(networkMethodName, randomInt);
                }
            }
            int waitCounter = 2000;
            while (receivedInt == null)
            {
                int delay = 3;
                await Task.Delay(delay);
                waitCounter -= delay;
                if (waitCounter < 0)
                {
                    throw new TimeoutException();
                }
            }
            Console.WriteLine($"Sent: {randomInt}, received: {receivedInt}");
            Console.ReadLine();
        }
    }
}
