using NetSdrClient.Models;

namespace NetSdrClient.Parsers
{
    public class DataItemMessageParser
    {
        public static DataItemMessage Parse(byte[] bytes)
        {
            DataItemMessage result = new();

            using var ms = new MemoryStream(bytes);
            using var binaryReader = new BinaryReader(ms);

            var firstHeaderByte = binaryReader.ReadByte();
            var secondHeaderByte = binaryReader.ReadByte();
            result.Header = GeneralParser.ParseHeader(firstHeaderByte, secondHeaderByte);

            var firstByte = binaryReader.ReadByte();
            if (firstByte != 0x04)
            {
                ThrowInvalidFormat();
            }

            var secondByte = binaryReader.ReadByte();
            // Real 16 Bit FIFO Data assumed for now
            if (secondByte == 0x84)
            {
                result.Data.NumberOfBytes = 1024;
            }
            else if (secondByte == 0x82)
            {
                result.Data.NumberOfBytes = 512;
            }
            else
            {
                ThrowInvalidFormat();
            }

            result.Data.SequenceNumber = binaryReader.ReadInt16();
            result.Data.Bytes = binaryReader.ReadBytes(int.MaxValue);
            if (result.Data.Bytes.Length != result.Data.NumberOfBytes)
            {
                ThrowInvalidFormat();
            }

            return result;
        }

        private static void ThrowInvalidFormat()
        {
            throw new FormatException("The byte array data is in an invalid format.");
        }
    }
}
