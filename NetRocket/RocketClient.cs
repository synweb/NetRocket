using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetRocket.Connections;
using NetRocket.Exceptions;
using NetRocket.Frames;

namespace NetRocket
{
    public class RocketClient : RocketBase
    {
        public RocketClient(string host, int port, string login, string key, bool autoReconnect = true) : base(host, port)
        {
            _autoReconnect = autoReconnect;
            _login = login;
            _key = key;
            Connection = new RocketConnection()
            {
                Socket = _socket,
                Buffer = new byte[_bufferSize],
                ConnectionId = 0
            };
        }

        private async void OnConnectionStateChanged(ConnectionState connectionState)
        {
            switch (connectionState)
            {
                case ConnectionState.NotConnected:
                    if (_autoReconnect)
                    {
                        await Reconnect();
                    }
                    break;
            }
        }

        private readonly bool _autoReconnect;
        private string _login;
        private string _key;
        public RocketConnection Connection { get; }

        /// <summary>
        /// Время между переподключениями в миллисекундах
        /// </summary>
        public int ConnectionRepeatInterval { get; set; } = 5000;

        /// <summary>
        /// Количество попыток подключения. Если подключаться нужно бесконечно, значение должно быть 0. Дефолтное значение - 5.
        /// </summary>
        public int MaxConnectionAttempts { get; set; } = 5;

        public async Task Connect()
        {
            await AsyncConnectInternal(ConnectionState.Connecting);
        }
        public async Task Reconnect()
        {
            await AsyncConnectInternal(ConnectionState.Reconnecting);
        }

        private async Task AsyncConnectInternal(ConnectionState intermediateState)
        {
            Connection.ConnectionState = intermediateState;
            bool connected = false;
            int attemptsRemaining = MaxConnectionAttempts;
            while (!connected)
            {
                try
                {
                    if (MaxConnectionAttempts != 0)
                    {
                        // если подключаемся в первый раз (не реконнект), то у нас только n попыток
                        attemptsRemaining--;
                        if (intermediateState == ConnectionState.Connecting && attemptsRemaining == 0)
                        {
                            throw new ServerUnavailableException(Host, Port, MaxConnectionAttempts);
                        }
                    }
                    _socket = new Socket(_socket.AddressFamily, _socket.SocketType, _socket.ProtocolType);
                    Connection.Socket = _socket;
                    await _socket.ConnectAsync(_ipEndPoint);
                    connected = _socket.Connected;
                    Connection.ConnectionStateChanged += OnConnectionStateChanged;
                }
                catch (SocketException e)
                {
                    Debug.WriteLine(e);
                    await Task.Delay(ConnectionRepeatInterval);
                }
            }


//#pragma warning disable 4014
            ReceiveData(Connection);
//#pragma warning restore 4014
            bool authenticated = await Authenticate();
            if (!authenticated)
            {
                Debug.WriteLine("Wrong login or key");
                return;
            }
            Connection.ConnectionState = ConnectionState.Connected;


        }

        private async Task<bool> Authenticate()
        {
            bool authenticated = await SendRequestAndAwaitResult<bool>(_socket, new RoAuthRequestFrame(new Credentials(_login, _key)));
            if (!authenticated)
            {
                CloseConnection(Connection);
            }
            return authenticated;
        }

        public async Task Disconnect()
        {
            await Task.Run(() =>
            {
                CloseConnection(Connection);
            });
        }

        public async Task SendMessage(string msg)
        {
            await SendMessageInternal(_socket, new RoSimpleFrame(msg));
        }

        public async Task SendMessage(byte[] msg)
        {
            await SendMessageInternal(_socket, new RoSimpleFrame(msg));
        }

        protected override bool AuthorizeRequest(byte[] data, RocketConnection conn)
        {
            // раз уж соединение открыто, на клиенте по умолчанию принимаем всё
            return true;
        }

        protected override bool AuthorizeConnection(RocketConnection conn, Credentials credentials)
        {
            // раз уж соединение открыто, на клиенте по умолчанию принимаем всё
            return true;
        }

        protected override void CloseConnection(RocketConnection conn)
        {
            base.CloseConnection(conn);
            Debug.WriteLine($"Connection #{conn.ConnectionId} closed");
        }


        /// <summary>
        /// Вызвать метод, который зарегистрирован на сервере, без результата
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public async Task CallServerMethod(string methodName, object param = null)
        {
            await SendRequest(_socket, new RoRequestFrame(methodName, param));
        }

        /// <summary>
        /// Вызвать метод, который зарегистрирован на сервере, и получить результат
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public async Task<T> CallServerMethod<T>(string methodName, object param = null)
        {
            var requestFrame = new RoRequestFrame(methodName, param);
            return await SendRequestAndAwaitResult<T>(_socket, requestFrame);
        }

        public override async void Dispose()
        {
            await Disconnect();
            base.Dispose();
        }
    }
}
