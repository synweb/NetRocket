﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetRocket.Connections;
using NetRocket.Cryptography;
using NetRocket.Exceptions;
using NetRocket.Frames;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NetRocket
{
    public abstract class RocketBase: IDisposable
    {
        protected readonly HashAlgorithm _crc32 = new CRC32();

        public delegate void DataReceivedHandler(byte[] data, RocketConnection rocketConnection);

        public event DataReceivedHandler DataReceived = (x, y) => { };

        protected const int HEAD_FRAME_LENGTH = 16;
        protected byte HEAD0 = 255;
        protected byte HEAD1 = 127;
        protected byte HEAD14 = 63;
        protected byte HEAD15 = 31;

        public int SendTimeout { get; set; } = 10000; // 10 sec
        public int ReceiveTimeout { get; set; } = 10000; // 10 sec
        public long MaxMessageLength { get; set; } = 10485760; // 10 mb
        protected const int _bufferSize = 65536;

        protected Socket _socket;
        protected IPEndPoint _ipEndPoint;

        public string Host { get; set; }
        public int Port { get; set; }

        protected RocketBase(string host, int port)
        {
            Host = host;
            Port = port;
            var ipAddr = IPAddress.Parse(Host);
            _ipEndPoint = new IPEndPoint(ipAddr, Port);
            _socket = new Socket(_ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = SendTimeout,
                SendTimeout = ReceiveTimeout,
                NoDelay = true
            };
            Task.Factory.StartNew(QueueProcessingLoop);
        }

        protected void EnqueueMessage(RocketConnection connection, RoFrame frame)
        {
            try
            {
                var json = JsonConvert.SerializeObject(frame);
                var bytes = Encoding.UTF8.GetBytes(json);
                EnqueueMessage(connection, bytes);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private class RocketMessage
        {
            public RocketMessage(RocketConnection connection, byte[] data)
            {
                Connection = connection;
                Data = data;
            }

            public RocketMessage()
            {
                
            }

            public RocketConnection Connection { get; set; }
            public byte[] Data { get; set; }
        }

        private ConcurrentQueue<RocketMessage> _messageQueue = new ConcurrentQueue<RocketMessage>();
        private object _queueLockObject = new object();
        private ManualResetEvent _queueMre;

        private void EnqueueMessage(RocketConnection connection, byte[] msg)
        {
            var checksum = _crc32.ComputeHash(msg);
            var lengthBytes = BitConverter.GetBytes(msg.LongCount());
            using (var ms = new MemoryStream())
            {
                // сначала отправляем длину сообщения и контрольную сумму
                ms.WriteByte(HEAD0);
                ms.WriteByte(HEAD1);
                ms.Write(lengthBytes, 0, lengthBytes.Length);
                ms.Write(checksum, 0, checksum.Length);
                ms.WriteByte(HEAD14);
                ms.WriteByte(HEAD15);
                // затем - само сообщение
                ms.Write(msg, 0, msg.Length);
                _messageQueue.Enqueue(new RocketMessage(connection, ms.ToArray()));
            }
        }

        private void QueueProcessingLoop()
        {
            _queueMre = new ManualResetEvent(false);
            while (!_queueMre.WaitOne(2))
            {
                while (!_messageQueue.IsEmpty)
                {
                    try
                    {
                        RocketMessage msg;
                        lock (_queueLockObject)
                        {
                            bool dequeued = _messageQueue.TryPeek(out msg);
                            if (!dequeued || msg == null)
                                break;
                            if (!msg.Connection.Socket.Connected)
                                break;
                            int bytesSent = msg.Connection.Socket.Send(msg.Data, SocketFlags.Partial);
                            _messageQueue.TryDequeue(out var msg2);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
            }
        }

        //private async Task<int> SendMessageInternal(Socket socket, byte[] msg)
        //{
        //    if (!socket.Connected)
        //        return 0;

        //    var checksum = _crc32.ComputeHash(msg);
        //    var lengthBytes = BitConverter.GetBytes(msg.LongCount());
        //    using (var ms = new MemoryStream())
        //    {
        //        ms.WriteByte(HEAD0);
        //        ms.WriteByte(HEAD1);
        //        ms.Write(lengthBytes, 0, lengthBytes.Length);
        //        ms.Write(checksum, 0, checksum.Length);
        //        ms.WriteByte(HEAD14);
        //        ms.WriteByte(HEAD15);
        //        // сначала отправляем длину сообщения и контрольную сумму
        //        socket.Send(ms.ToArray());
        //    }
        //    // Затем - само сообщение
        //    int bytesSent = await socket.SendAsync(new ArraySegment<byte>(msg), SocketFlags.Partial);
        //    return bytesSent;
        //}

        protected async void ReceiveData(RocketConnection conn)
        {
            var headBufferSegment = new ArraySegment<byte>(conn.Buffer, 0, HEAD_FRAME_LENGTH);
            bool connectionShutdown = false;
            while (!connectionShutdown)
            {
                try
                {
                    int bytesRead = await conn.Socket.ReceiveAsync(headBufferSegment, SocketFlags.None);
                    Debug.WriteLine($"Got {bytesRead} bytes");
                    if (bytesRead == 0)
                    {
                        connectionShutdown = true;
                        CloseConnection(conn);
                        break;
                    }
                    if (bytesRead != HEAD_FRAME_LENGTH)
                    {
                        // длина первого пакета - строго 16 байт
                        //connectionShutdown = true;
                        //CloseConnection(conn);
                        break;
                    }

                    var buffer = conn.Buffer;
                    if (!(buffer[0] == HEAD0
                          && buffer[1] == HEAD1
                          && buffer[14] == HEAD14
                          && buffer[15] == HEAD15))
                    {
                        // неверный формат первого пакета!
                        continue;
                    }
                    Debug.WriteLine($"Head ok");
                    var longSize = 8;
                    var byteBodyLength = new byte[longSize];
                    var checksumLength = 4;
                    var checksum = new byte[checksumLength];
                    Buffer.BlockCopy(buffer, 2, byteBodyLength, 0, longSize);
                    Buffer.BlockCopy(buffer, 10, checksum, 0, checksumLength);
                    long bodyLength = BitConverter.ToInt64(byteBodyLength, 0);
                    Debug.WriteLine($"BodyLength: {bodyLength}");
                    if (bodyLength > MaxMessageLength)
                    {
                        // размер пакет больше предельного
                        long byteCounter = 0;
                        while (byteCounter < bodyLength && conn.Socket.Available > 0)
                        {
                            // получаем оставшиеся куски пакета
                            byteCounter += conn.Socket.Receive(buffer, 0, buffer.Length, SocketFlags.Partial);
                        }
                        continue;
                    }
                    using (var ms = new MemoryStream())
                    {
                        int iterationCounter = 1;
                        while (ms.Length < bodyLength)
                        {
                            int bytesLengthToRead = (int) (bodyLength > buffer.Length ? buffer.Length : bodyLength);
                            bytesRead = conn.Socket.Receive(buffer, 0, bytesLengthToRead, SocketFlags.Partial);
                            ms.Write(buffer, 0, bytesRead);
                            iterationCounter++;
                        }
                        var data = ms.ToArray();
                        Debug.WriteLine($"Received. Checking sum...");
                         
                        var dataChecksum = _crc32.ComputeHash(data);
                        var checksumOk = dataChecksum.SequenceEqual(checksum);
                        if (checksumOk)
                        {
#pragma warning disable 4014
                            Task.Factory.StartNew(async () =>
#pragma warning restore 4014
                            {
                                Debug.WriteLine($"Checksum ok. Authorizing...");
                                var authorized = AuthorizeRequest(data, conn);
                                if (authorized)
                                {
                                    Debug.WriteLine($"Auth ok");
                                    var isInboundMethod = await ProcessRequest(data, conn, isAuthorized: true);
                                    var isResponse = await ProcessResponse(data, conn);
                                    if (isInboundMethod)
                                    {
                                        Debug.WriteLine($"It is request");
                                    }
                                    if (isResponse)
                                    {
                                        Debug.WriteLine($"It is response");
                                    }
                                    //if (await isInboundMethod || await isResponse)
                                    if (isInboundMethod || isResponse)
                                        return;

                                    DataReceived(data, conn);
                                }
                                else
                                {
                                    Debug.WriteLine($"It is auth request");
                                    Task.Run(async () => await ProcessRequest(data, conn, isAuthorized: false));
                                }
                            });
                        }
                        else
                        {
                            throw new InvalidFrameChecksumException();
                        }
                    }

                }
                catch (SocketException e)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine($"SocketErrorCode: {e.SocketErrorCode}");
                    Debug.WriteLine($"Source: {e.Source}");
                    if (e.InnerException != null)
                    {
                        Debug.WriteLine($"Inner: {e.InnerException}");
                    }
                    if (e.SocketErrorCode != SocketError.OperationAborted)
                    {
                        connectionShutdown = true;
                        CloseConnection(conn);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        protected abstract bool AuthorizeRequest(byte[] data, RocketConnection conn);
        protected abstract bool AuthorizeConnection(RocketConnection conn, Credentials credentials);

        protected virtual void CloseConnection(RocketConnection conn)
        {
            if (conn.Socket.Connected)
            {
                conn.Socket.Shutdown(SocketShutdown.Both);
            }
            conn.ConnectionState = NetRocket.Connections.ConnectionState.Dropped;
        }

        #region InboundMethods

        private readonly List<InboundMethod> _inboundMethods = new List<InboundMethod>();

        public void RegisterMethod(string networkMethodName, Action method)
        {
            CheckIfMethodNameIsFree(networkMethodName);
            var delegateMap = new Dictionary<Type, object>();
            _inboundMethods.Add(new InboundMethod(networkMethodName, delegateMap, null, null));
            if (!delegateMap.TryGetValue(typeof(void), out var tmp))
            {
                tmp = new List<Action>();
                delegateMap[typeof(void)] = tmp;
            }
            List<Action> list = (List<Action>)tmp;
            list.Add(method);
        }

        /// <summary>
        /// Зарегистрировать сетевой метод, который будет вызывать void-функцию
        /// </summary>
        /// <typeparam name="TParam"></typeparam>
        /// <param name="networkMethodName"></param>
        /// <param name="method"></param>
        public void RegisterMethod<TParam>(string networkMethodName, Action<TParam> method)
        {
            CheckIfMethodNameIsFree(networkMethodName);
            var delegateMap = new Dictionary<Type, object>();
            _inboundMethods.Add(new InboundMethod(networkMethodName, delegateMap, typeof(TParam), null));
            object tmp;
            if (!delegateMap.TryGetValue(typeof(TParam), out tmp))
            {
                tmp = new List<Action<TParam>>();
                delegateMap[typeof(TParam)] = tmp;
            }
            List<Action<TParam>> list = (List<Action<TParam>>)tmp;
            list.Add(method);
        }

        /// <summary>
        /// Зарегистрировать сетевой метод, который будет отдавать результат
        /// </summary>
        /// <typeparam name="TParam"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="networkMethodName"></param>
        /// <param name="method"></param>
        public void RegisterMethod<TParam, TResult>(string networkMethodName, Func<TParam, TResult> method)
        {
            CheckIfMethodNameIsFree(networkMethodName);
            var delegateMap = new Dictionary<Type, object>();
            _inboundMethods.Add(new InboundMethod(networkMethodName, delegateMap, typeof(TParam), typeof(TResult)));
            if (!delegateMap.TryGetValue(typeof(TParam), out var tmp))
            {
                tmp = new List<Func<TParam, TResult>>();
                delegateMap[typeof(TParam)] = tmp;
            }
            List<Func<TParam, TResult>> list = (List<Func<TParam, TResult>>)tmp;
            list.Add(method);
        }

        private void CheckIfMethodNameIsFree(string networkMethodName)
        {
            if (_inboundMethods.Any(x => x.NetworkMethodName.Equals(networkMethodName)))
            {
                throw new InvalidOperationException("Already contains");
            }
        }

        protected void InvokeNetworkProcedureWithoutParams(string networkMethodName)
        {
            var method = _inboundMethods.SingleOrDefault(x => x.NetworkMethodName.Equals(networkMethodName));
            if (method == null)
            {
                throw new KeyNotFoundException();
            }
            object tmp;
            if (method.DelegateMap.TryGetValue(typeof(void), out tmp))
            {
                List<Action> list = (List<Action>)tmp;
                foreach (var action in list)
                {
                    action();
                }
            }
        }

        protected void InvokeNetworkProcedure<T>(string networkMethodName, T param)
        {
            var method = _inboundMethods.SingleOrDefault(x => x.NetworkMethodName.Equals(networkMethodName));
            if (method == null)
            {
                throw new KeyNotFoundException();
            }
            object tmp;
            if (method.DelegateMap.TryGetValue(typeof(T), out tmp))
            {
                List<Action<T>> list = (List<Action<T>>) tmp;
                foreach (var action in list)
                {
                    action(param);
                }
            }
        }

        protected T2 InvokeNetworkFunction<T, T2>(string localMethodName, T param)
        {
            var method = _inboundMethods.SingleOrDefault(x => x.NetworkMethodName.Equals(localMethodName));
            if (method == null)
            {
                throw new KeyNotFoundException();
            }
            object tmp;
            if (method.DelegateMap.TryGetValue(typeof(T), out tmp))
            {
                List<Func<T, T2>> list = (List<Func<T, T2>>)tmp;
                var func = list.FirstOrDefault();
                if (func == null)
                {
                    throw new NullReferenceException();
                }
                return func(param);
            }
            throw new NullReferenceException();
        }

        protected void InvokeNetworkMethod(string localMethodName)
        {
            var runtimeMethods = this.GetType().GetRuntimeMethods();
            var method = runtimeMethods.First(x => x.Name.Equals(nameof(InvokeNetworkProcedureWithoutParams)));
            method.Invoke(this, new[] { localMethodName });
        }

        protected void InvokeNetworkMethod(string localMethodName, object param)
        {
            var runtimeMethods = this.GetType().GetRuntimeMethods();
            var method = runtimeMethods.First(x => x.Name.Equals(nameof(InvokeNetworkProcedure)) && x.IsGenericMethod);
            var genericMethod = method.MakeGenericMethod(param?.GetType() ?? typeof(object));
            genericMethod.Invoke(this, new[]{localMethodName, param});
        }

        protected object InvokeNetworkFunction(string localMethodName, object param, Type resultType)
        {
            var runtimeMethods = this.GetType().GetRuntimeMethods();
            var method = runtimeMethods.First(x => x.Name.Equals(nameof(InvokeNetworkFunction)) && x.IsGenericMethod);
            var genericMethod = method.MakeGenericMethod(param?.GetType() ?? typeof(object), resultType ?? typeof(object));
            var res = genericMethod.Invoke(this, new[] { localMethodName, param });
            return res;
        }

        #endregion

        #region Requests And Responses

        private object CastIncomingValue(object value, Type targetType)
        {
            object res = null;
            if (value is JToken)
            {
                res = ((JToken)value).ToObject(targetType);
            }
            else if (targetType.GetTypeInfo().GetInterfaces().Any(x => x == typeof(IConvertible)))
            {
                res = Convert.ChangeType(value, targetType);
            }
            else
            {
                res = value;
            }
            return res;
        }

        private T CastIncomingValue<T>(object value)
        {
            T res;
            if (value is JToken)
            {
                res = ((JToken)value).ToObject<T>();
            }
            else if (typeof(T).GetTypeInfo().GetInterfaces().Any(x => x == typeof(IConvertible)))
            {
                res = (T) Convert.ChangeType(value, typeof(T));
            }
            else
            {
                res = (T) value;
            }
            return res;
        }


        private async Task<bool> ProcessRequest(byte[] data, RocketConnection conn, bool isAuthorized)
        {
            try
            {
                var message = Encoding.UTF8.GetString(data);
                string startsWith = "request:";
                if (!message.StartsWith(startsWith))
                    return false; // текст не подходит - это не запрос
                var stringData = message.Substring(startsWith.Length);
                var request = JsonConvert.DeserializeObject<RoRequestFrame>(stringData);
                if (isAuthorized)
                {
                    var method = _inboundMethods.SingleOrDefault(x => x.NetworkMethodName.Equals(request.MethodName));
                    if (method == null)
                    {
                        // метод не зареген
                        Task.Factory.StartNew(async () =>
                        {
                            SendResponse(conn, new RoResponseFrame(request.Guid, ResponseStatusCode.InexistMethod));
                        });
                        return false;
                    }
                    var paramObject = request.Parameter == null ? null : CastIncomingValue(request.Parameter, method.ParamType);
                    // если мы авторизованы, просто вызываем нормальный метод
                    if (method.ResultType == null)
                    {
                        // простой запрос, которому ответ не нужен
                        if (method.ParamType == null)
                        {
                            // параметра нет
                            InvokeNetworkMethod(method.NetworkMethodName);
                        }
                        else
                        {
                            InvokeNetworkMethod(method.NetworkMethodName, paramObject);
                        }
#pragma warning disable 4014
                        Task.Factory.StartNew(async () =>
                        {
                            SendResponse(conn,
                                new RoResponseFrame(request.Guid, null, ResponseStatusCode.Ok));
                        });
#pragma warning restore 4014
                    }
                    else
                    {
                        // вторая сторона ожидает ответ
                        var responseResult =
                            InvokeNetworkFunction(method.NetworkMethodName, paramObject, method.ResultType);
#pragma warning disable 4014
                        Task.Factory.StartNew(async () =>
                        {
                            SendResponse(conn,
                                new RoResponseFrame(request.Guid, responseResult, ResponseStatusCode.Ok));
                        });
#pragma warning restore 4014
                    }
                }
                else
                {
                    // для неавторизованных хотим исключительно авторизацию
                    // передаваться могут только креденшалы
                    var credentials = ((JObject)request.Parameter).ToObject<Credentials>();
                    // если всё ок, авторизуем соединение
                    bool authorized = AuthorizeConnection(conn, credentials);

                    if (authorized)
                    {
#pragma warning disable 4014
                        Task.Run(async () => SendResponse(conn, new RoResponseFrame(request.Guid, true, ResponseStatusCode.Ok)));
#pragma warning restore 4014
                    }
                    else
                    {
                        SendResponse(conn, new RoResponseFrame(request.Guid, false, ResponseStatusCode.Unauthorized));
                        CloseConnection(conn);
                    }
                }
                return true;
            }
            catch
            {
                // при любой ошибке считаем, что метода нет и мы ничего не обработали
                return false;
            }
        }

        private async Task<bool> ProcessResponse(byte[] data, RocketConnection conn)
        {
            try
            {
                await Task.Run(() =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, @"^response:(.+$)", RegexOptions.Singleline);
                    if (!match.Success)
                    {
                        // текст не подходит под регулярку - это не ответ
                        return false;
                    }
                    var stringData = match.Groups[1].Value;
                    var responseFrame = JsonConvert.DeserializeObject<RoResponseFrame>(stringData);
                    if (!_frameResposesAwaitingDictionary.ContainsKey(responseFrame.RequestGuid))
                        return false;
                    _frameResposesAwaitingDictionary[responseFrame.RequestGuid] = responseFrame;
                    return true;
                });
            }
            catch
            {
                // при любой ошибке считаем, что это не ответ
                return false;
            }
            return false;
        }

        /// <summary>
        /// Словарь с запросами, которые ждут ответа
        /// </summary>
        protected Dictionary<Guid, RoResponseFrame> _frameResposesAwaitingDictionary = new Dictionary<Guid, RoResponseFrame>();

        /// <summary>
        /// Отослать ответ на запрос
        /// </summary>
        /// <param name="connection">Удалённый сокет</param>
        /// <param name="response">Пакет ответа</param>
        /// <returns></returns>
        protected void SendResponse(RocketConnection connection, RoResponseFrame response)
        {
            var json = JsonConvert.SerializeObject(response);
            var bytes = Encoding.UTF8.GetBytes($"response:{json}");
            EnqueueMessage(connection, bytes);
        }

        protected async Task SendRequest(RocketConnection connection, RoRequestFrame request, int timeout = 0)
        {
            SendRequestInternal(connection, request);
            RoResponseFrame response = await AwaitResponse(connection, request, timeout);
        }

        /// <summary>
        /// Отослать запрос и дождаться ответа
        /// </summary>
        /// <typeparam name="T">Тип результата</typeparam>
        /// <param name="socket">Удалённый сокет</param>
        /// <param name="request">Пакет запроса</param>
        /// <param name="timeout">Таймаут. Если передан 0, то будет установлен равным ReceiveTimeout</param>
        /// <returns></returns>
        protected async Task<T> SendRequestAndAwaitResult<T>(RocketConnection connection, RoRequestFrame request, int timeout = 0)
        {
            SendRequestInternal(connection, request);
            RoResponseFrame response = await AwaitResponse(connection, request, timeout);
            // удаляем из словаря гуид запроса и ответ, возвращаем результат
            var objectRes = response.Result;
            var result = CastIncomingValue<T>(objectRes);
            return result;
        }

        private async Task<RoResponseFrame> AwaitResponse(RocketConnection connection, RoRequestFrame request, int timeout)
        {
            // когда в словаре по этому гуиду будет объект, значит, ответ пришёл
            //int maxCounter = timeout > 0 ? timeout : ReceiveTimeout;
            //int counter = 0;
            var requestStartTime = DateTime.Now;

            while (!_frameResposesAwaitingDictionary.ContainsKey(request.Guid) || _frameResposesAwaitingDictionary[request.Guid] == null)
            {
                if (requestStartTime.AddMilliseconds(timeout > 0 ? timeout : ReceiveTimeout) < DateTime.Now)
                {
                    _frameResposesAwaitingDictionary.Remove(request.Guid);
                    //throw new RequestTimeoutException(request.Guid);
                    // кладём запрос обратно в очередь, так как ответа не последовало
                    if (this is RocketServer)
                    {
                        // если мы сервер, и клиент от нас отключился, то долбиться к нему не стремимся
                        break;
                    }
                    else
                    {
                        // если мы клиент, то продолжаем долбиться до сервера
                        //EnqueueMessage(connection, request);
                        SendRequestInternal(connection, request);
                    }
                }
                int delay = 2;
                await Task.Delay(delay);
            }
            var response = _frameResposesAwaitingDictionary[request.Guid];
            _frameResposesAwaitingDictionary.Remove(request.Guid);
            switch (response.StatusCode)
            {
                case ResponseStatusCode.InexistMethod:
                    throw new MethodDoesNotExistException(request.MethodName);
                case ResponseStatusCode.Unauthorized:
                    throw new UnauthorizedException();
            }
            return response;
        }

        private void SendRequestInternal(RocketConnection connection, RoRequestFrame request)
        {
            var json = JsonConvert.SerializeObject(request);
            var bytes = Encoding.UTF8.GetBytes($"request:{json}");
            // кладём в словарь гуид запроса, на который нам нужен ответ
            _frameResposesAwaitingDictionary.Add(request.Guid, null);
            EnqueueMessage(connection, bytes);
        }
        #endregion

        public virtual void Dispose()
        {
            _queueMre?.Set();
            _crc32?.Dispose();
            _socket?.Dispose();
            
        }
    }
}
