using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClient.Sockets
{
    public class SocketWrapper : ISocket
    {
        private readonly Socket _socket;
        public int Available => _socket.Available;
        public bool Connected => _socket.Connected;

        public SocketWrapper(Socket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        public async Task<int> SendAsync(ArraySegment<byte> buffer)
        {
            return await _socket.SendAsync(buffer, SocketFlags.None);
        }

        public async Task<int> ReceiveAsync(ArraySegment<byte> buffer)
        {
            return await _socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _socket.ReceiveAsync(buffer, cancellationToken);
        }

        public void Connect(EndPoint remoteEP)
        {
            _socket.Connect(remoteEP);
        }
        public void Bind(EndPoint localEndPoint)
        {
            _socket.Bind(localEndPoint);
        }
        public bool Poll(int microSeconds, SelectMode mode)
        {
            return _socket.Poll(microSeconds, mode);
        }

        public void Shutdown(SocketShutdown how)
        {
            _socket.Shutdown(how);
        }

        public void Close()
        {
            _socket.Close();
        }
    }
}
