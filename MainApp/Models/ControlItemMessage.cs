using NetSdrClient.Models.Enums;

namespace NetSdrClient.Models
{
    public struct ControlItemMessage
    {
        public Header Header;
        public ControlItemCode Code;
        public byte[] Parameters;
    }
}
