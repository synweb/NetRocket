using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetRocket.Connections;
using NetRocket.Frames;

namespace NetRocket
{
    public class RocketServer : RocketBase
    {
        public RocketServer(string host, int port) : base(host, port)
        {
        }

        private void OnConnectionStateChanged(RocketConnection rocketConnection, ConnectionState state)
        {
            if (state == ConnectionState.Dropped || state == ConnectionState.NotConnected)
            {
                lock (_currentConnections)
                {
                    _currentConnections.Remove(rocketConnection);
                }
            }
        }

        private readonly List<RocketConnection> _currentConnections = new List<RocketConnection>();
        public int ConnectedClientsCount => _currentConnections.Count;
        private int _maxPendingConnections = 10;
        private int _connectionIdCounter = 0;
        private readonly List<Credentials> _possibleCredentials = new List<Credentials>();
        public int CredentialsRegistered => _possibleCredentials.Count;

        public void RegisterCredentials(Credentials credentials)
        {
            UnregisterCredetials(credentials);
            _possibleCredentials.Add(credentials);
        }

        public void UnregisterCredetials(Credentials credentials)
        {
            _possibleCredentials.RemoveAll(x => x.Login.Equals(credentials.Login, StringComparison.CurrentCultureIgnoreCase));
        }

        public void UnregisterLogin(string login)
        {
            _possibleCredentials.RemoveAll(x => x.Login.Equals(login, StringComparison.CurrentCultureIgnoreCase));
        }

        public async Task Start()
        {
            try
            {
                _socket.Bind(_ipEndPoint);
                _socket.Listen(_maxPendingConnections);
                Debug.WriteLine($"Listening on {Port} started");
            }
            catch (Exception e)
            {
                throw new Exception("StatusCode occured starting listeners, check inner exception", e);
            }

#pragma warning disable 4014
            Task.Factory.StartNew(async () => 
#pragma warning restore 4014
            {
                while (true)
                {
                    var conn = new RocketConnection
                    {
                        ConnectionState = ConnectionState.Connecting,
                        Buffer = new byte[_bufferSize],
                        ConnectionId = _connectionIdCounter,
                    };
                    conn.Socket = await _socket.AcceptAsync();
                    conn.ConnectionStateChanged += (state) => { OnConnectionStateChanged(conn, state); };
                    lock (_currentConnections)
                    {
                        _currentConnections.Add(conn);
                    }
                    Debug.WriteLine(
                        $"Connection #{_connectionIdCounter} unauthorized.\r\nRemote Host:{conn.Socket.RemoteEndPoint}\r\nCurrent connections: {_currentConnections.Count}");
                    _connectionIdCounter++;
                    conn.ConnectionState = ConnectionState.Unauthorized;
                    base.ReceiveData(conn);
                }
            });
        }

        public bool IsListening
        {
            get
            {
                Int32 optVal = (Int32)_socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.AcceptConnection);
                return optVal > 0;
            }
        }

        public async Task SendMessage(byte[] msg, RocketConnection con)
        {
            await SendMessageInternal(con.Socket, new RoSimpleFrame(msg));
        }

        public async Task SendMessage(string msg, RocketConnection con)
        {
            await SendMessageInternal(con.Socket, new RoSimpleFrame(msg));
        }

        //public async Task SendToAll(byte[] bytes)
        //{
        //    await Task.Run(() =>
        //    {
        //        foreach (var rocketConnection in _currentConnections.ToList())
        //        {
        //            SendMessage(bytes, rocketConnection);
        //        }
        //    });
        //}


        protected override void CloseConnection(RocketConnection conn)
        {
            _authorizedConnections.Remove(conn);
            base.CloseConnection(conn);
            Debug.WriteLine($"Connection #{conn.ConnectionId} closed\r\nCurrent connections: {_currentConnections.Count}");
        }

        #region Auth

        private readonly HashSet<RocketConnection> _authorizedConnections = new HashSet<RocketConnection>();

        protected override bool AuthorizeConnection(RocketConnection conn, Credentials credentials)
        {
            var user = _possibleCredentials.FirstOrDefault(x => x.Login.Equals(credentials.Login, StringComparison.CurrentCultureIgnoreCase));
            if (user != null)
            {
                var authorized = credentials.Key.Equals(user.Key);
                if (authorized)
                {
                    Debug.WriteLine($"Connection #{conn.ConnectionId} authorized\r\nCurrent connections: {_currentConnections.Count}");
                    conn.ConnectionState = ConnectionState.Connected;
                    _authorizedConnections.Add(conn);
                    Authorized(user.Login, conn);
                    return true;
                }
                else
                {
                    Debug.WriteLine($"Connection #{conn.ConnectionId} sent wrong auth data.");
                    return false;
                }
            }
            return false;
        }

        protected override bool AuthorizeRequest(byte[] data, RocketConnection conn)
        {
            return _authorizedConnections.Contains(conn);
        }

        public delegate void AuthorizedHandler(string login, RocketConnection rocketConnection);
        public event AuthorizedHandler Authorized = (x, y) => { };

        #endregion

        #region Methods

        public async Task CallClientMethod(string methodName, RocketConnection clientRocketConnection)
        {
            await SendRequest(clientRocketConnection.Socket, new RoRequestFrame(methodName, null));
        }

        public async Task CallClientMethod(string methodName, object param, RocketConnection clientRocketConnection)
        {
            await SendRequest(clientRocketConnection.Socket, new RoRequestFrame(methodName, param));
        }

        public async Task<T> CallClientMethod<T>(string methodName, object param, RocketConnection clientRocketConnection)
        {
            return await SendRequestAndAwaitResult<T>(clientRocketConnection.Socket, new RoRequestFrame(methodName, param));
        }

        #endregion
    }

        
}
