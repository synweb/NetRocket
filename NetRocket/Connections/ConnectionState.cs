namespace NetRocket.Connections
{
    public enum ConnectionState
    {
        NotConnected,
        Dropped,
        Connecting,
        Reconnecting,
        Unauthorized,
        Connected,
    }
}
