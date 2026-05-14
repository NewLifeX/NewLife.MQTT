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

    private MqttClient CreateClient(String? clientId = null) => new MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"nl_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = false,
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
        using var client = CreateClient("nl_qos2_test");
        await client.ConnectAsync();

        var rs = await client.PublishAsync("cross/qos2/test", "qos2_data", QualityOfService.ExactlyOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
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
}
