using NetSdrClient.Models;

namespace NetSdrClient.MessageManagers
{
    public class DataMessageManager
    {
        private const int BUFFER_SIZE = 5;
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
