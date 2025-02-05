using NetSdrClient.Models.Enums;

namespace Tests.Generators
{
    public class IQPacketGenerator
    {
        public static byte[] CreatePacket(int size, byte sequenceNumber)
        {
            var header = CreateHeader(size, MessageType.DataItem0);
            var packet = RandomExtensions.GenerateNRandomBytes(size);
            packet[0] = header[0];
            packet[1] = header[1];
            packet[2] = 0x04;
            packet[3] = 0x82;
            packet[4] = (byte)sequenceNumber;
            packet[5] = 0;

            return packet;
        }


        public static byte[] CreateHeader(int length, MessageType msgType)
        {
            var header = new byte[2];
            header[0] = (byte)length;
            header[1] = (byte)((length >> 8) & ((1 << 13) - 1));
            header[1] |= (byte)((int)msgType << 5);

            return header;
        }
    }
}
