using NetSdrClient.Models.Enums;

namespace NetSdrClient.CommandBuilder
{
    public class NetSDRCommandBuilder
    {
        /// <summary>
        /// Prepares message for Receiver State Control Item 0x0018
        /// </summary>
        public static byte[] SetReceiverStateMessage(bool isComplexData, bool isStart, CaptureMode captureMode, byte fifoSize = 0)
        {
            var itemCode = ControlItemCode.ReceiverState;
            // header (2 byte) + item code (2 byte) + 4 x byte parameters
            var messageLength = 2 + 2 + 4;

            var header = PrepareHeader(messageLength, (int)MessageType.SetControlItem);
            byte[] buffer = new byte[messageLength];

            using MemoryStream ms = new(buffer);
            using BinaryWriter writer = new(ms);

            writer.Write((short)header);
            writer.Write((short)itemCode);

            // 1 parameter: Bit 7 == 1 specifies complex base band data 0 == real A / D samples
            var dataTypeParameterValue = isComplexData ? 1 << 7 : 0;
            writer.Write((byte)dataTypeParameterValue);

            // 2 parameter: 0x01 = Idle(Stop), 0x02 = Run
            var runStopControlValue = isStart ? 0x02 : 0x01;
            writer.Write((byte)runStopControlValue);

            // 3 parameter: capture mode has 5 predefined values
            writer.Write((byte)captureMode);

            // 4 parameter: N samples for Fifo16Bit mode
            writer.Write(fifoSize);

            return buffer;
        }

        /// <summary>
        /// Controls the NetSDR NCO center frequency.
        /// </summary>
        public static byte[] SetReceiverFrequencyMessage(NetSDRChannelID channel, long frequency)
        {
            var itemCode = ControlItemCode.ReceiverFrequency;
            // header (2 bytes) + item code (2 bytes) + parameter 1 (1 byte) + parameter 2 (5 bytes)
            var messageLength = 2 + 2 + 1 + 5;

            var header = PrepareHeader(messageLength, (int)MessageType.SetControlItem);
            byte[] buffer = new byte[messageLength];

            using MemoryStream ms = new(buffer);
            using BinaryWriter writer = new(ms);

            writer.Write((short)header);
            writer.Write((short)itemCode);

            // 1 parameter: channel ID
            writer.Write((short)channel);

            // 2 parameter: frequency value - 5 bytes
            var singleByteMask = 0xFF;
            writer.Write((byte)(frequency & singleByteMask));
            writer.Write((byte)(frequency >> 8 & singleByteMask));
            writer.Write((byte)(frequency >> 16 & singleByteMask));
            writer.Write((byte)(frequency >> 24 & singleByteMask));
            writer.Write((byte)(frequency >> 32 & singleByteMask));

            return buffer;
        }

        private static int PrepareHeader(int length, int msgType)
        {
            int header = length | msgType << 13;
            return header;
        }
    }
}
