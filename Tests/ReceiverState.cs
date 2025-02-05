using Moq;
using NetSdrClient.CommandBuilder;
using NetSdrClient.Interfaces;
using NetSdrClient.Models.Enums;
using NetSdrClient.Sockets;
using System.Net;
using System.Net.Sockets;

namespace Tests
{
    public class ReceiverState
    {
        private IPAddress deviceIP = IPAddress.Parse("192.168.0.1");
        private Mock<ISocket> _mockTcpSocket;
        private Mock<ISocket> _mockUdpSocket;
        private NetSdrClient.NetSdrClient _client;

        [SetUp]
        public void Setup()
        {
            var mockSocketFactory = new Mock<ISocketFactory>();
            _mockTcpSocket = new Mock<ISocket>();
            _mockUdpSocket = new Mock<ISocket>();

            _mockTcpSocket.Setup(s => s.Connect(It.IsAny<IPEndPoint>()))
                .Callback(() =>
                {
                    _mockTcpSocket.Setup(s => s.Poll(It.IsAny<int>(), It.IsAny<SelectMode>())).Returns(false);
                    _mockTcpSocket.SetupGet(s => s.Connected).Returns(true);
                });

            mockSocketFactory.Setup(f => f.CreateTCPSocket()).Returns(_mockTcpSocket.Object);
            mockSocketFactory.Setup(f => f.CreateUDPSocket()).Returns(_mockUdpSocket.Object);
            _client = new NetSdrClient.NetSdrClient(mockSocketFactory.Object, deviceIP, timeout: TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public async Task SuccessfullDataCaptureStart()
        {
            byte fifoSize = 10;
            var startCommand = NetSDRCommandBuilder.SetReceiverStateMessage(
                isComplexData: false, 
                isStart: true, 
                CaptureMode.Fifo16Bit, 
                fifoSize
            );

            // return message back as a sign of ACK for now
            _mockTcpSocket.Setup(s => s.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(startCommand.Length)
              .Callback((Memory<byte> buffer, CancellationToken _) => {
                  startCommand.CopyTo(buffer);
               });

            // Act
            _client.Connect();
            var result = await _client.SetReceiverState(true, fifoSize);
            Assert.True(result);
        }

        [Test]
        public void DataCaptureStartNak()
        {
            byte fifoSize = 10;
            var nakResponseBytes = new byte[] { 0x2, 0x20 };

            _mockTcpSocket.Setup(s => s.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(nakResponseBytes.Length)
              .Callback((Memory<byte> buffer, CancellationToken _) => {
                  nakResponseBytes.CopyTo(buffer);
              });

            // Act
            _client.Connect();
            Assert.ThrowsAsync<InvalidOperationException>(() => _client.SetReceiverState(true, fifoSize));
        }

        [Test]
        public void DataCaptureStartDeviceNotResponded()
        {
            _client.Connect();
            Assert.ThrowsAsync<TimeoutException>(() => _client.SetReceiverState(true, 10));
        }

        [Test]
        public void DataCaptureForbidNotConnected()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() => _client.SetReceiverState(true, 10));
        }
    }
}