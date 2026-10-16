using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;
using MqttClient = NewLife.MQTT.MqttClient;

namespace XUnitTest.Integration;

/// <summary>NewLife MqttClient → MQTTnet Server 交叉集成测试。
/// 验证 NewLife 客户端能与第三方 MQTT 服务端正常交互</summary>
[Trait("Category", "CrossCompatibility")]
[Collection("CrossClientTests")]
public class NewLifeClientCrossTests : IClassFixture<CrossIntegrationFixture>
{
    private readonly Int32 _port;
    private readonly CrossIntegrationFixture _fixture;

    public NewLifeClientCrossTests(CrossIntegrationFixture fixture)
    {
        _fixture = fixture;
        _port = fixture.Port;
    }

    private MqttClient CreateClient(String? clientId = null, MqttVersion version = MqttVersion.V311) => new MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"nl_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = false,
        Version = version,
    };

    #region 连接与断开
    [Fact(DisplayName = "NewLife客户端连接MQTTnet服务端成功")]
    public async Task Connect_To_MQTTnet_Server_Success()
    {
        using var client = CreateClient("nl_connect_test");
        await client.ConnectAsync();
        Assert.True(client.IsConnected);
        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "NewLife客户端连接断开事件正常触发")]
    public async Task Connect_Disconnect_Events_Fired()
    {
        using var client = CreateClient("nl_event_test");
        var connected = false;
        var disconnected = false;

        client.Connected += (s, e) => connected = true;
        client.Disconnected += (s, e) => disconnected = true;

        await client.ConnectAsync();
        Assert.True(connected);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.True(disconnected);
    }
    #endregion

    #region 心跳
    [Fact(DisplayName = "NewLife客户端向MQTTnet服务端发送Ping成功")]
    public async Task Ping_MQTTnet_Server_Returns_Response()
    {
        using var client = CreateClient("nl_ping_test");
        await client.ConnectAsync();

        var rs = await client.PingAsync();
        Assert.NotNull(rs);

        await client.DisconnectAsync();
    }
    #endregion

    #region 订阅与发布 QoS 0
    [Fact(DisplayName = "NewLife客户端QoS0发布订阅通过MQTTnet服务端转发")]
    public async Task Publish_QoS0_And_Receive_Via_MQTTnet()
    {
        using var sub = CreateClient("nl_sub_qos0");
        using var pub = CreateClient("nl_pub_qos0");

        await sub.ConnectAsync();
        await pub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/qos0/test");
        await Task.Delay(200);

        var payload = "hello_mqttnet_" + Rand.NextString(4);
        await pub.PublishAsync("cross/qos0/test", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 订阅与发布 QoS 1
    [Fact(DisplayName = "NewLife客户端QoS1发布收到PubAck并订阅端收到消息")]
    public async Task Publish_QoS1_AckReceived_And_Delivered()
    {
        using var sub = CreateClient("nl_sub_qos1");
        using var pub = CreateClient("nl_pub_qos1");

        await sub.ConnectAsync();
        await pub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/qos1/test");
        await Task.Delay(200);

        var payload = "qos1_payload_" + Rand.NextString(4);
        var ack = await pub.PublishAsync("cross/qos1/test", payload, QualityOfService.AtLeastOnce);

        Assert.NotNull(ack);
        Assert.IsType<PubAck>(ack);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 QoS1 消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 订阅与发布 QoS 2
    [Fact(DisplayName = "NewLife客户端QoS2发布完成PUBREC-PUBREL-PUBCOMP流程")]
    public async Task Publish_QoS2_CompletionFlow()
    {
        using var sub = CreateClient("nl_qos2_sub");
        using var pub = CreateClient("nl_qos2_test");

        await sub.ConnectAsync();
        await pub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync(new[] { "cross/qos2/test" }, QualityOfService.ExactlyOnce);
        await Task.Delay(200);

        var rs = await pub.PublishAsync("cross/qos2/test", "qos2_data", QualityOfService.ExactlyOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 QoS2 消息");
        Assert.Equal("qos2_data", tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 通配符订阅
    [Fact(DisplayName = "NewLife客户端通配符+订阅通过MQTTnet服务端路由")]
    public async Task Subscribe_Wildcard_Plus_Via_MQTTnet()
    {
        using var sub = CreateClient("nl_wild_sub");
        using var pub = CreateClient("nl_wild_pub");

        await sub.ConnectAsync();
        await pub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Topic);

        await sub.SubscribeAsync("cross/+/data");
        await Task.Delay(200);

        await pub.PublishAsync("cross/room1/data", "25.5");

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未收到通配符路由消息");
        Assert.Equal("cross/room1/data", tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }

    [Fact(DisplayName = "NewLife客户端通配符#订阅通过MQTTnet服务端路由")]
    public async Task Subscribe_Wildcard_Hash_Via_MQTTnet()
    {
        using var sub = CreateClient("nl_hash_sub");
        using var pub = CreateClient("nl_hash_pub");

        await sub.ConnectAsync();
        await pub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Topic);

        await sub.SubscribeAsync("cross/sensor/#");
        await Task.Delay(200);

        await pub.PublishAsync("cross/sensor/room1/temperature", "22.3");

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未收到 # 通配符路由消息");
        Assert.Equal("cross/sensor/room1/temperature", tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 断线重连
    [Fact(DisplayName = "NewLife客户端断线后自动重连MQTTnet服务端")]
    public async Task Reconnect_After_Server_Disconnect()
    {
        using var client = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = "nl_reconnect_client",
            Timeout = 5000,
            Reconnect = true,
        };

        var reconnected = new TaskCompletionSource<Boolean>();
        var connectCount = 0;
        client.Connected += (s, e) =>
        {
            connectCount++;
            if (connectCount >= 2) reconnected.TrySetResult(true);
        };

        await client.ConnectAsync();
        Assert.Equal(1, connectCount);

        // 服务端强制断开该客户端，触发重连
        var serverClient = (await _fixture.MqttnetServer.GetClientsAsync())
            .FirstOrDefault(c => c.Id == "nl_reconnect_client");
        if (serverClient != null)
            await serverClient.DisconnectAsync(new MqttServerClientDisconnectOptions());

        var winner = await Task.WhenAny(reconnected.Task, Task.Delay(6000));
        Assert.True(winner == reconnected.Task, "客户端未在超时内完成重连");

        client.Dispose();
    }
    #endregion

    #region MQTT 5.0 协议互操作
    [Fact(DisplayName = "NewLife客户端V500连接MQTTnet服务端并完成发布订阅")]
    public async Task MQTT5_Connect_And_PubSub_Via_MQTTnet()
    {
        using var sub = CreateClient("nl_v5_sub", MqttVersion.V500);
        using var pub = CreateClient("nl_v5_pub", MqttVersion.V500);

        await sub.ConnectAsync();
        await pub.ConnectAsync();
        Assert.True(sub.IsConnected);
        Assert.True(pub.IsConnected);

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/v5/test");
        await Task.Delay(200);

        var payload = "v5_nl_" + Rand.NextString(4);
        var ack = await pub.PublishAsync("cross/v5/test", payload, QualityOfService.AtLeastOnce);

        Assert.NotNull(ack);
        Assert.IsType<PubAck>(ack);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 V500 消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region Retain 消息
    [Fact(DisplayName = "NewLife客户端发布Retain消息MQTTnet服务端存储并投递给新订阅者")]
    public async Task Retain_NewLife_Publish_MQTTnet_Server_Delivers()
    {
        using var pub = CreateClient("nl_retain_pub");
        await pub.ConnectAsync();

        var retainMsg = new PublishMessage
        {
            Topic = "cross/retain/test",
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
            Payload = (ArrayPacket)"retain_nl_value".GetBytes(),
        };
        await pub.PublishAsync(retainMsg);
        await Task.Delay(200);

        // 新订阅者连接后应收到 retain 消息
        using var sub = CreateClient("nl_retain_sub");
        await sub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/retain/test");

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "新订阅者未收到 Retain 消息");
        Assert.Equal("retain_nl_value", tcs.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region Will 消息
    [Fact(DisplayName = "NewLife客户端异常断开MQTTnet服务端发布遗嘱消息")]
    public async Task Will_NewLife_AbnormalDisconnect_MQTTnet_Server_Publishes()
    {
        using var sub = CreateClient("nl_will_sub");
        await sub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/will/status");
        await Task.Delay(200);

        var willClient = CreateClient("nl_will_client");
        willClient.WillTopic = "cross/will/status";
        willClient.WillMessage = "offline"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;
        await willClient.ConnectAsync();
        await Task.Delay(200);

        // 模拟异常断开（直接 Dispose 不发 DISCONNECT）
        willClient.Dispose();
        await Task.Delay(500);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.True(winner == tcs.Task, "未在超时内收到遗嘱消息");
        Assert.Equal("offline", tcs.Task.Result);

        await sub.DisconnectAsync();
    }
    #endregion
}
