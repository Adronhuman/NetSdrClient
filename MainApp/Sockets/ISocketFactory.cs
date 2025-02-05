using NetSdrClient.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClient.Interfaces
{
    public interface ISocketFactory
    {
        ISocket CreateTCPSocket();
        ISocket CreateUDPSocket();
    }
}
