using System.Threading.Channels;

namespace NetSdrClient.Extensions
{
    public static class ChannelExtensions
    {
        public static async Task<T> ReadWithTimeoutAsync<T>(this ChannelReader<T> reader, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                var readTask = reader.ReadAsync(cts.Token).AsTask();

                if (await Task.WhenAny(readTask, Task.Delay(timeout)) == readTask)
                {
                    return await readTask;
                }
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException("Read operation timed out.");
            }

            throw new TimeoutException("Read operation timed out.");
        }
    }
}
