using NetSdrClient.CommandBuilder;
using NetSdrClient.Extensions;
using NetSdrClient.MessageManagers;
using NetSdrClient.Models;
using NetSdrClient.Models.Enums;
using NetSdrClient.Parsers;
using NetSdrClient.Sockets;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetSdrClient
{
    public class NetSdrClient
    {
        public readonly static int TCP_PORT = 50000;
        public readonly static int UDP_PORT = 60000;
        private TimeSpan _timeout;
        private bool _shouldBufferBeforeNofity;
        private ISocketFactory _socketFactory;
        private readonly IPEndPoint _receiverEndpoint;
        private ISocket? _tcpSocket;
        private CancellationTokenSource _cts = new();
        private readonly Channel<ControlItemMessage> _responseChannel = Channel.CreateUnbounded<ControlItemMessage>();
        private readonly Channel<ControlItemMessage> _unsolicitedChannel = Channel.CreateUnbounded<ControlItemMessage>();
        private ISocket? _udpSocket;
        private DataMessageManager? _messageManager;

        public event EventHandler<DataItemMessage> DataMessageArrived = delegate { };

        public NetSdrClient(IPAddress receiver)
        {
            _receiverEndpoint = new IPEndPoint(receiver, TCP_PORT);
            _socketFactory = new DefaultSocketFactory();
        }

        public NetSdrClient(ISocketFactory socketFactory,
            IPAddress receiver,
            TimeSpan timeout = default,
            bool bufferBeforeNotify = false)
        {
            _receiverEndpoint = new IPEndPoint(receiver, TCP_PORT);
            _socketFactory = socketFactory;
            _timeout = (timeout == default(TimeSpan)) ? TimeSpan.FromSeconds(5) : timeout;
            _shouldBufferBeforeNofity = bufferBeforeNotify;
        }

        #region Interface methods
        public void Connect()
        {
            if (IsConnected()) return;

            if (_tcpSocket != null)
                Disconnect();

            SetupControlSocket(out _tcpSocket);

            _cts = new();
            _ = Task.Run(() =>
                ConfigureControlPipe(_tcpSocket).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine($"Pipe processing failed: {task.Exception}");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted)
            );
        }

        public void Disconnect()
        {
            _cts.Cancel();
            if (_tcpSocket == null) return;

            try
            {
                _tcpSocket.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                _tcpSocket.Close();
                _tcpSocket = null;
            }
        }

        /// <summary>
        /// Sets the receiver state to either start or stop. Only 16 bit FIFO mode for now
        /// </summary>
        /// <param name="start">Pass <c>true</c> to start the receiver, <c>false</c> to stop it.</param>
        /// <param name="isComplexData">Complex or real baseband</param>
        /// <param name="captureMode">Capture mode as in specification (4.2.1) </param>
        /// <param name="fifoSize">Specifies the number of 4096 16 bit data samples in the FIFO mode</param>
        /// <returns><c>true</c> if the operation succeeded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the receiver returns NAK</exception>
        /// <exception cref="TimeoutException">Thrown when the receiver does not respond</exception>
        public async Task<bool> SetReceiverState(bool start, byte fifoSize)
        {
            ThrowIfNotConnected();

            // capture mode and complex/real config could be extended later
            var commandBytes = NetSDRCommandBuilder.SetReceiverStateMessage(false, start, CaptureMode.Fifo16Bit, fifoSize);
            await _tcpSocket!.SendAsync(commandBytes);

            ControlItemMessage message = await _responseChannel.Reader.ReadWithTimeoutAsync(_timeout);
            if (message.Header.MessageLength == 2)
            {
                throw new InvalidOperationException("Failed to set receiver state: Device returned NAK.");
            }

            if (start)
            {
                await StartReceivingDataAsync();
            }

            return true;
        }

        /// <summary>
        /// Controls the NetSDR NCO center frequency
        /// </summary>
        /// <param name= "channel">Selects which channel to set</param>
        /// <param name="frequency">Frequency in Hz units. Ex.: 14.01MHz = 14010000</param>
        /// <returns><c>true</c> if the operation succeeded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the receiver returns NAK</exception>
        /// <exception cref="TimeoutException">Thrown when the receiver does not respond</exception>
        public async Task<bool> SetReceiverFrequency(NetSDRChannelID channel, long frequency)
        {
            ThrowIfNotConnected();

            var commandBytes = NetSDRCommandBuilder.SetReceiverFrequencyMessage(channel, frequency);
            await _tcpSocket!.SendAsync(commandBytes);

            ControlItemMessage message = await _responseChannel.Reader.ReadWithTimeoutAsync(_timeout);
            if (message.Header.MessageLength == 2)
            {
                throw new InvalidOperationException("Failed to set receiver state: Device returned NAK.");
            }

            return true;
        }
        #endregion

        #region Control Item Pipe
        private async Task ConfigureControlPipe(ISocket socket)
        {
            var pipe = new Pipe();
            Task writing = FillControlPipeAsync(socket, pipe.Writer);
            Task reading = ReadControlPipeAsync(pipe.Reader);
            await Task.WhenAll(writing, reading);
        }

        private async Task FillControlPipeAsync(ISocket socket, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (!_cts.IsCancellationRequested)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await socket.ReceiveAsync(memory, _cts.Token);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error receiving data");
                    break;
                }

                FlushResult result = await writer.FlushAsync(_cts.Token);
                if (result.IsCompleted)
                {
                    break;
                }
            }
            await writer.CompleteAsync();
        }

        private async Task ReadControlPipeAsync(PipeReader reader)
        {
            while (!_cts.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(_cts.Token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryCollectControlMessage(ref buffer, out var message))
                {
                    ClassifyControlMessage(message);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }

        private void ClassifyControlMessage(ControlItemMessage message)
        {
            // NAK
            if (message.Header.MessageLength == 2)
            {
                _responseChannel.Writer.TryWrite(message);
            }

            // unsolicited
            if (message.Header.MessageType == MessageType.UnsolicitedControlItem)
            {
                _unsolicitedChannel.Writer.TryWrite(message);
            }

            _responseChannel.Writer.TryWrite(message);
        }

        private bool TryCollectControlMessage(ref ReadOnlySequence<byte> buffer, out ControlItemMessage message)
        {
            message = default;
            if (buffer.Length < 2)
            {
                return false;
            }

            Span<byte> firstTwoBytes = stackalloc byte[2];
            buffer.Slice(0, 2).CopyTo(firstTwoBytes);

            var header = GeneralParser.ParseHeader(firstTwoBytes[0], firstTwoBytes[1]);
            if (buffer.Length < header.MessageLength)
            {
                return false;
            }

            message.Header = header;

            var sqReader = new SequenceReader<byte>(buffer.Slice(0, header.MessageLength));
            sqReader.TryReadLittleEndian(out short header_bytes);
            if (sqReader.TryReadLittleEndian(out short itemCode_bytes))
            {

                message.Code = EnumExtensions.GetItemCode(itemCode_bytes);
                message.Parameters = buffer.Slice(4, header.MessageLength).ToArray();
            }

            // messageLength bytes is going to be marked as consumed,
            // others stay in buffer waiting for next iteration
            buffer = buffer.Slice(header.MessageLength);

            return true;
        }
        #endregion

        #region Data Item Receival
        private async Task StartReceivingDataAsync()
        {
            SetupDataSocket(out _udpSocket);
            _messageManager = new DataMessageManager(_shouldBufferBeforeNofity ? 5 : 0);
            _messageManager.DataMessageReceived += (sender, msg) =>
            {
                DataMessageArrived.Invoke(this, msg);
            };

            _ = Task.Run(() => ReceiveMessagesAsync(_udpSocket, _messageManager));
        }

        private async Task ReceiveMessagesAsync(ISocket udpSocket, DataMessageManager messageManager)
        {
            while (!_cts.IsCancellationRequested)
            {
                // 0x04 | 0x84 | 16bit Sequence Number | 1024/512 Data Bytes - 512/256 16bit data samples
                // 1028 / 514 bytes total
                var buffer = ArrayPool<byte>.Shared.Rent(1028);
                try
                {
                    await udpSocket.ReceiveAsync(buffer);
                    var dataMessage = DataItemMessageParser.Parse(buffer);
                    messageManager.Feed(dataMessage);
                }
                catch (Exception ex) { }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        #endregion

        private void SetupControlSocket(out ISocket tcpSocket)
        {
            tcpSocket = _socketFactory.CreateTCPSocket();
            tcpSocket.Connect(_receiverEndpoint);
        }

        private void SetupDataSocket(out ISocket udpSocket)
        {
            udpSocket = _socketFactory.CreateUDPSocket();
            IPEndPoint localEndPoint = new(IPAddress.Any, UDP_PORT);
            udpSocket.Bind(localEndPoint);
        }

        private void ThrowIfNotConnected()
        {
            if (!IsConnected())
                throw new InvalidOperationException("Not connected to the receiver.");
        }

        private bool IsConnected()
        {
            if (_tcpSocket == null) return false;
            if (!_tcpSocket.Connected) return false;

            try
            {
                // Poll() returns true if:
                // - Data is available
                // - Connection is closed/reset/terminated
                if (_tcpSocket.Poll(1, SelectMode.SelectRead) && _tcpSocket.Available == 0)
                {
                    return false;
                }
            }
            catch (SocketException)
            {
                return false;
            }

            return true;
        }
    }
}
