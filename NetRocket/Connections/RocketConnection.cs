using System.Net.Sockets;

namespace NetRocket.Connections
{
    public class RocketConnection
    {
        private ConnectionState _connectionState = ConnectionState.NotConnected;
        public byte[] Buffer { get; internal set; }
        internal Socket Socket { get; set; }
        public int ConnectionId { get; internal set; }

        public ConnectionState ConnectionState
        {
            get { return _connectionState; }
            set
            {
                var oldState = _connectionState;
                _connectionState = value;
                if (oldState != value)
                {
                    OnConnectionStateChanged(ConnectionState);
                }
            }
        }

        public event ConnectionStateChangedHandler ConnectionStateChanged;

        protected virtual void OnConnectionStateChanged(ConnectionState connectionstate)
        {
            ConnectionStateChanged?.Invoke(connectionstate);
        }
    }
}