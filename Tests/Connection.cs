using Moq;
using NetSdrClient.Interfaces;
using NetSdrClient.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    public class Connection
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

            mockSocketFactory.Setup(f => f.CreateTCPSocket()).Returns(_mockTcpSocket.Object);
            mockSocketFactory.Setup(f => f.CreateUDPSocket()).Returns(_mockUdpSocket.Object);
            _client = new NetSdrClient.NetSdrClient(mockSocketFactory.Object, deviceIP);
        }

        [Test]
        public void Connect()
        {
            _client.Connect();

            var endpoint = new IPEndPoint(deviceIP, NetSdrClient.NetSdrClient.TCP_PORT);
            _mockTcpSocket.Verify(s => s.Connect(endpoint), Times.Once);
        }

        [Test]
        public void Disconnect()
        {
            var sequence = new MockSequence();

            _mockTcpSocket.InSequence(sequence).Setup(s => s.Shutdown(It.IsAny<SocketShutdown>()));
            _mockTcpSocket.InSequence(sequence).Setup(s => s.Close());

            _client.Connect();
            _client.Disconnect();

            _mockTcpSocket.Verify(s => s.Shutdown(It.IsAny<SocketShutdown>()), Times.Once);
            _mockTcpSocket.Verify(s => s.Close(), Times.Once);
        }
    }
}
