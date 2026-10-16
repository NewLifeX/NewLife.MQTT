using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>MQTTnet Client → NewLife MqttServer 交叉集成测试。
/// 验证 NewLife 服务端能正确接受第三方 MQTT 客户端连接并处理消息</summary>
[Trait("Category", "CrossCompatibility")]
[Collection("CrossServerTests")]
public class MqttnetClientCrossTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;
    private readonly MqttClientFactory _mqttFactory = new MqttClientFactory();

    public MqttnetClientCrossTests()
    {
        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();
        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private async Task<IMqttClient> CreateAndConnectMqttnetClient(
        String clientId,
        MqttProtocolVersion version = MqttProtocolVersion.V311)
    {
        var client = _mqttFactory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _port)
            .WithClientId(clientId)
            .WithProtocolVersion(version)
            .Build();
        await client.ConnectAsync(options, CancellationToken.None);
        return client;
    }

    #region 连接与断开
    [Fact(DisplayName = "MQTTnet客户端连接NewLife服务端成功")]
    public async Task Connect_MQTTnet_Client_To_NewLife_Server()
    {
        using var client = await CreateAndConnectMqttnetClient("mqn_connect_test");
        Assert.True(client.IsConnected);
        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "MQTTnet客户端携带用户名密码连接NewLife服务端")]
    public async Task Connect_With_Credentials_To_NewLife_Server()
    {
        using var client = _mqttFactory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _port)
            .WithClientId("mqn_auth_client")
            .WithCredentials("testuser", "testpass")
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .Build();

        await client.ConnectAsync(options, CancellationToken.None);
        Assert.True(client.IsConnected);
        await client.DisconnectAsync();
    }
    #endregion

    #region 订阅与发布 QoS 0/1
    [Fact(DisplayName = "MQTTnet客户端QoS0发布订阅通过NewLife服务端转发")]
    public async Task Publish_QoS0_Subscribe_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_sub_qos0");
        using var pub = await CreateAndConnectMqttnetClient("mqn_pub_qos0");

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("mqn/qos0/test")
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var payload = "mqn_qos0_" + Rand.NextString(4);
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/qos0/test")
            .WithPayload(payload)
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }

    [Fact(DisplayName = "MQTTnet客户端QoS1发布订阅通过NewLife服务端转发")]
    public async Task Publish_QoS1_Subscribe_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_sub_qos1");
        using var pub = await CreateAndConnectMqttnetClient("mqn_pub_qos1");

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic("mqn/qos1/test").WithAtLeastOnceQoS())
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var payload = "mqn_qos1_" + Rand.NextString(4);
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/qos1/test")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 QoS1 消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 订阅与发布 QoS 2
    [Fact(DisplayName = "MQTTnet客户端QoS2发布NewLife服务端完成四步握手并投递")]
    public async Task QoS2_MQTTnet_Publish_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_qos2_sub");
        using var pub = await CreateAndConnectMqttnetClient("mqn_qos2_pub");

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic("mqn/qos2/test").WithAtLeastOnceQoS())
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var payload = "mqn_qos2_" + Rand.NextString(4);
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/qos2/test")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 QoS2 消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region Retain 消息
    [Fact(DisplayName = "MQTTnet客户端发布Retain消息NewLife服务端正确存储并投递")]
    public async Task Retain_MQTTnet_Publish_NewLife_Server_Delivers()
    {
        using var pub = await CreateAndConnectMqttnetClient("mqn_retain_pub");

        var retainMsg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/retain/test")
            .WithPayload("retain_value_mqn")
            .WithRetainFlag()
            .Build();
        await pub.PublishAsync(retainMsg, CancellationToken.None);
        await Task.Delay(200);

        // 新订阅者连接后应收到 retain 消息
        using var sub = await CreateAndConnectMqttnetClient("mqn_retain_sub");
        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("mqn/retain/test")
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "新订阅者未收到 Retain 消息");
        Assert.Equal("retain_value_mqn", tcs.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region Will 消息
    [Fact(DisplayName = "MQTTnet客户端异常断开NewLife服务端发布遗嘱消息")]
    public async Task Will_MQTTnet_AbnormalDisconnect_NewLife_Server_Publishes()
    {
        // 订阅者先订阅遗嘱主题
        using var sub = await CreateAndConnectMqttnetClient("mqn_will_sub");
        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };
        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("mqn/will/status")
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(100);

        // 带遗嘱消息的客户端连接
        var willClient = _mqttFactory.CreateMqttClient();
        var willOptions = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _port)
            .WithClientId("mqn_will_client")
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithWillTopic("mqn/will/status")
            .WithWillPayload("offline")
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();
        await willClient.ConnectAsync(willOptions, CancellationToken.None);
        await Task.Delay(100);

        // 异常断开（不发 DISCONNECT 报文）
        willClient.Dispose();
        await Task.Delay(500);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未收到遗嘱消息");
        Assert.Equal("offline", tcs.Task.Result);

        await sub.DisconnectAsync();
    }
    #endregion

    #region 通配符订阅
    [Fact(DisplayName = "MQTTnet客户端通配符+订阅通过NewLife服务端路由")]
    public async Task Subscribe_Wildcard_Plus_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_wild_sub");
        using var pub = await CreateAndConnectMqttnetClient("mqn_wild_pub");

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(e.ApplicationMessage.Topic);
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("mqn/+/temperature")
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/room1/temperature")
            .WithPayload("22.5")
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未收到通配符消息");
        Assert.Equal("mqn/room1/temperature", tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }

    [Fact(DisplayName = "MQTTnet客户端通配符#订阅通过NewLife服务端路由")]
    public async Task Subscribe_Wildcard_Hash_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_hash_sub");
        using var pub = await CreateAndConnectMqttnetClient("mqn_hash_pub");

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(e.ApplicationMessage.Topic);
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("mqn/sensor/#")
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/sensor/room1/temperature")
            .WithPayload("22.3")
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未收到 # 通配符消息");
        Assert.Equal("mqn/sensor/room1/temperature", tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 断线重连
    [Fact(DisplayName = "MQTTnet客户端断线后重新连接NewLife服务端")]
    public async Task Reconnect_MQTTnet_Client_To_NewLife_Server()
    {
        using var client = _mqttFactory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _port)
            .WithClientId("mqn_reconnect_test")
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .Build();

        await client.ConnectAsync(options, CancellationToken.None);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.False(client.IsConnected);

        // 重新连接
        await client.ConnectAsync(options, CancellationToken.None);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }
    #endregion

    #region MQTT 5.0 协议互操作
    [Fact(DisplayName = "MQTTnet客户端V500连接NewLife服务端并完成发布订阅")]
    public async Task MQTT5_Connect_And_PubSub_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_v5_sub", MqttProtocolVersion.V500);
        using var pub = await CreateAndConnectMqttnetClient("mqn_v5_pub", MqttProtocolVersion.V500);

        Assert.True(sub.IsConnected);
        Assert.True(pub.IsConnected);

        var tcs = new TaskCompletionSource<String>();
        sub.ApplicationMessageReceivedAsync += e =>
        {
            tcs.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray()));
            return Task.CompletedTask;
        };

        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic("mqn/v5/test").WithAtLeastOnceQoS())
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        var payload = "mqn_v5_" + Rand.NextString(4);
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("mqn/v5/test")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await pub.PublishAsync(msg, CancellationToken.None);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(4000));
        Assert.True(winner == tcs.Task, "未在超时内收到 V500 消息");
        Assert.Equal(payload, tcs.Task.Result);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 取消订阅
    [Fact(DisplayName = "MQTTnet客户端取消订阅后不再收到NewLife服务端消息")]
    public async Task Unsubscribe_Stops_Delivery_Via_NewLife_Server()
    {
        using var sub = await CreateAndConnectMqttnetClient("mqn_unsub_sub");
        using var pub = await CreateAndConnectMqttnetClient("mqn_unsub_pub");

        var count = 0;
        sub.ApplicationMessageReceivedAsync += e =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        };

        var topic = $"mqn/unsub/{Rand.NextString(4)}";
        var subOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();
        await sub.SubscribeAsync(subOptions, CancellationToken.None);
        await Task.Delay(200);

        // 先发一条确认订阅生效
        var msg1 = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload("msg1")
            .Build();
        await pub.PublishAsync(msg1, CancellationToken.None);
        await Task.Delay(500);
        Assert.Equal(1, count);

        // 取消订阅后再发，不应收到
        var unsubOptions = _mqttFactory.CreateUnsubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();
        await sub.UnsubscribeAsync(unsubOptions, CancellationToken.None);
        await Task.Delay(100);

        var msg2 = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload("msg2")
            .Build();
        await pub.PublishAsync(msg2, CancellationToken.None);
        await Task.Delay(500);
        Assert.Equal(1, count);

        await sub.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion

    #region 共享订阅
    [Fact(DisplayName = "MQTTnet客户端共享订阅通过NewLife服务端轮询分发")]
    public async Task SharedSubscription_MQTTnet_Clients_RoundRobin_Via_NewLife_Server()
    {
        using var sub1 = await CreateAndConnectMqttnetClient("mqn_share_sub1");
        using var sub2 = await CreateAndConnectMqttnetClient("mqn_share_sub2");
        using var pub = await CreateAndConnectMqttnetClient("mqn_share_pub");

        var count1 = 0;
        var count2 = 0;
        sub1.ApplicationMessageReceivedAsync += e =>
        {
            Interlocked.Increment(ref count1);
            return Task.CompletedTask;
        };
        sub2.ApplicationMessageReceivedAsync += e =>
        {
            Interlocked.Increment(ref count2);
            return Task.CompletedTask;
        };

        var topic = $"mqn/share/{Rand.NextString(4)}";
        var filter = "$share/grp/" + topic;

        var subOptions1 = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(filter).WithAtLeastOnceQoS())
            .Build();
        await sub1.SubscribeAsync(subOptions1, CancellationToken.None);
        var subOptions2 = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(filter).WithAtLeastOnceQoS())
            .Build();
        await sub2.SubscribeAsync(subOptions2, CancellationToken.None);
        await Task.Delay(300);

        const Int32 total = 10;
        for (var i = 0; i < total; i++)
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload($"share_{i}")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await pub.PublishAsync(msg, CancellationToken.None);
        }

        // 等待全部消息投递完成
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (count1 + count2 < total && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Equal(total, count1 + count2);
        Assert.True(count1 > 0 && count2 > 0, $"共享订阅应轮询分发到组内两个订阅者，实际 sub1={count1} sub2={count2}");

        await sub1.DisconnectAsync();
        await sub2.DisconnectAsync();
        await pub.DisconnectAsync();
    }
    #endregion
}
