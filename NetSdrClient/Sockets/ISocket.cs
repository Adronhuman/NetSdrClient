using System.Net;
using System.Net.Sockets;

namespace NetSdrClient.Sockets
{
    public interface ISocket
    {
        int Available { get; }
        bool Connected { get; }
        Task<int> SendAsync(ArraySegment<byte> buffer);
        Task<int> ReceiveAsync(ArraySegment<byte> buffer);
        ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
        void Connect(EndPoint remoteEP);
        void Bind(EndPoint localEndPoint);
        bool Poll(int microSeconds, SelectMode mode);
        void Shutdown(SocketShutdown how);
        void Close();
    }
}
