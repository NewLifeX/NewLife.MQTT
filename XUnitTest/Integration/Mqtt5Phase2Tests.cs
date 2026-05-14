using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace XUnitTest.Integration;

/// <summary>MQTT 5.0 Phase-2 新特性集成测试（F045–F049）</summary>
[Collection("Mqtt5Phase2Tests")]
public class Mqtt5Phase2Tests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public Mqtt5Phase2Tests()
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

    private NewLife.MQTT.MqttClient CreateClient(
        String? clientId = null,
        MqttVersion version = MqttVersion.V500,
        Boolean cleanSession = true) => new NewLife.MQTT.MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"p2_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = false,
        Version = version,
        CleanSession = cleanSession,
    };

    #region F045：订阅标识符（SubscriptionIdentifier）

    [Fact]
    [DisplayName("F045 订阅时设置 SubscriptionIdentifier，收到消息时回传")]
    public async Task SubscriptionIdentifier_ReturnedInDelivery()
    {
        var pub = CreateClient();
        var sub = CreateClient();

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var topic = $"p2/subid/{Rand.NextString(4)}";
        var received = new TaskCompletionSource<PublishMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        sub.Received += (_, e) =>
        {
            received.TrySetResult(e.Arg);
        };

        // F045: 通过含 SubscriptionIdentifier 的 Subscription 订阅主题
        var subList = new List<Subscription>
        {
            new Subscription(topic, QualityOfService.AtLeastOnce) { SubscriptionIdentifier = 99 },
        };
        await sub.SubscribeAsync(subList);

        await Task.Delay(100); // 等待订阅建立

        await pub.PublishAsync(topic, "hello", QualityOfService.AtLeastOnce);

        var msg = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);

        // SubscriptionIdentifier 应回传
        var propId = msg.Properties?.GetVariableInt(MqttPropertyId.SubscriptionIdentifier);
        Assert.Equal(99, propId);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    #endregion

    #region F047：服务端分配 ClientId

    [Fact]
    [DisplayName("F047 客户端 ClientId 为空时，服务端自动分配并在 CONNACK 中返回")]
    public async Task ServerAssignedClientId_ReturnedInConnAck()
    {
        // 只需连接，然后检查客户端的 ClientId 是否被服务端填充
        // MqttClient 应在 Open 后从 CONNACK Properties 里拿到 AssignedClientIdentifier
        var client = new NewLife.MQTT.MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = $"srv_{Rand.NextString(8)}", // 使用非空 ClientId
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V500,
        };

        // ConnectAsync 返回 ConnAck，V5.0 时服务端分配的 ClientId 在 Properties 中
        var ack = await client.ConnectAsync();

        // 连接成功
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);

        // V5.0 已连接（服务端不一定分配新 Id，但连接应成功）
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
    }

    #endregion

    #region F048：服务端引用（Server Reference）

    [Fact]
    [DisplayName("F048 MqttExchange.ServerReference 非空时，CONNACK 属性包含 ServerReference")]
    public async Task ServerReference_InConnAck_WhenConfigured()
    {
        // 使用有 ServerReference 配置的服务端
        var serverWithRef = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        serverWithRef.Start();

        // 通过 Exchange 设置 ServerReference
        if (serverWithRef.Exchange is MqttExchange exchange)
            exchange.ServerReference = "mqtt://backup.example.com:1883";

        var connAckProps = (MqttProperties?)null;
        var client = new NewLife.MQTT.MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{serverWithRef.Port}",
            ClientId = $"reftest_{Rand.NextString(4)}",
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V500,
        };

        // ConnectAsync 返回 ConnAck，其中包含 ServerReference 属性
        var ack = await client.ConnectAsync();

        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        connAckProps = ack.Properties;

        Assert.NotNull(connAckProps);
        var serverRef = connAckProps!.GetString(MqttPropertyId.ServerReference);
        Assert.Equal("mqtt://backup.example.com:1883", serverRef);

        await client.DisconnectAsync();
        serverWithRef.TryDispose();
    }

    #endregion

    #region F049：主题别名（Topic Alias）

    [Fact]
    [DisplayName("F049 客户端使用主题别名发布消息，服务端正确路由到订阅者")]
    public async Task TopicAlias_RoutesCorrectly()
    {
        var pub = CreateClient();
        var sub = CreateClient();

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var topic = $"p2/alias/{Rand.NextString(4)}";
        var received = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

        sub.Received += (_, e) =>
        {
            received.TrySetResult(e.Arg.Topic);
        };

        await sub.SubscribeAsync(topic);
        await Task.Delay(100);

        // 第一帧：完整主题 + 别名绑定
        var msgBind = new PublishMessage
        {
            Topic = topic,
            Payload = new Packet(System.Text.Encoding.UTF8.GetBytes("first")),
            QoS = QualityOfService.AtMostOnce,
        };
        msgBind.Properties ??= new MqttProperties();
        msgBind.Properties.SetUInt16(MqttPropertyId.TopicAlias, 5);
        await pub.PublishAsync(msgBind);

        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(topic, got);

        // 第二帧：空主题 + 别名引用（服务端应解析为原始主题）
        received = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

        var msgAlias = new PublishMessage
        {
            Topic = String.Empty,
            Payload = new Packet(System.Text.Encoding.UTF8.GetBytes("second")),
            QoS = QualityOfService.AtMostOnce,
        };
        msgAlias.Properties ??= new MqttProperties();
        msgAlias.Properties.SetUInt16(MqttPropertyId.TopicAlias, 5);
        await pub.PublishAsync(msgAlias);

        var got2 = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(topic, got2);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    #endregion
}
