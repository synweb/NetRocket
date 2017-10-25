using System;
using Xunit;

namespace NetRocket.Tests
{
    public class ServerTests
    {
        private readonly string _serverIp = "127.0.0.1";
        private readonly int _serverPort = Utils.GetFreePort();

        [Fact]
        public async void StartListeningTest()
        {
            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                Assert.False(server.IsListening);
                await server.Start();
                Assert.True(server.IsListening);
            }
        }

        [Fact]
        public async void RegisterCredentialsTest()
        {
            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                Assert.Equal(server.CredentialsRegistered, 0);
                server.RegisterCredentials(new Credentials("Pasha", "100500"));
                Assert.Equal(server.CredentialsRegistered, 1);
                server.RegisterCredentials(new Credentials("Pasha", "200500"));
                Assert.Equal(server.CredentialsRegistered, 1);
                server.RegisterCredentials(new Credentials("Petya", "230500"));
                Assert.Equal(server.CredentialsRegistered, 2);
            }
        }

        [Fact]
        public async void UnregisterLoginTest()
        {
            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                Assert.Equal(server.CredentialsRegistered, 0);
                server.RegisterCredentials(new Credentials("Pasha", "100500"));
                Assert.Equal(server.CredentialsRegistered, 1);
                server.UnregisterLogin("Pasha");
                Assert.Equal(server.CredentialsRegistered, 0);
            }
        }

        [Fact]
        public async void UnregisterCredentialsTest()
        {
            using (var server = new RocketServer(_serverIp, _serverPort))
            {
                Assert.Equal(server.CredentialsRegistered, 0);
                var credentials = new Credentials("Pasha", "100500");
                server.RegisterCredentials(credentials);
                Assert.Equal(server.CredentialsRegistered, 1);
                var credentials2 = new Credentials("Pasha2", "111");
                server.UnregisterCredetials(credentials2);
                Assert.Equal(server.CredentialsRegistered, 1);
                var credentials3 = new Credentials("Pasha", "110");
                server.UnregisterCredetials(credentials3);
                Assert.Equal(server.CredentialsRegistered, 0);
            }
        }
    }
}
