using NetSdrClient.Interfaces;
using NetSdrClient.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClient.SocketFactory
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
