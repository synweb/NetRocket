using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetRocket.Exceptions;
using Xunit;

namespace NetRocket.Tests
{
    public class CommunicationFromClientTests
    {
        [Fact]
        public async void NotConnectingToNotListeningServerTest()
        {
            await Assert.ThrowsAsync<ServerUnavailableException>(async () =>
            {
                using (var server = Utils.CreateServer())
                {
                    server.RegisterCredentials(new Credentials("client", "key"));
                    const string networkMethodName = "action";
                    server.RegisterMethod(networkMethodName, () => { });
                    // NOT calling  await server.Start();
                    using (var client =
                        new RocketClient(server.Host, server.Port, "client", "key") {ConnectionRepeatInterval = 100, MaxConnectionAttempts = 2})
                    {
                        await client.Connect();
                        await client.CallServerMethod(networkMethodName);
                    }
                }
            });
        }

        [Fact]
        public async void ConnectionCountIncreasedServerTest()
        {
            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                Assert.Equal(server.ConnectedClientsCount, 0);
                await server.Start();
                using (var client =
                    new RocketClient(server.Host, server.Port, "client", "key") {ConnectionRepeatInterval = 100})
                {
                    await client.Connect();
                    Assert.Equal(server.ConnectedClientsCount, 1);

                    using (var client2 =
                        new RocketClient(server.Host, server.Port, "client", "key") {ConnectionRepeatInterval = 100})
                    {
                        await client2.Connect();
                        Assert.Equal(server.ConnectedClientsCount, 2);
                    }
                    await Task.Delay(1000);
                    Assert.Equal(server.ConnectedClientsCount, 1);
                }
                await Task.Delay(1000);
                Assert.Equal(server.ConnectedClientsCount, 0);
            }
        }

        [Fact]
        public async void ParameterizedMethodCallTest()
        {
            var randomInt = new Random((int)DateTime.Now.Ticks).Next();
            int? receivedInt = null;

            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "writeInt";
                server.RegisterMethod<int>(networkMethodName, (x) => { receivedInt = x; });
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, "client", "key"))
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
            Assert.Equal(randomInt, receivedInt);
        }

        [Fact]
        public async void ParameterizedMethodCallWithResultTest()
        {
            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "compare";
                server.RegisterMethod<int, int>(networkMethodName, x => x.CompareTo(0));
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, "client", "key"))
                {
                    await client.Connect();
                    int minusOne = await client.CallServerMethod<int>(networkMethodName, -684251);
                    int one = await client.CallServerMethod<int>(networkMethodName, 32464);
                    int zero = await client.CallServerMethod<int>(networkMethodName, 0);

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
            using (var server = Utils.CreateServer())
            {
                server.RegisterCredentials(new Credentials("client", "key"));
                const string networkMethodName = "doSomething";
                server.RegisterMethod(networkMethodName, () => { received = true; });
                await server.Start();
                using (var client = new RocketClient(server.Host, server.Port, "client", "key"))
                {
                    await client.Connect();
                    await client.CallServerMethod(networkMethodName);

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
