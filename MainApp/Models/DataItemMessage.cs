namespace NetSdrClient.Models
{
    public class DataItemMessage
    {
        public Header Header { get; set; }
        public Data0ItemParameter Data { get; set; } = new Data0ItemParameter();
    }

    public class Data0ItemParameter
    {
        public short SequenceNumber { get; set; }
        public ushort NumberOfBytes { get; set; }
        public byte[] Bytes { get; set; }
    }
}
