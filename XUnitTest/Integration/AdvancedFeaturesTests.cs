using System;
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

/// <summary>高级特性补充集成测试。覆盖需求文档已实现但尚未有端到端验证的功能点：
/// RetainHandling 模式 1/2、ACL 发布拒绝（V500 PUBACK ReasonCode=0x87）、
/// $SYS 系统主题订阅投递，以及 MqttBridge 跨 Broker 消息桥接（Out 方向）。</summary>
[Collection("AdvancedFeaturesTests")]
public class AdvancedFeaturesTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public AdvancedFeaturesTests()
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
        MqttVersion version = MqttVersion.V311,
        Boolean cleanSession = true) => new NewLife.MQTT.MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"adv_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = false,
        Version = version,
        CleanSession = cleanSession,
    };

    #region RetainHandling 模式 1：仅首次订阅发送 Retain 消息
    [Fact]
    [DisplayName("F022 RetainHandling=1：首次订阅收到 Retain；订阅已存在时重新订阅不再收到")]
    public async Task RetainHandling1_OnlyNewSubscription()
    {
        var topic = $"adv/retain1/{Rand.NextString(4)}";
        using var pub = CreateClient("rh1_pub");
        await pub.ConnectAsync();

        // 发布 Retain 消息
        await pub.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket("rh1_retained"u8.ToArray()),
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        });
        await Task.Delay(200);

        // 第一次订阅（新订阅）RetainHandling=1，应收到 Retain 消息
        using var sub = CreateClient("rh1_sub");
        await sub.ConnectAsync();

        var firstReceived = new TaskCompletionSource<String>();
        sub.Received += (s, e) => firstReceived.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync([new Subscription(topic, QualityOfService.AtMostOnce) { RetainHandling = 1 }]);
        var w1 = await Task.WhenAny(firstReceived.Task, Task.Delay(3000));
        Assert.True(w1 == firstReceived.Task, "RetainHandling=1：首次订阅 3s 内未收到 Retain 消息");
        Assert.Equal("rh1_retained", firstReceived.Task.Result);

        // 不退订，直接重新订阅（订阅已存在），RetainHandling=1 不应再次推送 Retain
        var resubReceived = false;
        sub.Received += (s, e) => { if (e.Arg.Retain) resubReceived = true; };
        await sub.SubscribeAsync([new Subscription(topic, QualityOfService.AtMostOnce) { RetainHandling = 1 }]);

        // 等待确认未收到 Retain 推送
        await Task.Delay(1200);
        Assert.False(resubReceived, "RetainHandling=1：订阅已存在时重新订阅不应再次收到 Retain 消息");

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region RetainHandling 模式 2：订阅时永不发送 Retain 消息
    [Fact]
    [DisplayName("F022 RetainHandling=2：订阅时不推送任何 Retain 消息")]
    public async Task RetainHandling2_NeverSendOnSubscribe()
    {
        var topic = $"adv/retain2/{Rand.NextString(4)}";
        using var pub = CreateClient("rh2_pub");
        await pub.ConnectAsync();

        // 发布 Retain 消息
        await pub.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket("rh2_retained"u8.ToArray()),
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        });
        await Task.Delay(200);

        // 使用 RetainHandling=2 订阅，不应收到 Retain 消息
        using var sub = CreateClient("rh2_sub");
        await sub.ConnectAsync();

        var retainReceived = false;
        sub.Received += (s, e) => retainReceived = true;
        await sub.SubscribeAsync([new Subscription(topic, QualityOfService.AtMostOnce) { RetainHandling = 2 }]);

        // 等待确认未收到 Retain 推送
        await Task.Delay(1200);
        Assert.False(retainReceived, "RetainHandling=2：订阅时不应收到任何 Retain 消息");

        // 验证：普通消息（非 Retain）仍然正常投递
        var normalReceived = new TaskCompletionSource<String>();
        sub.Received += (s, e) => normalReceived.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        var normalPayload = "rh2_normal_" + Rand.NextString(4);
        await pub.PublishAsync(topic, normalPayload, QualityOfService.AtMostOnce);

        var w = await Task.WhenAny(normalReceived.Task, Task.Delay(3000));
        Assert.True(w == normalReceived.Task, "RetainHandling=2：普通非 Retain 消息应正常投递");
        Assert.Equal(normalPayload, normalReceived.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region ACL 发布拒绝：V500 QoS1 返回 PUBACK ReasonCode=0x87
    [Fact]
    [DisplayName("F014 ACL 发布拒绝：V500 QoS1 向受限主题发布返回 PUBACK ReasonCode=0x87")]
    public async Task ACL_V500_PublishRejected_ReturnsReasonCode0x87()
    {
        // 独立服务端，注入只拒绝 restricted/* 发布的 ACL
        var aclServer = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        aclServer.Authenticator = new PublishBlockAuthenticator();
        aclServer.Start();
        var aclPort = aclServer.Port;

        try
        {
            using var client = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{aclPort}",
                ClientId = "acl_v500_pub",
                Timeout = 5000,
                Reconnect = false,
                Version = MqttVersion.V500,
            };
            await client.ConnectAsync();

            // 向受限主题发布 QoS1，应返回带拒绝码的 PUBACK
            var ack = await client.PublishAsync("restricted/topic", "test_payload", QualityOfService.AtLeastOnce);

            Assert.NotNull(ack);
            var pubAck = Assert.IsType<PubAck>(ack);
            Assert.Equal(0x87, pubAck.ReasonCode); // Not Authorized

            // 向允许主题发布 QoS1，应正常返回 PUBACK (ReasonCode=0x00)
            var ack2 = await client.PublishAsync("allowed/topic", "test_payload", QualityOfService.AtLeastOnce);
            Assert.NotNull(ack2);
            var pubAck2 = Assert.IsType<PubAck>(ack2);
            Assert.Equal(0x00, pubAck2.ReasonCode);

            await client.DisconnectAsync();
        }
        finally
        {
            aclServer.TryDispose();
        }
    }
    #endregion

    #region $SYS 系统主题订阅 E2E
    [Fact]
    [DisplayName("F015 $SYS 系统主题：客户端订阅 $SYS/# 并在定时器触发后收到统计消息")]
    public async Task SysTopics_ClientSubscribes_ReceivesStats()
    {
        // 独立服务端，缩短 $SYS 推送间隔以加速测试
        var sysServer = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        sysServer.Start();
        var sysPort = sysServer.Port;

        // 必须在第一个客户端连接之前设置间隔，确保定时器以 1s 间隔创建
        (sysServer.Exchange as MqttExchange)!.SysTopicInterval = 1;

        try
        {
            using var sub = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{sysPort}",
                ClientId = "sys_sub_client",
                Timeout = 5000,
                Reconnect = false,
            };
            await sub.ConnectAsync();

            var sysTopicReceived = new TaskCompletionSource<String>();
            sub.Received += (s, e) =>
            {
                if (e.Arg.Topic.StartsWith("$SYS/"))
                    sysTopicReceived.TrySetResult(e.Arg.Topic);
            };

            // 订阅所有 $SYS 主题
            await sub.SubscribeAsync("$SYS/#");

            // 等待 $SYS 定时器触发（最多 5 秒）
            var w = await Task.WhenAny(sysTopicReceived.Task, Task.Delay(5000));
            Assert.True(w == sysTopicReceived.Task, "5 秒内未收到任何 $SYS 系统统计主题消息");

            var receivedTopic = sysTopicReceived.Task.Result;
            Assert.StartsWith("$SYS/broker/", receivedTopic);

            await sub.DisconnectAsync();
        }
        finally
        {
            sysServer.TryDispose();
        }
    }
    #endregion

    #region MqttBridge E2E：本地 Broker → 远端 Broker 消息转发（Out 方向）
    [Fact]
    [DisplayName("F037 MqttBridge Out：本地发布消息经 Bridge 转发到远端 Broker 的订阅者")]
    public async Task MqttBridge_Out_ForwardsToRemoteBroker()
    {
        // 远端 Broker（被动接收方）
        var remoteBroker = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        remoteBroker.Start();
        var remotePort = remoteBroker.Port;

        // 本地 Broker（触发方）
        var localBroker = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        localBroker.Start();
        var localPort = localBroker.Port;

        var suffix = Rand.NextString(4);
        var bridgeTopic = $"bridge/test/{suffix}";

        // 配置 Bridge：本地 bridge/# 消息转发到远端
        var bridge = new MqttBridge
        {
            Name = "e2e_bridge",
            RemoteServer = $"tcp://127.0.0.1:{remotePort}",
            ClientId = "bridge_client_e2e",
            Exchange = localBroker.Exchange,
            Log = XTrace.Log,
        };
        bridge.Rules.Add(new MqttBridgeRule
        {
            LocalTopic = "bridge/#",
            Direction = BridgeDirection.Out,
        });
        await bridge.StartAsync();

        // 配置规则引擎，将匹配主题路由到 Bridge
        var ruleEngine = new MqttRuleEngine { Exchange = localBroker.Exchange };
        ruleEngine.AddRule(new MqttRule
        {
            Name = "bridge_out_rule",
            TopicFilter = "bridge/#",
            ActionType = RuleActionType.Bridge,
            BridgeName = "e2e_bridge",
        });
        ruleEngine.Bridges["e2e_bridge"] = bridge;
        (localBroker.Exchange as MqttExchange)!.RuleEngine = ruleEngine;

        try
        {
            // 在远端 Broker 上订阅目标主题
            using var remoteSub = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{remotePort}",
                ClientId = "bridge_remote_sub",
                Timeout = 5000,
                Reconnect = false,
            };
            await remoteSub.ConnectAsync();
            var bridgeReceived = new TaskCompletionSource<String>();
            remoteSub.Received += (s, e) => bridgeReceived.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
            await remoteSub.SubscribeAsync(bridgeTopic);
            await Task.Delay(300);

            // 在本地 Broker 发布消息，规则引擎 → Bridge → 远端 Broker
            using var localPub = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{localPort}",
                ClientId = "bridge_local_pub",
                Timeout = 5000,
                Reconnect = false,
            };
            await localPub.ConnectAsync();
            var bridgePayload = "bridge_msg_" + Rand.NextString(4);
            await localPub.PublishAsync(bridgeTopic, bridgePayload, QualityOfService.AtMostOnce);

            // 等待远端订阅者收到桥接转发的消息
            var w = await Task.WhenAny(bridgeReceived.Task, Task.Delay(6000));
            Assert.True(w == bridgeReceived.Task, "6 秒内远端 Broker 订阅者未收到桥接消息");
            Assert.Equal(bridgePayload, bridgeReceived.Task.Result);

            await localPub.DisconnectAsync();
            await remoteSub.DisconnectAsync();
        }
        finally
        {
            await bridge.StopAsync();
            bridge.TryDispose();
            localBroker.TryDispose();
            remoteBroker.TryDispose();
        }
    }
    #endregion

    #region MqttBridge E2E：远端 Broker → 本地 Broker 消息订阅（In 方向）
    [Fact]
    [DisplayName("F037 MqttBridge In：远端 Broker 消息经 Bridge 转发到本地 Broker 的订阅者")]
    public async Task MqttBridge_In_ForwardsFromRemoteBroker()
    {
        // 远端 Broker（消息来源）
        var remoteBroker = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        remoteBroker.Start();
        var remotePort = remoteBroker.Port;

        // 本地 Broker（订阅接收方）
        var localBroker = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        localBroker.Start();
        var localPort = localBroker.Port;

        var suffix = Rand.NextString(4);
        var remoteTopic = $"remote/data/{suffix}";

        // 配置 Bridge：订阅远端 remote/# 消息并转发到本地
        var bridge = new MqttBridge
        {
            Name = "e2e_in_bridge",
            RemoteServer = $"tcp://127.0.0.1:{remotePort}",
            ClientId = "bridge_in_client",
            Exchange = localBroker.Exchange,
            Log = XTrace.Log,
        };
        bridge.Rules.Add(new MqttBridgeRule
        {
            RemoteTopic = "remote/#",
            Direction = BridgeDirection.In,
        });
        await bridge.StartAsync();

        try
        {
            // 在本地 Broker 订阅目标主题
            using var localSub = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{localPort}",
                ClientId = "bridge_local_sub",
                Timeout = 5000,
                Reconnect = false,
            };
            await localSub.ConnectAsync();
            var bridgeReceived = new TaskCompletionSource<String>();
            localSub.Received += (s, e) => bridgeReceived.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
            await localSub.SubscribeAsync(remoteTopic);
            await Task.Delay(300);

            // 在远端 Broker 发布消息，Bridge 订阅并转发到本地
            using var remotePub = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{remotePort}",
                ClientId = "bridge_remote_pub",
                Timeout = 5000,
                Reconnect = false,
            };
            await remotePub.ConnectAsync();
            var bridgePayload = "in_bridge_msg_" + Rand.NextString(4);
            await remotePub.PublishAsync(remoteTopic, bridgePayload, QualityOfService.AtMostOnce);

            // 等待本地订阅者收到桥接转发的消息
            var w = await Task.WhenAny(bridgeReceived.Task, Task.Delay(6000));
            Assert.True(w == bridgeReceived.Task, "6 秒内本地 Broker 订阅者未收到桥接消息");
            Assert.Equal(bridgePayload, bridgeReceived.Task.Result);

            await remotePub.DisconnectAsync();
            await localSub.DisconnectAsync();
        }
        finally
        {
            await bridge.StopAsync();
            bridge.TryDispose();
            localBroker.TryDispose();
            remoteBroker.TryDispose();
        }
    }
    #endregion
}

#region 辅助：发布 ACL 认证器（拒绝 restricted/* 主题的发布）
/// <summary>仅阻止向 restricted/* 发布的认证器，订阅和连接均放行</summary>
file sealed class PublishBlockAuthenticator : IMqttAuthenticator
{
    /// <inheritdoc/>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password) => ConnectReturnCode.Accepted;

    /// <inheritdoc/>
    public Boolean AuthorizePublish(String? clientId, String topic) => !topic.StartsWith("restricted/");

    /// <inheritdoc/>
    public Boolean AuthorizeSubscribe(String? clientId, String topic) => true;
}
#endregion
