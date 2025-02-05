using System.Net.Sockets;

namespace NetSdrClient.Sockets
{
    public class DefaultSocketFactory : ISocketFactory
    {
        public ISocket CreateTCPSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            return new SocketWrapper(socket);
        }

        public ISocket CreateUDPSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            return new SocketWrapper(socket);
        }
    }
}
