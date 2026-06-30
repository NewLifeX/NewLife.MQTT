using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.MqttSn;
using Xunit;

namespace XUnitTest.Integration;

public class MqttSnE2ETests
{
    [System.ComponentModel.DisplayName("MQTT-SN: raw CONNECT/CONNACK round-trip over UDP")]
    [Fact]
    public async Task MqttSn_RawConnectRoundTrip()
    {
        // Start broker and gateway
        using var server = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        server.Start();

        using var gateway = new MqttSnGateway
        {
            Port = 0,
            GatewayId = 1,
            MqttBroker = $"tcp://127.0.0.1:{server.Port}",
            AdvertiseInterval = 60,
            Log = XTrace.Log,
        };
        gateway.Start();
        var gwPort = gateway.ActualPort;
        Assert.True(gwPort > 0, $"Gateway port={gwPort} should be > 0");

        // Raw UDP: send CONNECT, expect CONNACK
        using var udp = new UdpClient(0);
        var gwEp = new IPEndPoint(IPAddress.Loopback, gwPort);

        var connectMsg = new MqttSnConnectMessage
        {
            Flags = 0x02,
            Duration = 300,
            ClientId = "raw-test",
        };
        var data = connectMsg.Encode();
        await udp.SendAsync(data, data.Length, gwEp);

        var cts = new CancellationTokenSource(5000);
        try
        {
            var result = await udp.ReceiveAsync(cts.Token);
            var msg = MqttSnCodec.Decode(result.Buffer, 0, result.Buffer.Length);
            Assert.NotNull(msg);
            Assert.IsType<MqttSnConnAckMessage>(msg);
            var connAck = (MqttSnConnAckMessage)msg;
            Assert.Equal(MqttSnReturnCode.Accepted, connAck.ReturnCode);
        }
        catch (OperationCanceledException)
        {
            Assert.True(false, "No CONNACK received within 5s");
        }
    }
}
