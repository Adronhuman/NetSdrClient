using NetSdrClient.Models.Enums;

namespace NetSdrClient.Models
{
    public struct Header
    {
        public short MessageLength;
        public MessageType MessageType;
    }
}
