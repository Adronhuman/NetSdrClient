namespace NetSdrClient.Models.Enums
{
    public enum CaptureMode : byte
    {
        // 16 bit Contiguous mode where the data is sent contiguously back to the Host
        Contiguous16Bit = 0x00,
        // 24 bit Contiguous mode where the data is sent contiguously back to the Host
        Contiguous24Bit = 0x80,
        // 16 bit FIFO mode where data is captured in a FIFO and then sent to the Host
        Fifo16Bit = 0x01,
        // 24 bit Hardware Triggered Pulse mode (HW trigger start/stop)
        HardwareTriggered24Bit = 0x83,
        // 16 bit Hardware Triggered Pulse mode (HW trigger start/stop)
        HardwareTriggered16Bit = 0x03
    }
}
