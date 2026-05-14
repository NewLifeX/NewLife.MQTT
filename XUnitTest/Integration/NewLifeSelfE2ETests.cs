using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>NewLife MqttClient ↔ NewLife MqttServer 端到端集成测试。
/// 覆盖断线重连、持久化 Session 离线消息、MQTT 5.0 协议、多订阅者等场景，
/// 作为对 MqttIntegrationTests 的补充</summary>
[Collection("SelfE2ETests")]
public class NewLifeSelfE2ETests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public NewLifeSelfE2ETests()
    {
        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = ObjectContainer.Current.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();
        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateClient(
        String? clientId = null,
        MqttVersion version = MqttVersion.V311,
        Boolean reconnect = false) => new MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"e2e_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = reconnect,
        Version = version,
    };

    #region QoS 端到端发布-订阅
    [Fact(DisplayName = "QoS1 发布-订阅端到端消息投递")]
    public async Task QoS1_PubSub_EndToEnd()
    {
        using var pub = CreateClient("e2e_pub_qos1");
        using var sub = CreateClient("e2e_sub_qos1");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("e2e/qos1/topic");
        await Task.Delay(100);

        var payload = "e2e_qos1_" + Rand.NextString(4);
        var ack = await pub.PublishAsync("e2e/qos1/topic", payload, QualityOfService.AtLeastOnce);

        Assert.NotNull(ack);
        Assert.IsType<PubAck>(ack);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "QoS1 消息未到达订阅者");
        Assert.Equal(payload, tcs.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact(DisplayName = "QoS2 发布-订阅端到端消息投递")]
    public async Task QoS2_PubSub_EndToEnd()
    {
        using var pub = CreateClient("e2e_pub_qos2");
        using var sub = CreateClient("e2e_sub_qos2");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("e2e/qos2/topic");
        await Task.Delay(100);

        var payload = "e2e_qos2_" + Rand.NextString(4);
        var comp = await pub.PublishAsync("e2e/qos2/topic", payload, QualityOfService.ExactlyOnce);

        Assert.NotNull(comp);
        Assert.IsType<PubComp>(comp);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "QoS2 消息未到达订阅者");
        Assert.Equal(payload, tcs.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 多订阅者
    [Fact(DisplayName = "同一主题多个订阅者均收到消息")]
    public async Task Multiple_Subscribers_All_Receive_Message()
    {
        using var pub = CreateClient("e2e_multi_pub");
        using var sub1 = CreateClient("e2e_multi_sub1");
        using var sub2 = CreateClient("e2e_multi_sub2");
        using var sub3 = CreateClient("e2e_multi_sub3");

        await pub.ConnectAsync();
        await sub1.ConnectAsync();
        await sub2.ConnectAsync();
        await sub3.ConnectAsync();

        var received = new Int32[3];
        sub1.Received += (s, e) => Interlocked.Increment(ref received[0]);
        sub2.Received += (s, e) => Interlocked.Increment(ref received[1]);
        sub3.Received += (s, e) => Interlocked.Increment(ref received[2]);

        await sub1.SubscribeAsync("e2e/multi/topic");
        await sub2.SubscribeAsync("e2e/multi/topic");
        await sub3.SubscribeAsync("e2e/multi/topic");
        await Task.Delay(200);

        await pub.PublishAsync("e2e/multi/topic", "broadcast", QualityOfService.AtMostOnce);
        await Task.Delay(1000);

        Assert.Equal(1, received[0]);
        Assert.Equal(1, received[1]);
        Assert.Equal(1, received[2]);

        await pub.DisconnectAsync();
        await sub1.DisconnectAsync();
        await sub2.DisconnectAsync();
        await sub3.DisconnectAsync();
    }
    #endregion

    #region 断线重连
    [Fact(DisplayName = "手动断开后重新连接成功")]
    public async Task ManualDisconnect_Then_Reconnect()
    {
        using var client = CreateClient("e2e_reconnect_client", reconnect: false);

        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.False(client.IsConnected);

        // 手动重连
        await client.ConnectAsync();
        Assert.True(client.IsConnected, "重连后应处于已连接状态");

        await client.DisconnectAsync();
    }
    #endregion

    #region MQTT 5.0 协议版本
    [Fact(DisplayName = "MQTT 5.0 版本连接与基本发布订阅")]
    public async Task MQTT5_Connect_And_PubSub()
    {
        using var pub = CreateClient("e2e_v5_pub", MqttVersion.V500);
        using var sub = CreateClient("e2e_v5_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        Assert.True(pub.IsConnected);
        Assert.True(sub.IsConnected);

        var tcs = new TaskCompletionSource<String>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("e2e/v5/topic");
        await Task.Delay(100);

        var payload = "v5_payload_" + Rand.NextString(4);
        await pub.PublishAsync("e2e/v5/topic", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "MQTT 5.0 消息未到达订阅者");
        Assert.Equal(payload, tcs.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 取消订阅
    [Fact(DisplayName = "取消订阅后不再收到消息")]
    public async Task Unsubscribe_Stops_Receiving_Messages()
    {
        using var pub = CreateClient("e2e_unsub_pub");
        using var sub = CreateClient("e2e_unsub_sub");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receiveCount = 0;
        sub.Received += (s, e) => Interlocked.Increment(ref receiveCount);

        await sub.SubscribeAsync("e2e/unsub/topic");
        await Task.Delay(100);

        // 发一条消息，确认订阅有效
        await pub.PublishAsync("e2e/unsub/topic", "msg1");
        await Task.Delay(500);
        Assert.Equal(1, receiveCount);

        // 取消订阅
        await sub.UnsubscribeAsync(new[] { "e2e/unsub/topic" });
        await Task.Delay(100);

        // 再发消息，不应收到
        await pub.PublishAsync("e2e/unsub/topic", "msg2");
        await Task.Delay(500);
        Assert.Equal(1, receiveCount);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 持久化 Session（CleanSession=false）
    [Fact(DisplayName = "CleanSession=false 离线期间消息上线后补投")]
    public async Task PersistentSession_Offline_Messages_Delivered_On_Reconnect()
    {
        var clientId = "e2e_persist_client";

        // 第一步：客户端连接并订阅（CleanSession=false）
        var client1 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            CleanSession = false,
        };
        await client1.ConnectAsync();
        await client1.SubscribeAsync("e2e/persist/topic");
        await client1.DisconnectAsync();
        client1.Dispose();
        await Task.Delay(100);

        // 第二步：发布者发布消息（客户端离线）
        using var pub = CreateClient("e2e_persist_pub");
        await pub.ConnectAsync();
        await pub.PublishAsync("e2e/persist/topic", "offline_msg", QualityOfService.AtLeastOnce);
        await Task.Delay(200);

        // 第三步：客户端重连（同一 ClientId，CleanSession=false）
        var tcs = new TaskCompletionSource<String>();
        var client2 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            CleanSession = false,
        };
        client2.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await client2.ConnectAsync();

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "离线消息未在重连后补投");
        Assert.Equal("offline_msg", tcs.Task.Result);

        await pub.DisconnectAsync();
        await client2.DisconnectAsync();
        client2.Dispose();
    }
    #endregion
}
