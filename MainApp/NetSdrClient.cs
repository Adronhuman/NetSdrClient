using NetSdrClient.CommandBuilder;
using NetSdrClient.Extensions;
using NetSdrClient.MessageManagers;
using NetSdrClient.Models;
using NetSdrClient.Models.Enums;
using NetSdrClient.Parsers;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetSdrClient
{
    public class NetSdrClient(IPAddress receiver)
    {
        private static int TCP_PORT = 50000;
        private static int UDP_PORT = 60000;
        private readonly IPEndPoint _receiverEndpoint = new IPEndPoint(receiver, TCP_PORT);
        private Socket? _tcpSocket;
        private CancellationTokenSource _cts = new();
        private readonly Channel<ControlItemMessage> _responseChannel = Channel.CreateUnbounded<ControlItemMessage>();
        private readonly Channel<ControlItemMessage> _unsolicitedChannel = Channel.CreateUnbounded<ControlItemMessage>();
        private Socket? _udpSocket;
        private DataMessageManager? _messageManager;

        public event EventHandler<DataItemMessage> DataMessageArrived = delegate { };

        #region Interface methods
        public void Connect()
        {
            if (IsConnected()) return;

            if (_tcpSocket != null)
                Disconnect();

            SetupControlSocket(out _tcpSocket);

            _cts = new();
            _ = ConfigureControlPipe(_tcpSocket).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.WriteLine($"Pipe processing failed: {task.Exception}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
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
        /// Sets the receiver state to either start or stop.
        /// </summary>
        /// <param name="start">Pass <c>true</c> to start the receiver, <c>false</c> to stop it.</param>
        /// <param name="isComplexData">Complex or real baseband</param>
        /// <param name="captureMode">Capture mode as in specification (4.2.1) </param>
        /// <returns><c>true</c> if the operation succeeded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the receiver returns NAK</exception>
        /// <exception cref="TimeoutException">Thrown when the receiver does not respond</exception>
        public async Task<bool> SetReceiverState(bool start, bool isComplexData, CaptureMode captureMode)
        {
            ThrowIfNotConnected();

            var commandBytes = NetSDRCommandBuilder.SetReceiverStateMessage(isComplexData, start, captureMode);
            _tcpSocket!.Send(commandBytes);

            ControlItemMessage message = await _responseChannel.Reader.ReadWithTimeoutAsync(TimeSpan.FromSeconds(5));
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
            _tcpSocket!.Send(commandBytes);

            ControlItemMessage message = await _responseChannel.Reader.ReadWithTimeoutAsync(TimeSpan.FromSeconds(5));
            if (message.Header.MessageLength == 2)
            {
                throw new InvalidOperationException("Failed to set receiver state: Device returned NAK.");
            }

            return true;
        }
        #endregion

        #region Control Item Pipe
        private async Task ConfigureControlPipe(Socket socket)
        {
            var pipe = new Pipe();
            Task writing = FillControlPipeAsync(socket, pipe.Writer);
            Task reading = ReadControlPipeAsync(pipe.Reader);
            await Task.WhenAll(writing, reading);
        }

        private async Task FillControlPipeAsync(Socket socket, PipeWriter writer)
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
            _messageManager = new DataMessageManager();
            _messageManager.DataMessageReceived += (sender, msg) =>
            {
                DataMessageArrived.Invoke(this, msg);
            };

            _ = ReceiveMessagesAsync(_udpSocket, _messageManager);
        }

        private async Task ReceiveMessagesAsync(Socket udpSocket, DataMessageManager messageManager)
        {
            while (true)
            {
                // 0x04 | 0x84 | 16bit Sequence Number | 1024/512 Data Bytes - 512/256 16bit data samples
                // 1028 / 514 bytes total
                var buffer = ArrayPool<byte>.Shared.Rent(1028);
                try
                {
                    udpSocket.Receive(buffer);
                    var dataMessage = DataItemMessageParser.Parse(buffer);
                    messageManager.Feed(dataMessage);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        #endregion

        private void SetupControlSocket(out Socket tcpSocket)
        {
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect(_receiverEndpoint);
        }

        private void SetupDataSocket(out Socket udpSocket)
        {
            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
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
                return !(_tcpSocket.Poll(1, SelectMode.SelectRead) && _tcpSocket.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
