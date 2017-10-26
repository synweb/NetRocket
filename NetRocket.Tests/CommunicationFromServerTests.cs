using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetRocket.Connections;
using NetRocket.Exceptions;
using Xunit;

namespace NetRocket.Tests
{
    public class CommunicationFromServerTests
    {
        [Fact]
        public async void InexistMethodCallTest()
        {
            await Assert.ThrowsAsync<MethodDoesNotExistException>(async () =>
            {
                RocketConnection clientRocketConnectionOnServer = null;
                using (var server = Utils.CreateServer())
                {
                    const string clientLogin = "client";
                    const string clientKey = "key";
                    server.RegisterCredentials(new Credentials(clientLogin, clientKey));
                    const string networkMethodName = "inexistMethod";
                    server.Authorized += (login, connection) => { clientRocketConnectionOnServer = connection; };
                    await server.Start();
                    using (var client = new RocketClient(server.Host, server.Port, clientLogin, clientKey))
                    {
                        await client.Connect();
                        await server.CallClientMethod(networkMethodName, null, clientRocketConnectionOnServer);
                    }
                }
            });
        }

        [Fact]
        public async void WrongAuthTest()
        {
            await Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                RocketConnection clientRocketConnectionOnServer = null;
                using (var server = Utils.CreateServer())
                {
                    const string clientLogin = "client";
                    const string clientKey = "key";
                    server.RegisterCredentials(new Credentials(clientLogin, clientKey));
                    const string networkMethodName = "okMethod";
                    server.RegisterMethod(networkMethodName, () => {});
                    server.Authorized += (login, connection) => { clientRocketConnectionOnServer = connection; };
                    await server.Start();
                    using (var client = new RocketClient(server.Host, server.Port, clientLogin, clientKey + "96513251684561"))
                    {
                        await client.Connect();
                        await server.CallClientMethod(networkMethodName, null, clientRocketConnectionOnServer);
                    }
                }
            });
        }


        [Fact]
        public async void ParameterizedMethodCallTest()
        {
            var randomInt = new Random((int)DateTime.Now.Ticks).Next();
            int? receivedInt = null;
            RocketConnection clientRocketConnectionOnServer = null;

            using (var server = Utils.CreateServer())
            {
                const string clientLogin = "client";
                const string clientKey = "key";
                server.RegisterCredentials(new Credentials(clientLogin, clientKey));
                const string networkMethodName = "writeInt";
                server.Authorized += (login, connection) => { clientRocketConnectionOnServer = connection; };
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, clientLogin, clientKey))
                {
                    client.RegisterMethod<int>(networkMethodName, (x) => { receivedInt = x; });
                    await client.Connect();
                    await server.CallClientMethod(networkMethodName, randomInt, clientRocketConnectionOnServer);
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
            Assert.Equal(randomInt, receivedInt);
        }

        [Fact]
        public async void ParameterizedMethodCallWithResultTest()
        {
            int serverPort = Utils.GetFreePort();
            RocketConnection clientRocketConnectionOnServer = null;
            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "compare";
                server.Authorized += (login, connection) => { clientRocketConnectionOnServer = connection; };
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, "client", "key"))
                {
                    client.RegisterMethod<int, int>(networkMethodName, x => x.CompareTo(0));
                    await client.Connect();
                    int minusOne = await server.CallClientMethod<int>(networkMethodName, -684251, clientRocketConnectionOnServer);
                    int one = await server.CallClientMethod<int>(networkMethodName, 32464, clientRocketConnectionOnServer);
                    int zero = await server.CallClientMethod<int>(networkMethodName, 0, clientRocketConnectionOnServer);

                    Assert.Equal(minusOne, -1);
                    Assert.Equal(one, 1);
                    Assert.Equal(zero, 0);
                }
            }
        }

        [Fact]
        public async void UnparameterizedMethodCallTest()
        {
            bool? received = null;
            RocketConnection clientRocketConnectionOnServer = null;
            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "doSomething";
                server.Authorized += (login, connection) => { clientRocketConnectionOnServer = connection; };
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, "client", "key"))
                {
                    client.RegisterMethod(networkMethodName, () => { received = true; });
                    await client.Connect();
                    await server.CallClientMethod(networkMethodName, clientRocketConnectionOnServer);
                }
            }
            int waitCounter = 2000;
            while (received == null)
            {
                int delay = 3;
                await Task.Delay(delay);
                waitCounter -= delay;
                if (waitCounter < 0)
                {
                    throw new TimeoutException();
                }
            }
            Assert.True(received);
        }
    }
}
