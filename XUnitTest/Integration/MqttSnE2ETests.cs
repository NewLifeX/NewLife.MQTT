using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.MqttSn;
using Xunit;

namespace XUnitTest.Integration;

public class MqttSnE2ETests
{
    [System.ComponentModel.DisplayName("MQTT-SN: raw UDP CONNECT/CONNACK round-trip")]
    [Fact]
    public async Task RawUdpConnectRoundTrip()
    {
        using var server = new MqttServer { Port = 0, ServiceProvider = new ObjectContainer().BuildServiceProvider(), Log = XTrace.Log, SessionLog = XTrace.Log };
        server.Start();
        using var gateway = new MqttSnGateway { Port = 0, GatewayId = 1, MqttBroker = $"tcp://127.0.0.1:{server.Port}", AdvertiseInterval = 120, Log = XTrace.Log };
        gateway.Start();
        var gwEp = new IPEndPoint(IPAddress.Loopback, gateway.ActualPort);

        using var udp = new UdpClient(0);
        var data = new MqttSnConnectMessage { Flags = 0x02, Duration = 300, ClientId = "raw" }.Encode();
        await udp.SendAsync(data, data.Length, gwEp);
        var cts = new CancellationTokenSource(5000);
        var result = await udp.ReceiveAsync(cts.Token);
        var msg = MqttSnCodec.Decode(result.Buffer, 0, result.Buffer.Length);
        Assert.NotNull(msg);
        Assert.IsType<MqttSnConnAckMessage>(msg);
        Assert.Equal(MqttSnReturnCode.Accepted, ((MqttSnConnAckMessage)msg).ReturnCode);
    }

    [System.ComponentModel.DisplayName("MQTT-SN: MqttSnClient.ConnectAsync E2E through gateway")]
    [Fact]
    public async Task SnClientConnectThroughGateway()
    {
        using var server = new MqttServer { Port = 0, ServiceProvider = new ObjectContainer().BuildServiceProvider(), Log = XTrace.Log, SessionLog = XTrace.Log };
        server.Start();
        using var gateway = new MqttSnGateway { Port = 0, GatewayId = 1, MqttBroker = $"tcp://127.0.0.1:{server.Port}", AdvertiseInterval = 120, Log = XTrace.Log };
        gateway.Start();

        using var client = new MqttSnClient
        {
            ClientId = "sn-e2e-01",
            LocalPort = 0,
            Gateway = new IPEndPoint(IPAddress.Loopback, gateway.ActualPort),
            KeepAliveSeconds = 120,
            CleanSession = true,
            Log = XTrace.Log,
        };

        var connected = await client.ConnectAsync();
        Assert.True(connected, "MqttSnClient should successfully connect to gateway");
    }

    [System.ComponentModel.DisplayName("MQTT-SN: MqttSnClient RegisterTopic + Publish E2E")]
    [Fact]
    public async Task SnClientRegisterAndPublishE2E()
    {
        using var server = new MqttServer { Port = 0, ServiceProvider = new ObjectContainer().BuildServiceProvider(), Log = XTrace.Log, SessionLog = XTrace.Log };
        server.Start();

        using var gateway = new MqttSnGateway { Port = 0, GatewayId = 1, MqttBroker = $"tcp://127.0.0.1:{server.Port}", AdvertiseInterval = 120, Log = XTrace.Log };
        gateway.Start();

        using var client = new MqttSnClient
        {
            ClientId = "sn-pub-e2e",
            LocalPort = 0,
            Gateway = new IPEndPoint(IPAddress.Loopback, gateway.ActualPort),
            KeepAliveSeconds = 120,
            CleanSession = true,
            Log = XTrace.Log,
        };
        var connected = await client.ConnectAsync();
        Assert.True(connected, "Should connect");

        // Register a topic - this uses REGISTER/REGACK through the receive loop
        var topicId = await client.RegisterTopicAsync("/test/sn-e2e-register");
        Assert.True(topicId > 0, $"Should get a valid topic ID, got {topicId}");

        // Publish using the registered topic ID
        await client.PublishAsync(topicId, "hello-e2e"u8.ToArray(), QualityOfService.AtMostOnce);
    }
}
