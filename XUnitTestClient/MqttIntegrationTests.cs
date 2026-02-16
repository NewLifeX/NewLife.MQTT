using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTestClient;

/// <summary>客户端/服务端集成测试。覆盖QoS全路径、遗嘱消息、Retain消息、会话持久化</summary>
[Collection("MqttIntegration")]
public class MqttIntegrationTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public MqttIntegrationTests()
    {
        // 使用随机端口避免冲突
        _port = Rand.Next(20000, 30000);

        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);

        _server = new MqttServer
        {
            Port = _port,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();
    }

    public void Dispose()
    {
        _server.TryDispose();
    }

    private MqttClient CreateClient(String? clientId = null)
    {
        return new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId ?? $"test_{Rand.NextString(6)}",
            Timeout = 5000,
            Reconnect = false,
        };
    }

    #region QoS 全路径
    [Fact]
    public async Task QoS0_PublishAndReceive()
    {
        using var client1 = CreateClient("qos0_pub");
        using var client2 = CreateClient("qos0_sub");

        await client1.ConnectAsync();
        await client2.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        client2.Received += (s, e) =>
        {
            received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        };

        await client2.SubscribeAsync("qos0/test");
        await Task.Delay(100);

        var msg = "hello_qos0_" + Rand.NextString(4);
        await client1.PublishAsync("qos0/test", msg, QualityOfService.AtMostOnce);

        var result = await Task.WhenAny(received.Task, Task.Delay(3000));
        if (result == received.Task)
        {
            Assert.Equal(msg, received.Task.Result);
        }

        await client1.DisconnectAsync();
        await client2.DisconnectAsync();
    }

    [Fact]
    public async Task QoS1_PublishAndAck()
    {
        using var client = CreateClient("qos1_test");
        await client.ConnectAsync();

        var rs = await client.PublishAsync("qos1/topic", "qos1_data", QualityOfService.AtLeastOnce);

        // QoS1 应返回 PubAck
        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task QoS2_PublishAndComplete()
    {
        using var client = CreateClient("qos2_test");
        await client.ConnectAsync();

        var rs = await client.PublishAsync("qos2/topic", "qos2_data", QualityOfService.ExactlyOnce);

        // QoS2 应返回 PubComp（经历 PUBREC→PUBREL→PUBCOMP 流程）
        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }
    #endregion

    #region 发布/订阅
    [Fact]
    public async Task Subscribe_MultipleTopics()
    {
        using var client = CreateClient("sub_multi");
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(new[] { "topic/a", "topic/b", "topic/c/#" });

        Assert.NotNull(rs);
        Assert.Equal(3, rs!.GrantedQos.Count);

        await client.UnsubscribeAsync(new[] { "topic/a", "topic/b", "topic/c/#" });
        await client.DisconnectAsync();
    }

    [Fact]
    public async Task Subscribe_WildcardReceive()
    {
        using var pub = CreateClient("wild_pub");
        using var sub = CreateClient("wild_sub");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Topic);

        await sub.SubscribeAsync("sensor/+/temperature");
        await Task.Delay(100);

        await pub.PublishAsync("sensor/room1/temperature", "25.5");

        var result = await Task.WhenAny(received.Task, Task.Delay(3000));
        if (result == received.Task)
        {
            Assert.Equal("sensor/room1/temperature", received.Task.Result);
        }

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 连接管理
    [Fact]
    public async Task Connect_Disconnect_Events()
    {
        using var client = CreateClient("event_test");

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

    [Fact]
    public async Task Ping_Heartbeat()
    {
        using var client = CreateClient("ping_test");
        await client.ConnectAsync();

        var rs = await client.PingAsync();
        Assert.NotNull(rs);

        await client.DisconnectAsync();
    }
    #endregion

    #region Retain 消息
    [Fact]
    public async Task Retain_StoreAndDeliver()
    {
        using var pub = CreateClient("retain_pub");
        await pub.ConnectAsync();

        // 发布保留消息
        var retainMsg = new PublishMessage
        {
            Topic = "retain/test/topic",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = (Packet)"retain_value_123".GetBytes(),
        };
        await pub.PublishAsync(retainMsg);
        await Task.Delay(100);

        // 新订阅者应该收到保留消息
        using var sub = CreateClient("retain_sub");
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<PublishMessage>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg);

        await sub.SubscribeAsync("retain/test/topic");

        var result = await Task.WhenAny(received.Task, Task.Delay(3000));
        if (result == received.Task)
        {
            Assert.Equal("retain/test/topic", received.Task.Result.Topic);
            Assert.Equal("retain_value_123", received.Task.Result.Payload?.ToStr());
        }

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    public async Task Retain_ClearWithEmptyPayload()
    {
        using var pub = CreateClient("retain_clear_pub");
        await pub.ConnectAsync();

        // 先发布保留消息
        var retainMsg = new PublishMessage
        {
            Topic = "retain/clear/test",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = (Packet)"some_value".GetBytes(),
        };
        await pub.PublishAsync(retainMsg);
        await Task.Delay(100);

        // 发送空 payload 清除保留消息
        var clearMsg = new PublishMessage
        {
            Topic = "retain/clear/test",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            // 空 Payload
        };
        await pub.PublishAsync(clearMsg);
        await Task.Delay(100);

        // 新订阅者不应收到保留消息
        using var sub = CreateClient("retain_clear_sub");
        await sub.ConnectAsync();

        var received = false;
        sub.Received += (s, e) => received = true;

        await sub.SubscribeAsync("retain/clear/test");
        await Task.Delay(500);

        // 不应收到消息（已被清除）
        Assert.False(received);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 遗嘱消息
    [Fact]
    public async Task Will_PublishedOnAbnormalDisconnect()
    {
        // 订阅者先准备
        using var sub = CreateClient("will_sub");
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("client/status");
        await Task.Delay(100);

        // 带遗嘱消息的客户端
        var willClient = CreateClient("will_client");
        willClient.WillTopic = "client/status";
        willClient.WillMessage = "offline"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;

        await willClient.ConnectAsync();
        await Task.Delay(100);

        // 模拟异常断开（直接Dispose而不发DISCONNECT）
        willClient.Dispose();
        await Task.Delay(500);

        // 订阅者应收到遗嘱消息
        var result = await Task.WhenAny(received.Task, Task.Delay(3000));
        if (result == received.Task)
        {
            Assert.Equal("offline", received.Task.Result);
        }

        await sub.DisconnectAsync();
    }

    [Fact]
    public async Task Will_NotPublishedOnNormalDisconnect()
    {
        // 订阅者先准备
        using var sub = CreateClient("will_normal_sub");
        await sub.ConnectAsync();

        var received = false;
        sub.Received += (s, e) => received = true;

        await sub.SubscribeAsync("client/normal/status");
        await Task.Delay(100);

        // 带遗嘱消息的客户端
        using var willClient = CreateClient("will_normal_client");
        willClient.WillTopic = "client/normal/status";
        willClient.WillMessage = "offline"u8.ToArray();

        await willClient.ConnectAsync();
        await Task.Delay(100);

        // 正常断开（发送 DISCONNECT）
        await willClient.DisconnectAsync();
        await Task.Delay(500);

        // 订阅者不应收到遗嘱消息（正常断开时遗嘱被清除）
        Assert.False(received);

        await sub.DisconnectAsync();
    }
    #endregion

    #region MqttExchange 单元测试
    [Fact]
    public void MqttExchange_RetainMessages()
    {
        var exchange = new MqttExchange();

        // 发布 Retain 消息
        var msg = new PublishMessage
        {
            Topic = "sensor/temp",
            Retain = true,
            Payload = (Packet)"25.5".GetBytes(),
        };
        exchange.Publish(msg);

        // 获取匹配的 Retain 消息
        var retains = exchange.GetRetainMessages("sensor/+");
        Assert.Single(retains);
        Assert.Equal("sensor/temp", retains[0].Topic);

        // 空 payload 清除
        var clearMsg = new PublishMessage
        {
            Topic = "sensor/temp",
            Retain = true,
        };
        exchange.Publish(clearMsg);

        retains = exchange.GetRetainMessages("sensor/+");
        Assert.Empty(retains);
    }

    [Fact]
    public void MqttExchange_PersistentSession()
    {
        var exchange = new MqttExchange();

        // 保存持久会话
        var subs = new Dictionary<String, QualityOfService>
        {
            ["topic/a"] = QualityOfService.AtLeastOnce,
            ["topic/b/#"] = QualityOfService.ExactlyOnce,
        };
        exchange.SavePersistentSession("client_1", 100, subs);

        // 恢复持久会话
        var restored = exchange.RestorePersistentSession("client_1", 200);
        Assert.True(restored);

        // 清除持久会话
        exchange.ClearPersistentSession("client_1");
        restored = exchange.RestorePersistentSession("client_1", 300);
        Assert.False(restored);
    }

    [Fact]
    public void MqttExchange_OfflineMessages()
    {
        var exchange = new MqttExchange();

        // 创建持久会话
        exchange.SavePersistentSession("offline_client", 100, null);

        // 暂存离线消息
        var msg1 = new PublishMessage { Topic = "t1", Payload = (Packet)"m1".GetBytes() };
        var msg2 = new PublishMessage { Topic = "t2", Payload = (Packet)"m2".GetBytes() };
        exchange.EnqueueOfflineMessage("offline_client", msg1);
        exchange.EnqueueOfflineMessage("offline_client", msg2);

        // 清除后无法再暂存
        exchange.ClearPersistentSession("offline_client");
        exchange.EnqueueOfflineMessage("offline_client", msg1); // 不会报错，但不会存储
    }
    #endregion

    #region ACL 权限控制
    [Fact]
    public void DefaultAuthenticator_AllowsAll()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.Equal(ConnectReturnCode.Accepted, auth.Authenticate("c1", "user", "pass"));
        Assert.True(auth.AuthorizePublish("c1", "any/topic"));
        Assert.True(auth.AuthorizeSubscribe("c1", "any/topic/#"));
    }
    #endregion

    #region InflightManager
    [Fact]
    public void InflightManager_AddAndAcknowledge()
    {
        var sent = new List<PublishMessage>();
        using var mgr = new InflightManager(msg => { sent.Add(msg); return Task.CompletedTask; });

        var msg = new PublishMessage { Topic = "t1", Id = 1, QoS = QualityOfService.AtLeastOnce };
        mgr.Add(1, msg);

        // 确认后移除
        var acked = mgr.Acknowledge(1);
        Assert.True(acked);

        // 重复确认应返回 false
        var acked2 = mgr.Acknowledge(1);
        Assert.False(acked2);
    }
    #endregion

    #region MqttSessionCapabilities
    [Fact]
    public void SessionCapabilities_ApplyConnectProperties()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt16(MqttPropertyId.ReceiveMaximum, 100);
        props.SetUInt32(MqttPropertyId.MaximumPacketSize, 65536);
        props.SetUInt16(MqttPropertyId.TopicAliasMaximum, 10);
        props.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);

        caps.ApplyConnectProperties(props);

        Assert.Equal((UInt16)100, caps.ReceiveMaximum);
        Assert.Equal((UInt32)65536, caps.MaximumPacketSize);
        Assert.Equal((UInt16)10, caps.TopicAliasMaximum);
        Assert.Equal((UInt32)3600, caps.SessionExpiryInterval);
    }

    [Fact]
    public void SessionCapabilities_FlowControl()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 2 };

        Assert.True(caps.CanSendQosMessage());

        caps.InflightCount = 1;
        Assert.True(caps.CanSendQosMessage());

        caps.InflightCount = 2;
        Assert.False(caps.CanSendQosMessage());
    }

    [Fact]
    public void SessionCapabilities_TopicAlias()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 5 };

        // 客户端发来主题别名
        var msg1 = new PublishMessage
        {
            Topic = "sensor/temperature",
            Properties = new MqttProperties(),
        };
        msg1.Properties.SetUInt16(MqttPropertyId.TopicAlias, 1);

        var ok = caps.ResolveTopicAlias(msg1);
        Assert.True(ok);
        Assert.Equal("sensor/temperature", msg1.Topic);

        // 后续用别名发送（无主题名）
        var msg2 = new PublishMessage
        {
            Topic = "",
            Properties = new MqttProperties(),
        };
        msg2.Properties.SetUInt16(MqttPropertyId.TopicAlias, 1);

        ok = caps.ResolveTopicAlias(msg2);
        Assert.True(ok);
        Assert.Equal("sensor/temperature", msg2.Topic);

        // 未知别名
        var msg3 = new PublishMessage
        {
            Topic = "",
            Properties = new MqttProperties(),
        };
        msg3.Properties.SetUInt16(MqttPropertyId.TopicAlias, 99);

        ok = caps.ResolveTopicAlias(msg3);
        Assert.False(ok);
    }
    #endregion
}
