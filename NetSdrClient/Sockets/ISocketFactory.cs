namespace NetSdrClient.Sockets
{
    public interface ISocketFactory
    {
        ISocket CreateTCPSocket();
        ISocket CreateUDPSocket();
    }
}
