namespace NetSdrClient.Models.Enums
{
    public static class EnumExtensions
    {
        public static MessageType GetMessageType(byte value)
        {
            return value switch
            {
                (byte)MessageType.SetControlItem => MessageType.SetControlItem,
                (byte)MessageType.UnsolicitedControlItem => MessageType.UnsolicitedControlItem,
                (byte)MessageType.DataItem0 => MessageType.DataItem0,
                _ => MessageType.Unknown
            };
        }

        public static ControlItemCode GetItemCode(short value)
        {
            return value switch
            {
                (short)ControlItemCode.ReceiverState => ControlItemCode.ReceiverState,
                (short)ControlItemCode.ReceiverFrequency => ControlItemCode.ReceiverFrequency,
                _ => ControlItemCode.Unknown
            };
        }
    }
}
