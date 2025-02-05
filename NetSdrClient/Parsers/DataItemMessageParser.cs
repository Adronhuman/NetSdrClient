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

            var firstBodyByte = binaryReader.ReadByte();
            var secondBodyByte = binaryReader.ReadByte();
            if (firstBodyByte != 0x04)
            {
                ThrowInvalidFormat();
            }

            result.Data.SequenceNumber = binaryReader.ReadInt16();
            result.Data.Bytes = binaryReader.ReadBytes(result.Header.MessageLength - 6);

            return result;
        }

        private static void ThrowInvalidFormat()
        {
            throw new FormatException("The byte array data is in an invalid format.");
        }
    }
}
