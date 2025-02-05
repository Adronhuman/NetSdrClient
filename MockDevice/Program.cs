using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

int port = 12345;
string address = "127.0.0.1";

var listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
listenerSocket.Bind(IPEndPoint.Parse($"{address}:{port}"));
listenerSocket.Listen(10);

// Client's monopoly assumed
while (true)
{
    try
    {
        var clientSocket = listenerSocket.Accept();

        var buffer = new byte[8*1024];
        clientSocket.Receive(buffer);






    }
    catch (Exception ex)
    {
        Console.WriteLine("Error happened" + ex);
        Trace.TraceError("check how Trace. in console works");
    }
}

// General Control Message Format 
// [16 bit header(lsb msb)][16 bit Control item (lsb msb)][Parameter bytes]
// 16 bit header:
// [8 bit Length lsb] [3 bit type] [5 bit Length msb]

// This is fucking amazing (crazy)