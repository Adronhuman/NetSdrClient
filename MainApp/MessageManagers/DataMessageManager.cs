using NetSdrClient.Models;

namespace NetSdrClient.MessageManagers
{
    // Reason for this class:
    // 1) detecting duplicates
    // 2) return message in order with bigger probability
    // Could be a bottleneck in case of rare packets (e.g. FIFO mode with big number of samples in each)
    // In case unncessary can be freely removed
    // OR used with BUFFER_SIZE = 0
    public class DataMessageManager(int BUFFER_SIZE)
    {
        private readonly PriorityQueue<DataItemMessage, short> messageBuffer = new();
        public event EventHandler<DataItemMessage> DataMessageReceived = delegate { };

        public void Feed(DataItemMessage message)
        {
            if (messageBuffer.UnorderedItems.Any(x => x.Priority == message.Data.SequenceNumber))
            {
                return;
            }

            messageBuffer.Enqueue(message, message.Data.SequenceNumber);
            if (messageBuffer.Count > BUFFER_SIZE)
            {
                var oldest = messageBuffer.Dequeue();
                DataMessageReceived.Invoke(this, oldest);
            }
        }
    }
}
