using Moq;
using NetSdrClient.Interfaces;
using NetSdrClient.Models;
using NetSdrClient.Sockets;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Tests.Generators;

namespace Tests
{
    public class DataStreaming
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

            var simpleAnswer = new byte[10];
            _mockTcpSocket.Setup(s => s.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(simpleAnswer.Length)
              .Callback((Memory<byte> buffer, CancellationToken _) => {
                  simpleAnswer.CopyTo(buffer);
              });

            mockSocketFactory.Setup(f => f.CreateTCPSocket()).Returns(_mockTcpSocket.Object);
            mockSocketFactory.Setup(f => f.CreateUDPSocket()).Returns(_mockUdpSocket.Object);
            _client = new NetSdrClient.NetSdrClient(mockSocketFactory.Object, deviceIP, timeout: TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public async Task DataReceival()
        {
            _client.Connect();

            var sequenceNumber = 73;
            var packets = Enumerable
                .Range(sequenceNumber, 3)
                .Select((i) => IQPacketGenerator.CreatePacket(2000, (byte) i))
                .ToList();

            var packetsSent = 0;
            _mockUdpSocket.Setup(udps => udps.ReceiveAsync(It.IsAny<ArraySegment<byte>>()))
                .Callback((ArraySegment<byte> buffer) => { 
                    if (packetsSent < packets.Count)
                    {
                        var currentPacket = new ArraySegment<byte>(packets[packetsSent]);
                        currentPacket.CopyTo(buffer);
                    }                
                })
                .Returns(async () =>
                {
                    if (packetsSent < packets.Count)
                    {
                        packetsSent++;
                        return packets[packetsSent-1].Length;
                    }
                    await Task.Delay(Timeout.Infinite);
                    return 1;
                });

            var receivedPackets = new List<DataItemMessage>();
            _client.DataMessageArrived += (s, dataMessage) =>
            {
                receivedPackets.Add(dataMessage);
            };


            await _client.SetReceiverState(true, fifoSize: 10);
            
            await Task.Delay(500);
            Assert.That(receivedPackets, Has.Count.EqualTo(3));

            for (int i = 0; i < packets.Count; i++)
            {
                var received = receivedPackets[i];
                Assert.Multiple(() =>
                {
                    Assert.That(received.Data.SequenceNumber, Is.EqualTo(sequenceNumber + i));
                    // first 6 bytes is a header
                    Assert.That(received.Data.Bytes, Is.EqualTo(packets[i].Skip(6)));
                });
            }
        }
    }
}
