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
                case ConnectionState.Dropped:
                    if (_autoReconnect)
                    {
                        try
                        {
                            await Reconnect();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
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
        /// Количество попыток подключения. Если подключаться нужно бесконечно, значение должно быть 0. Дефолтное значение - 0.
        /// </summary>
        public int MaxConnectionAttempts { get; set; } = 0;

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
                }
                catch (ServerUnavailableException)
                {
                    throw;
                }
                catch (Exception e)
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

            if (!_connectionStateChangedEventSubscribed)
            {
                Connection.ConnectionStateChanged += OnConnectionStateChanged;
                _connectionStateChangedEventSubscribed = true;
            }
        }

        /// <summary>
        /// Говорит о том, подписано ли событие изменения состояния соединения на соответствующий метод.
        /// Нужно для того, чтобы оно не подписывалось много ряд подряд при переподключении.
        /// </summary>
        private bool _connectionStateChangedEventSubscribed = false;

        private async Task<bool> Authenticate()
        {
            bool authenticated = await SendRequestAndAwaitResult<bool>(Connection, new RoAuthRequestFrame(new Credentials(_login, _key)));
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

        public void SendMessage(string msg)
        {
            EnqueueMessage(Connection, new RoSimpleFrame(msg));
        }

        public void SendMessage(byte[] msg)
        {
            EnqueueMessage(Connection, new RoSimpleFrame(msg));
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
            await SendRequest(Connection, new RoRequestFrame(methodName, param));
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
            return await SendRequestAndAwaitResult<T>(Connection, requestFrame);
        }

        public override async void Dispose()
        {
            Connection.ConnectionStateChanged -= OnConnectionStateChanged;
            await Disconnect();
            base.Dispose();
        }
    }
}
