using System.Threading.Channels;

namespace NetSdrClient.Extensions
{
    public static class ChannelExtensions
    {
        public static async Task<T> ReadWithTimeoutAsync<T>(this ChannelReader<T> reader, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            var readTask = reader.ReadAsync(cts.Token).AsTask();

            if (await Task.WhenAny(readTask, Task.Delay(timeout, cts.Token)) == readTask)
            {
                return await readTask;
            }

            throw new TimeoutException("Read operation timed out.");
        }
    }
}
