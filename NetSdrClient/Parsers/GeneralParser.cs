using NetSdrClient.Models;
using NetSdrClient.Models.Enums;

namespace NetSdrClient.Parsers
{
    public static class GeneralParser
    {
        public static Header ParseHeader(byte firstByte, byte secondByte)
        {
            Header header = new();
            var first5bitmask = (1 << 5) - 1;
            var last3bitmask = 0b111 << 5;

            var length = firstByte | (secondByte & first5bitmask) << 8;
            var msgType = (secondByte & last3bitmask) >> 5;

            header.MessageLength = (short)length;
            header.MessageType = EnumExtensions.GetMessageType((byte)msgType);

            return header;
        }
    }
}
