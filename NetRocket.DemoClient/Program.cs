using System;
using System.Threading.Tasks;

namespace NetRocket.DemoClient
{
    class Program
    {
        private static string _serverIp = "127.0.0.1";
        private static int _serverPort = 18020;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            const int NUMBERS_COUNT = 100;
            const string networkMethodName = "receiveString";
            using (var client = new RocketClient(_serverIp, _serverPort, "client", "key"))
            {
                try
                {
                    await client.Connect();
                    for (int i = 1; i < NUMBERS_COUNT; i++)
                    {
                        await client.CallServerMethod(networkMethodName, i.ToString());
                    }

                    Console.WriteLine($"Sent {NUMBERS_COUNT} numbers");
                    string input = string.Empty;
                    while (!"q".Equals(input))
                    {
                        input = Console.ReadLine();
                        await client.CallServerMethod(networkMethodName, input);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Console.ReadLine();
                    //throw;
                }
            }
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject);
            Console.ReadLine();
        }
    }
}
