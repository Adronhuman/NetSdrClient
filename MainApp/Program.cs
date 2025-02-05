

using System.Net.Sockets;

var receiverAddress = "127.0.0.1";

using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);


void ReadControlItem(byte[] message)
{
    using MemoryStream ms = new();
    using BinaryReader reader = new(ms);

    Int16 header = reader.ReadInt16();
    var (length, msgType) = RetrieveLengthAndTypeFromHeader(header);

    //Int16 itemCode = reader.ReadInt16();
}

// 16 bit header:
// [8 bit Length lsb] [3 bit type] [5 bit Length msb]
(int, int) RetrieveLengthAndTypeFromHeader(Int16 header)
{
    int lengthMask = 0b000__11111__11111111; // (1 << 13) - 1
    // var msgTypeMask = 0b111__00000__00000000; // (0b111 << 13)

    int length = header & lengthMask;
    int msgType = header >> 13;

    return (length, msgType);
}

var x = 5; // 101
Console.WriteLine(Convert.ToString((1 << 7), 2));
Console.WriteLine("0001111111111111");