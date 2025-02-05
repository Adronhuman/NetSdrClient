namespace NetSdrClient.Models.Enums
{
    public enum MessageType : byte
    {
        SetControlItem = 0b000,
        UnsolicitedControlItem = 0b001,
        DataItem0 = 0b100,
        Unknown = byte.MaxValue
    }
}
