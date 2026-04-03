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

/// <summary>综合集成测试。全面覆盖 MQTT 核心功能，同时测试 MQTT 3.1.1 和 5.0 协议版本</summary>
[Collection("ComprehensiveIntegration")]
public class ComprehensiveMqttIntegrationTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public ComprehensiveMqttIntegrationTests()
    {
        // 使用 Port=0 让系统自动分配随机端口，避免端口冲突
        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);

        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();

        // 获取实际分配的端口
        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateClient(String? clientId = null, MqttVersion version = MqttVersion.V311)
    {
        return new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId ?? $"test_{version}_{Rand.NextString(4)}",
            Timeout = 5000,
            Reconnect = false,
            Version = version,
        };
    }

    #region 1. 连接功能测试
    [Fact(DisplayName = "1.1 V311 客户端连接")]
    public async Task Connect_V311_Client()
    {
        using var client = CreateClient("connect_v311", MqttVersion.V311);

        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "1.2 V500 客户端连接")]
    public async Task Connect_V500_Client()
    {
        using var client = CreateClient("connect_v500", MqttVersion.V500);

        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "1.3 多客户端同时连接")]
    public async Task Connect_Multiple_Clients()
    {
        using var client1 = CreateClient("multi_1", MqttVersion.V311);
        using var client2 = CreateClient("multi_2", MqttVersion.V500);
        using var client3 = CreateClient("multi_3", MqttVersion.V311);

        // 并行连接
        await Task.WhenAll(
            client1.ConnectAsync(),
            client2.ConnectAsync(),
            client3.ConnectAsync()
        );

        Assert.True(client1.IsConnected);
        Assert.True(client2.IsConnected);
        Assert.True(client3.IsConnected);

        await Task.WhenAll(
            client1.DisconnectAsync(),
            client2.DisconnectAsync(),
            client3.DisconnectAsync()
        );
    }

    [Fact(DisplayName = "1.4 连接事件触发")]
    public async Task Connect_Events_Triggered()
    {
        using var client = CreateClient("events_client", MqttVersion.V311);

        var connected = false;
        var disconnected = false;

        client.Connected += (s, e) => connected = true;
        client.Disconnected += (s, e) => disconnected = true;

        await client.ConnectAsync();
        Assert.True(connected);

        await client.DisconnectAsync();
        Assert.True(disconnected);
    }
    #endregion

    #region 2. 订阅功能测试
    [Fact(DisplayName = "2.1 V311 单主题订阅")]
    public async Task Subscribe_V311_SingleTopic()
    {
        using var client = CreateClient("sub_v311_single", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(new[] { "test/single" });

        Assert.NotNull(rs);
        Assert.Single(rs!.GrantedQos);

        await client.UnsubscribeAsync(new[] { "test/single" });
        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "2.2 V500 单主题订阅")]
    public async Task Subscribe_V500_SingleTopic()
    {
        using var client = CreateClient("sub_v500_single", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(new[] { "test/single" });

        Assert.NotNull(rs);
        Assert.Single(rs!.GrantedQos);

        await client.UnsubscribeAsync(new[] { "test/single" });
        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "2.3 多主题订阅")]
    public async Task Subscribe_MultipleTopics()
    {
        using var client = CreateClient("sub_multi", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(new[] { "topic/a", "topic/b", "topic/c/#" });

        Assert.NotNull(rs);
        Assert.Equal(3, rs!.GrantedQos.Count);

        await client.UnsubscribeAsync(new[] { "topic/a", "topic/b", "topic/c/#" });
        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "2.4 通配符订阅")]
    public async Task Subscribe_Wildcard()
    {
        using var client = CreateClient("sub_wildcard", MqttVersion.V311);
        await client.ConnectAsync();

        // + 单层通配符
        var rs1 = await client.SubscribeAsync(new[] { "sensor/+/temperature" });
        Assert.NotNull(rs1);

        // # 多层通配符
        var rs2 = await client.SubscribeAsync(new[] { "device/#" });
        Assert.NotNull(rs2);

        await client.DisconnectAsync();
    }
    #endregion

    #region 3. 发布功能测试
    [Fact(DisplayName = "3.1 V311 发布 QoS0 消息")]
    public async Task Publish_V311_QoS0()
    {
        using var client = CreateClient("pub_v311_qos0", MqttVersion.V311);
        await client.ConnectAsync();

        // QoS0 不需要确认，返回 null
        var rs = await client.PublishAsync("test/qos0", "hello", QualityOfService.AtMostOnce);
        Assert.Null(rs);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "3.2 V311 发布 QoS1 消息")]
    public async Task Publish_V311_QoS1()
    {
        using var client = CreateClient("pub_v311_qos1", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("test/qos1", "hello", QualityOfService.AtLeastOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "3.3 V311 发布 QoS2 消息")]
    public async Task Publish_V311_QoS2()
    {
        using var client = CreateClient("pub_v311_qos2", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("test/qos2", "hello", QualityOfService.ExactlyOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "3.4 V500 发布 QoS0 消息")]
    public async Task Publish_V500_QoS0()
    {
        using var client = CreateClient("pub_v500_qos0", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("test/qos0", "hello", QualityOfService.AtMostOnce);
        Assert.Null(rs);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "3.5 V500 发布 QoS1 消息")]
    public async Task Publish_V500_QoS1()
    {
        using var client = CreateClient("pub_v500_qos1", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("test/qos1", "hello", QualityOfService.AtLeastOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "3.6 V500 发布 QoS2 消息")]
    public async Task Publish_V500_QoS2()
    {
        using var client = CreateClient("pub_v500_qos2", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("test/qos2", "hello", QualityOfService.ExactlyOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }
    #endregion

    #region 4. 消费（接收）功能测试
    [Fact(DisplayName = "4.1 V311 发布 → V311 订阅接收")]
    public async Task Consume_V311_To_V311()
    {
        using var publisher = CreateClient("pub_v311", MqttVersion.V311);
        using var subscriber = CreateClient("sub_v311", MqttVersion.V311);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "consume/v311" });
        await Task.Delay(200);

        var payload = "hello_v311_" + Rand.NextString(4);
        await publisher.PublishAsync("consume/v311", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到消息");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "4.2 V500 发布 → V500 订阅接收")]
    public async Task Consume_V500_To_V500()
    {
        using var publisher = CreateClient("pub_v500", MqttVersion.V500);
        using var subscriber = CreateClient("sub_v500", MqttVersion.V500);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "consume/v500" });
        await Task.Delay(200);

        var payload = "hello_v500_" + Rand.NextString(4);
        await publisher.PublishAsync("consume/v500", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到消息");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "4.3 V311 发布 → V500 订阅接收（跨版本）")]
    public async Task Consume_V311_To_V500()
    {
        using var publisher = CreateClient("cross_pub_v311", MqttVersion.V311);
        using var subscriber = CreateClient("cross_sub_v500", MqttVersion.V500);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "cross/v311_to_v500" });
        await Task.Delay(200);

        var payload = "cross_v311_to_v500_" + Rand.NextString(4);
        await publisher.PublishAsync("cross/v311_to_v500", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到跨协议消息");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "4.4 V500 发布 → V311 订阅接收（跨版本）")]
    public async Task Consume_V500_To_V311()
    {
        using var publisher = CreateClient("cross_pub_v500", MqttVersion.V500);
        using var subscriber = CreateClient("cross_sub_v311", MqttVersion.V311);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "cross/v500_to_v311" });
        await Task.Delay(200);

        var payload = "cross_v500_to_v311_" + Rand.NextString(4);
        await publisher.PublishAsync("cross/v500_to_v311", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到跨协议消息");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "4.5 一对多：一个发布者，多个订阅者")]
    public async Task Consume_One_Publisher_Multiple_Subscribers()
    {
        using var publisher = CreateClient("fanout_pub", MqttVersion.V311);
        using var sub1 = CreateClient("fanout_sub1", MqttVersion.V311);
        using var sub2 = CreateClient("fanout_sub2", MqttVersion.V500);
        using var sub3 = CreateClient("fanout_sub3", MqttVersion.V311);

        await publisher.ConnectAsync();
        await sub1.ConnectAsync();
        await sub2.ConnectAsync();
        await sub3.ConnectAsync();

        var received1 = new TaskCompletionSource<String>();
        var received2 = new TaskCompletionSource<String>();
        var received3 = new TaskCompletionSource<String>();

        sub1.Received += (s, e) => received1.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        sub2.Received += (s, e) => received2.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        sub3.Received += (s, e) => received3.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub1.SubscribeAsync(new[] { "fanout/broadcast" });
        await sub2.SubscribeAsync(new[] { "fanout/broadcast" });
        await sub3.SubscribeAsync(new[] { "fanout/broadcast" });
        await Task.Delay(300);

        var payload = "broadcast_" + Rand.NextString(4);
        await publisher.PublishAsync("fanout/broadcast", payload, QualityOfService.AtMostOnce);

        var winner1 = await Task.WhenAny(received1.Task, Task.Delay(5000));
        var winner2 = await Task.WhenAny(received2.Task, Task.Delay(5000));
        var winner3 = await Task.WhenAny(received3.Task, Task.Delay(5000));

        Assert.True(winner1 == received1.Task, "订阅者1未收到消息");
        Assert.Equal(payload, await received1.Task);

        Assert.True(winner2 == received2.Task, "订阅者2未收到消息");
        Assert.Equal(payload, await received2.Task);

        Assert.True(winner3 == received3.Task, "订阅者3未收到消息");
        Assert.Equal(payload, await received3.Task);

        await publisher.DisconnectAsync();
        await sub1.DisconnectAsync();
        await sub2.DisconnectAsync();
        await sub3.DisconnectAsync();
    }

    [Fact(DisplayName = "4.6 通配符订阅接收消息")]
    public async Task Consume_Wildcard_Subscription()
    {
        using var publisher = CreateClient("wild_pub", MqttVersion.V311);
        using var subscriber = CreateClient("wild_sub", MqttVersion.V311);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Topic);

        // 订阅单层通配符
        await subscriber.SubscribeAsync(new[] { "sensor/+/temperature" });
        await Task.Delay(200);

        // 发布到匹配的主题
        await publisher.PublishAsync("sensor/room1/temperature", "25.5");

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到通配符匹配消息");
        Assert.Equal("sensor/room1/temperature", await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }
    #endregion

    #region 5. 断开功能测试
    [Fact(DisplayName = "5.1 V311 正常断开")]
    public async Task Disconnect_V311_Normal()
    {
        using var client = CreateClient("disconnect_v311", MqttVersion.V311);

        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        // DisconnectAsync 发送 DISCONNECT 包后等待服务端关闭连接
    }

    [Fact(DisplayName = "5.2 V500 正常断开")]
    public async Task Disconnect_V500_Normal()
    {
        using var client = CreateClient("disconnect_v500", MqttVersion.V500);

        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "5.3 断开后再次连接")]
    public async Task Disconnect_And_Reconnect()
    {
        using var client = CreateClient("reconnect_test", MqttVersion.V311);

        // 第一次连接
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();

        // 等待断开完成
        await Task.Delay(200);

        // 第二次连接
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }
    #endregion

    #region 6. 高级功能测试
    [Fact(DisplayName = "6.1 Retain 消息存储和传递")]
    public async Task Advanced_Retain_Message()
    {
        using var publisher = CreateClient("retain_pub", MqttVersion.V311);
        await publisher.ConnectAsync();

        // 发布保留消息
        var retainMsg = new PublishMessage
        {
            Topic = "retain/test/topic",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = new ArrayPacket("retain_value_123"u8.ToArray()),
        };
        await publisher.PublishAsync(retainMsg);
        await Task.Delay(200);

        // 新订阅者应该收到保留消息
        using var subscriber = CreateClient("retain_sub", MqttVersion.V311);
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "retain/test/topic" });

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到保留消息");
        Assert.Equal("retain_value_123", await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "6.2 遗嘱消息：异常断开时发布")]
    public async Task Advanced_Will_Message_Abnormal_Disconnect()
    {
        // 订阅者先准备
        using var subscriber = CreateClient("will_sub", MqttVersion.V311);
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync(new[] { "client/will/status" });
        await Task.Delay(200);

        // 带遗嘱消息的客户端
        var willClient = CreateClient("will_client", MqttVersion.V311);
        willClient.WillTopic = "client/will/status";
        willClient.WillMessage = "offline"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;

        await willClient.ConnectAsync();
        await Task.Delay(200);

        // 模拟异常断开（直接 Dispose 而不发 DISCONNECT）
        willClient.Dispose();

        // 等待服务端检测到断开并发送遗嘱消息
        var winner = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.True(winner == received.Task, "5 秒内未收到遗嘱消息");
        Assert.Equal("offline", await received.Task);

        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "6.3 遗嘱消息：正常断开时不发布")]
    public async Task Advanced_Will_Message_Normal_Disconnect()
    {
        // 订阅者先准备
        using var subscriber = CreateClient("will_normal_sub", MqttVersion.V311);
        await subscriber.ConnectAsync();

        var received = false;
        subscriber.Received += (s, e) => received = true;

        await subscriber.SubscribeAsync(new[] { "client/normal/status" });
        await Task.Delay(200);

        // 带遗嘱消息的客户端
        using var willClient = CreateClient("will_normal_client", MqttVersion.V311);
        willClient.WillTopic = "client/normal/status";
        willClient.WillMessage = "offline"u8.ToArray();

        await willClient.ConnectAsync();
        await Task.Delay(200);

        // 正常断开（发送 DISCONNECT）
        await willClient.DisconnectAsync();
        await Task.Delay(500);

        // 订阅者不应收到遗嘱消息（正常断开时遗嘱被清除）
        Assert.False(received, "正常断开不应发送遗嘱消息");

        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "6.4 心跳保活（Ping/Pong）")]
    public async Task Advanced_Ping_Pong()
    {
        using var client = CreateClient("ping_test", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.PingAsync();
        Assert.NotNull(rs);

        await client.DisconnectAsync();
    }

    [Fact(DisplayName = "6.5 持久会话：CleanSession=false")]
    public async Task Advanced_Persistent_Session()
    {
        const String clientId = "persistent_client_v311";

        // 第一次连接，CleanSession=false
        using var client1 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V311,
            CleanSession = false,
        };

        await client1.ConnectAsync();
        await client1.SubscribeAsync(new[] { "persistent/topic" });
        await client1.DisconnectAsync();

        // 等待服务端保存持久会话
        await Task.Delay(300);

        // 第二次连接，同一 ClientId，CleanSession=false
        using var client2 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V311,
            CleanSession = false,
        };

        var ack = await client2.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        // SessionPresent=true 表示服务端找到了持久会话
        Assert.True(ack.SessionPresent);

        await client2.DisconnectAsync();
    }
    #endregion

    #region 7. 完整场景测试
    [Fact(DisplayName = "7.1 完整场景：连接→订阅→发布→接收→断开")]
    public async Task FullScenario_Complete_Flow()
    {
        // 创建发布者和订阅者
        using var publisher = CreateClient("scenario_pub", MqttVersion.V311);
        using var subscriber = CreateClient("scenario_sub", MqttVersion.V500);

        // 1. 连接
        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();
        Assert.True(publisher.IsConnected);
        Assert.True(subscriber.IsConnected);

        // 2. 订阅
        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        var subAck = await subscriber.SubscribeAsync(new[] { "scenario/test" });
        Assert.NotNull(subAck);
        await Task.Delay(200);

        // 3. 发布
        var payload = "scenario_test_" + Rand.NextString(4);
        await publisher.PublishAsync("scenario/test", payload, QualityOfService.AtMostOnce);

        // 4. 接收
        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到消息");
        Assert.Equal(payload, await received.Task);

        // 5. 断开
        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact(DisplayName = "7.2 完整场景：V311+V500 混合通信")]
    public async Task FullScenario_Mixed_Versions()
    {
        using var pubV311 = CreateClient("mixed_pub_v311", MqttVersion.V311);
        using var pubV500 = CreateClient("mixed_pub_v500", MqttVersion.V500);
        using var subV311 = CreateClient("mixed_sub_v311", MqttVersion.V311);
        using var subV500 = CreateClient("mixed_sub_v500", MqttVersion.V500);

        // 全部连接
        await Task.WhenAll(
            pubV311.ConnectAsync(),
            pubV500.ConnectAsync(),
            subV311.ConnectAsync(),
            subV500.ConnectAsync()
        );

        var received311 = new TaskCompletionSource<String>();
        var received500 = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => received311.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subV500.Received += (s, e) => received500.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        // 订阅同一主题
        await subV311.SubscribeAsync(new[] { "mixed/topic" });
        await subV500.SubscribeAsync(new[] { "mixed/topic" });
        await Task.Delay(300);

        // V311 发布者发布消息
        var payload1 = "from_v311_" + Rand.NextString(4);
        await pubV311.PublishAsync("mixed/topic", payload1, QualityOfService.AtMostOnce);

        // 两个订阅者都应收到
        var winner311_1 = await Task.WhenAny(received311.Task, Task.Delay(5000));
        Assert.True(winner311_1 == received311.Task, "V311 订阅者未收到 V311 发布者的消息");
        Assert.Equal(payload1, await received311.Task);

        var winner500_1 = await Task.WhenAny(received500.Task, Task.Delay(5000));
        Assert.True(winner500_1 == received500.Task, "V500 订阅者未收到 V311 发布者的消息");
        Assert.Equal(payload1, await received500.Task);

        // 重置 TaskCompletionSource
        received311 = new TaskCompletionSource<String>();
        received500 = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => received311.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subV500.Received += (s, e) => received500.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        // V500 发布者发布消息
        var payload2 = "from_v500_" + Rand.NextString(4);
        await pubV500.PublishAsync("mixed/topic", payload2, QualityOfService.AtMostOnce);

        // 两个订阅者都应收到
        var winner311_2 = await Task.WhenAny(received311.Task, Task.Delay(5000));
        Assert.True(winner311_2 == received311.Task, "V311 订阅者未收到 V500 发布者的消息");
        Assert.Equal(payload2, await received311.Task);

        var winner500_2 = await Task.WhenAny(received500.Task, Task.Delay(5000));
        Assert.True(winner500_2 == received500.Task, "V500 订阅者未收到 V500 发布者的消息");
        Assert.Equal(payload2, await received500.Task);

        // 全部断开
        await Task.WhenAll(
            pubV311.DisconnectAsync(),
            pubV500.DisconnectAsync(),
            subV311.DisconnectAsync(),
            subV500.DisconnectAsync()
        );
    }
    #endregion
}
