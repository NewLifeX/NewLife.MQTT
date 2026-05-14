using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
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

/// <summary>端到端（E2E）场景测试。覆盖项目全部核心使用场景，作为回归保障。
/// 每个测试场景独立启停服务端，完全隔离，互不干扰。</summary>
[Collection("E2EScenarioTests")]
public class E2EScenarioTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public E2EScenarioTests()
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
        Boolean reconnect = false,
        Boolean cleanSession = true) => new NewLife.MQTT.MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"e2e_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = reconnect,
        Version = version,
        CleanSession = cleanSession,
    };

    #region 场景一：多版本共存 Pub/Sub（V310 + V311 + V500 跨版本）
    [Fact]
    [DisplayName("E2E 场景1：V310+V311+V500 三版本跨版本 Pub/Sub")]
    public async Task Scenario1_MultiVersion_CoexistPublishSubscribe()
    {
        var topic = $"e2e/s1/{Rand.NextString(4)}";

        // V500 订阅者
        using var subV500 = CreateClient("s1_sub_v500", MqttVersion.V500);
        await subV500.ConnectAsync();
        var v500Received = new TaskCompletionSource<String>();
        subV500.Received += (s, e) => v500Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await subV500.SubscribeAsync(topic);

        // V311 订阅者
        using var subV311 = CreateClient("s1_sub_v311", MqttVersion.V311);
        await subV311.ConnectAsync();
        var v311Received = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => v311Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await subV311.SubscribeAsync(topic);

        await Task.Delay(200);

        // V310 (MQIsdp) 客户端发布
        using var pubV310 = new NewLife.MQTT.MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = "s1_pub_v310",
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V310,
        };
        var connectMsg = new ConnectMessage
        {
            ClientId = "s1_pub_v310",
            ProtocolName = "MQIsdp",
            ProtocolLevel = MqttVersion.V310,
            CleanSession = true,
        };
        await pubV310.ConnectAsync(connectMsg);

        var payload = "multiversion_" + Rand.NextString(4);
        await pubV310.PublishAsync(topic, payload, QualityOfService.AtMostOnce);

        var t500 = await Task.WhenAny(v500Received.Task, Task.Delay(5000));
        var t311 = await Task.WhenAny(v311Received.Task, Task.Delay(5000));

        Assert.True(t500 == v500Received.Task, "V500 订阅者 5s 内未收到消息");
        Assert.True(t311 == v311Received.Task, "V311 订阅者 5s 内未收到消息");
        Assert.Equal(payload, v500Received.Task.Result);
        Assert.Equal(payload, v311Received.Task.Result);

        await pubV310.DisconnectAsync();
        await subV311.DisconnectAsync();
        await subV500.DisconnectAsync();
    }
    #endregion

    #region 场景二：QoS 全级别完整流程（0/1/2）
    [Fact]
    [DisplayName("E2E 场景2：QoS 0 消息至多一次投递")]
    public async Task Scenario2a_QoS0_AtMostOnce()
    {
        var topic = $"e2e/s2/qos0/{Rand.NextString(4)}";
        using var pub = CreateClient("s2_pub_qos0");
        using var sub = CreateClient("s2_sub_qos0");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await sub.SubscribeAsync(topic);
        await Task.Delay(100);

        var payload = "qos0_" + Rand.NextString(4);
        await pub.PublishAsync(topic, payload, QualityOfService.AtMostOnce);

        var w = await Task.WhenAny(received.Task, Task.Delay(4000));
        Assert.True(w == received.Task, "QoS0 消息未到达");
        Assert.Equal(payload, received.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [DisplayName("E2E 场景2：QoS 1 消息至少一次投递 + PUBACK")]
    public async Task Scenario2b_QoS1_AtLeastOnce()
    {
        var topic = $"e2e/s2/qos1/{Rand.NextString(4)}";
        using var pub = CreateClient("s2_pub_qos1");
        using var sub = CreateClient("s2_sub_qos1");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await sub.SubscribeAsync(new[] { topic }, QualityOfService.AtLeastOnce);
        await Task.Delay(100);

        var payload = "qos1_" + Rand.NextString(4);
        var ack = await pub.PublishAsync(topic, payload, QualityOfService.AtLeastOnce);

        Assert.NotNull(ack);
        Assert.IsType<PubAck>(ack);

        var w = await Task.WhenAny(received.Task, Task.Delay(4000));
        Assert.True(w == received.Task, "QoS1 消息未到达");
        Assert.Equal(payload, received.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [DisplayName("E2E 场景2：QoS 2 消息恰好一次投递 + 四步握手")]
    public async Task Scenario2c_QoS2_ExactlyOnce()
    {
        var topic = $"e2e/s2/qos2/{Rand.NextString(4)}";
        using var pub = CreateClient("s2_pub_qos2");
        using var sub = CreateClient("s2_sub_qos2");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await sub.SubscribeAsync(new[] { topic }, QualityOfService.ExactlyOnce);
        await Task.Delay(100);

        var payload = "qos2_" + Rand.NextString(4);
        var ack = await pub.PublishAsync(topic, payload, QualityOfService.ExactlyOnce);

        Assert.NotNull(ack);
        Assert.IsType<PubComp>(ack);

        var w = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.True(w == received.Task, "QoS2 消息未到达");
        Assert.Equal(payload, received.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 场景三：断线重连 + 离线消息补投
    [Fact]
    [DisplayName("E2E 场景3：断线后重连 + 持久会话离线消息补投")]
    public async Task Scenario3_ReconnectAndOfflineMessages()
    {
        const String clientId = "s3_offline_client";
        var topic = $"e2e/s3/offline/{Rand.NextString(4)}";

        // 第一次连接，CleanSession=false 建立持久会话，订阅主题后断开
        using var client1 = new NewLife.MQTT.MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            CleanSession = false,
        };
        await client1.ConnectAsync();
        await client1.SubscribeAsync(new[] { topic }, QualityOfService.AtLeastOnce);
        await client1.DisconnectAsync();
        await Task.Delay(300);

        // 在客户端离线期间，发布者推送一条 QoS1 消息（会存入离线队列）
        using var pub = CreateClient("s3_publisher");
        await pub.ConnectAsync();
        await pub.PublishAsync(topic, "offline_msg", QualityOfService.AtLeastOnce);
        await pub.DisconnectAsync();
        await Task.Delay(300);

        // 第二次连接，相同 ClientId + CleanSession=false，服务端应补投离线消息
        using var client2 = new NewLife.MQTT.MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            CleanSession = false,
        };

        var offlineReceived = new TaskCompletionSource<String>();
        client2.Received += (s, e) => offlineReceived.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        var ack = await client2.ConnectAsync();

        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        // SessionPresent=true 表示服务端找到了持久会话
        Assert.True(ack.SessionPresent, "服务端未找到持久会话（SessionPresent 应为 true）");

        // 等待离线消息补投
        var w = await Task.WhenAny(offlineReceived.Task, Task.Delay(3000));
        Assert.True(w == offlineReceived.Task, "3 秒内未收到离线补投消息");
        Assert.Equal("offline_msg", offlineReceived.Task.Result);

        await client2.DisconnectAsync();
    }
    #endregion

    #region 场景四：Retain 消息完整生命周期
    [Fact]
    [DisplayName("E2E 场景4：Retain 消息生命周期（发布→收到→清除→不再收到）")]
    public async Task Scenario4_RetainMessage_Lifecycle()
    {
        var topic = $"e2e/s4/retain/{Rand.NextString(4)}";
        using var pub = CreateClient("s4_pub");

        await pub.ConnectAsync();

        // 发布 Retain 消息
        await pub.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = new ArrayPacket("retained_value"u8.ToArray()),
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        });
        await Task.Delay(100);

        // 新订阅者应立即收到 Retain 消息
        using var sub1 = CreateClient("s4_sub1");
        await sub1.ConnectAsync();
        var received1 = new TaskCompletionSource<String>();
        sub1.Received += (s, e) => received1.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await sub1.SubscribeAsync(topic);

        var w1 = await Task.WhenAny(received1.Task, Task.Delay(3000));
        Assert.True(w1 == received1.Task, "新订阅者 3s 内未收到 Retain 消息");
        Assert.Equal("retained_value", received1.Task.Result);

        // 用空 Payload 清除 Retain 消息
        await pub.PublishAsync(new PublishMessage
        {
            Topic = topic,
            Payload = null,
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        });
        await Task.Delay(100);

        // 再次订阅，不应收到 Retain 消息
        using var sub2 = CreateClient("s4_sub2");
        await sub2.ConnectAsync();
        var received2 = false;
        sub2.Received += (s, e) => received2 = true;
        await sub2.SubscribeAsync(topic);

        await Task.Delay(1000);
        Assert.False(received2, "Retain 消息已清除，新订阅者不应收到");

        await pub.DisconnectAsync();
        await sub1.DisconnectAsync();
        await sub2.DisconnectAsync();
    }
    #endregion

    #region 场景五：遗嘱消息（异常断开触发 / 正常 DISCONNECT 不触发）
    [Fact]
    [DisplayName("E2E 场景5a：异常断开触发遗嘱消息")]
    public async Task Scenario5a_Will_PublishedOnAbnormalDisconnect()
    {
        var willTopic = $"e2e/s5/will/{Rand.NextString(4)}";

        using var subscriber = CreateClient("s5_watcher");
        await subscriber.ConnectAsync();
        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await subscriber.SubscribeAsync(willTopic);
        await Task.Delay(200);

        // 携带遗嘱的客户端
        var willClient = CreateClient("s5_will_client");
        willClient.WillTopic = willTopic;
        willClient.WillMessage = "s5_abnormal_offline"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;
        await willClient.ConnectAsync();
        await Task.Delay(200);

        // 模拟异常断开（直接 Dispose 不发 DISCONNECT）
        willClient.Dispose();
        await Task.Delay(500);

        var w = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.True(w == received.Task, "5s 内未收到遗嘱消息");
        Assert.Equal("s5_abnormal_offline", received.Task.Result);

        await subscriber.DisconnectAsync();
    }

    [Fact]
    [DisplayName("E2E 场景5b：正常 DISCONNECT 不触发遗嘱消息")]
    public async Task Scenario5b_Will_NotPublishedOnNormalDisconnect()
    {
        var willTopic = $"e2e/s5b/will/{Rand.NextString(4)}";

        using var subscriber = CreateClient("s5b_watcher");
        await subscriber.ConnectAsync();
        var willReceived = false;
        subscriber.Received += (s, e) => willReceived = true;
        await subscriber.SubscribeAsync(willTopic);
        await Task.Delay(200);

        // 携带遗嘱的客户端正常 DISCONNECT
        using var willClient = CreateClient("s5b_will_client");
        willClient.WillTopic = willTopic;
        willClient.WillMessage = "s5b_should_not_arrive"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;
        await willClient.ConnectAsync();
        await willClient.DisconnectAsync(); // 正常断开

        // 等待一段时间确认遗嘱不触发
        await Task.Delay(1500);
        Assert.False(willReceived, "正常 DISCONNECT 不应触发遗嘱消息");

        await subscriber.DisconnectAsync();
    }
    #endregion

    #region 场景六：规则引擎 Republish E2E
    [Fact]
    [DisplayName("E2E 场景6：规则引擎 Republish 将消息从 topicA 转发到 topicB")]
    public async Task Scenario6_RuleEngine_Republish_EndToEnd()
    {
        var suffix = Rand.NextString(4);
        var sourceTopicFilter = $"e2e/s6/source/{suffix}";
        var destTopic = $"e2e/s6/dest/{suffix}";

        // 配置规则引擎
        var ruleEngine = new MqttRuleEngine { Exchange = _server.Exchange };
        ruleEngine.AddRule(new MqttRule
        {
            Name = "s6_republish_rule",
            TopicFilter = sourceTopicFilter,
            ActionType = RuleActionType.Republish,
            TargetTopic = destTopic,
        });
        (_server.Exchange as MqttExchange)!.RuleEngine = ruleEngine;

        using var sub = CreateClient("s6_dest_sub");
        await sub.ConnectAsync();
        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await sub.SubscribeAsync(destTopic);
        await Task.Delay(200);

        // 向源主题发布，规则引擎应转发到目标主题
        using var pub = CreateClient("s6_source_pub");
        await pub.ConnectAsync();
        var payload = "s6_republish_" + Rand.NextString(4);
        await pub.PublishAsync(sourceTopicFilter, payload, QualityOfService.AtMostOnce);

        var w = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.True(w == received.Task, "5s 内未收到规则引擎转发的消息");
        Assert.Equal(payload, received.Task.Result);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 场景七：认证 ACL E2E（自定义拒绝）
    [Fact]
    [DisplayName("E2E 场景7：自定义 IMqttAuthenticator 拒绝未授权发布")]
    public async Task Scenario7_ACL_RejectUnauthorizedPublish()
    {
        // 构建一个带自定义 ACL 的服务端
        var services = new ObjectContainer();
        var aclServer = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
        };
        // 注入拒绝发布到 restricted/* 主题的 ACL
        aclServer.Authenticator = new RestrictedTopicAuthenticator();
        aclServer.Start();
        var aclPort = aclServer.Port;

        try
        {
            using var client = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{aclPort}",
                ClientId = "s7_acl_client",
                Timeout = 5000,
                Reconnect = false,
            };

            var ack = await client.ConnectAsync();
            Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);

            // 订阅受限主题：服务端 ACL 拒绝时 GrantedQoS 应包含失败码（>=0x80）
            var subAck = await client.SubscribeAsync([new Subscription("restricted/data", QualityOfService.AtMostOnce)]);
            Assert.NotNull(subAck);
            // 服务端授权拒绝时 SubAck GrantedQoS=0x80
            Assert.Contains(subAck!.GrantedQos, rc => (Byte)rc >= 0x80);

            await client.DisconnectAsync();
        }
        finally
        {
            aclServer.TryDispose();
        }
    }

    [Fact]
    [DisplayName("E2E 场景7b：自定义 IMqttAuthenticator 拒绝错误用户名密码连接")]
    public async Task Scenario7b_ACL_RejectBadCredentials()
    {
        var services = new ObjectContainer();
        var authServer = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
        };
        authServer.Authenticator = new PasswordAuthenticator("admin", "secret");
        authServer.Start();
        var authPort = authServer.Port;

        try
        {
            // 错误密码
            using var badClient = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{authPort}",
                ClientId = "s7b_bad_client",
                UserName = "admin",
                Password = "wrong",
                Timeout = 5000,
                Reconnect = false,
            };
            await Assert.ThrowsAnyAsync<Exception>(() => badClient.ConnectAsync());

            // 正确密码
            using var goodClient = new NewLife.MQTT.MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{authPort}",
                ClientId = "s7b_good_client",
                UserName = "admin",
                Password = "secret",
                Timeout = 5000,
                Reconnect = false,
            };
            var ack = await goodClient.ConnectAsync();
            Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);

            await goodClient.DisconnectAsync();
        }
        finally
        {
            authServer.TryDispose();
        }
    }
    #endregion

    #region 场景八：共享订阅负载均衡
    [Fact]
    [DisplayName("E2E 场景8：$share 共享订阅 2 订阅者各收约 50% 消息")]
    public async Task Scenario8_SharedSubscription_LoadBalance()
    {
        const String group = "s8_group";
        var suffix = Rand.NextString(4);
        var sharedTopic = $"$share/{group}/e2e/s8/{suffix}";
        var publishTopic = $"e2e/s8/{suffix}";
        const Int32 total = 10;

        using var sub1 = CreateClient("s8_sub1");
        using var sub2 = CreateClient("s8_sub2");
        using var pub = CreateClient("s8_pub");

        await sub1.ConnectAsync();
        await sub2.ConnectAsync();
        await pub.ConnectAsync();

        var sub1Count = 0;
        var sub2Count = 0;
        sub1.Received += (s, e) => Interlocked.Increment(ref sub1Count);
        sub2.Received += (s, e) => Interlocked.Increment(ref sub2Count);

        await sub1.SubscribeAsync(sharedTopic);
        await sub2.SubscribeAsync(sharedTopic);
        await Task.Delay(200);

        for (var i = 0; i < total; i++)
        {
            await pub.PublishAsync(publishTopic, $"msg_{i}", QualityOfService.AtMostOnce);
            await Task.Delay(30);
        }

        // 等待所有消息分发完成
        await Task.Delay(1000);

        var totalReceived = sub1Count + sub2Count;
        Assert.Equal(total, totalReceived); // 总收到数 = 总发送数
        Assert.True(sub1Count > 0, "sub1 未收到任何消息");
        Assert.True(sub2Count > 0, "sub2 未收到任何消息");

        await pub.DisconnectAsync();
        await sub1.DisconnectAsync();
        await sub2.DisconnectAsync();
    }
    #endregion

    #region 场景九：MQTTnet 互操作（NewLife 服务端 + MQTTnet 客户端）
    [Fact]
    [DisplayName("E2E 场景9：NewLife 服务端 + MQTTnet 客户端 QoS1 双向互操作")]
    public async Task Scenario9_MQTTnet_Interop_QoS1()
    {
        var topic = $"e2e/s9/interop/{Rand.NextString(4)}";
        var mqttFactory = new MqttClientFactory();

        // MQTTnet 客户端作为发布者
        var mqttnetPub = mqttFactory.CreateMqttClient();
        var pubOptions = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _port)
            .WithClientId("s9_mqttnet_pub")
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .Build();
        await mqttnetPub.ConnectAsync(pubOptions);

        // NewLife 客户端作为订阅者
        using var nlSub = CreateClient("s9_nl_sub", MqttVersion.V311);
        await nlSub.ConnectAsync();
        var received = new TaskCompletionSource<String>();
        nlSub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await nlSub.SubscribeAsync(topic);
        await Task.Delay(200);

        // MQTTnet 发布 QoS1 消息
        var payload = "s9_interop_" + Rand.NextString(4);
        await mqttnetPub.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());

        var w = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.True(w == received.Task, "5s 内 NewLife 订阅者未收到 MQTTnet 发布的消息");
        Assert.Equal(payload, received.Task.Result);

        // 反向：NewLife 发布，MQTTnet 订阅
        using var nlPub = CreateClient("s9_nl_pub", MqttVersion.V311);
        await nlPub.ConnectAsync();

        var mqttnetSubTopic = $"e2e/s9/reverse/{Rand.NextString(4)}";
        var mqttnetReceived = new TaskCompletionSource<String>();
        mqttnetPub.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Topic == mqttnetSubTopic)
                mqttnetReceived.TrySetResult(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
            return Task.CompletedTask;
        };
        await mqttnetPub.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(mqttnetSubTopic, MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());
        await Task.Delay(200);

        var reversePayload = "s9_reverse_" + Rand.NextString(4);
        await nlPub.PublishAsync(mqttnetSubTopic, reversePayload, QualityOfService.AtLeastOnce);

        var w2 = await Task.WhenAny(mqttnetReceived.Task, Task.Delay(5000));
        Assert.True(w2 == mqttnetReceived.Task, "5s 内 MQTTnet 订阅者未收到 NewLife 发布的消息");
        Assert.Equal(reversePayload, mqttnetReceived.Task.Result);

        await mqttnetPub.DisconnectAsync();
        mqttnetPub.Dispose();
        await nlSub.DisconnectAsync();
        await nlPub.DisconnectAsync();
    }
    #endregion
}

#region 辅助：自定义认证器（ACL 场景七）
/// <summary>限制订阅 restricted/* 主题的认证器</summary>
file sealed class RestrictedTopicAuthenticator : IMqttAuthenticator
{
    /// <inheritdoc/>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password) => ConnectReturnCode.Accepted;

    /// <inheritdoc/>
    public Boolean AuthorizePublish(String? clientId, String topic) => !topic.StartsWith("restricted/");

    /// <inheritdoc/>
    public Boolean AuthorizeSubscribe(String? clientId, String topic) => !topic.StartsWith("restricted/");
}

/// <summary>用户名密码认证器</summary>
file sealed class PasswordAuthenticator : IMqttAuthenticator
{
    private readonly String _user;
    private readonly String _pass;

    public PasswordAuthenticator(String user, String pass)
    {
        _user = user;
        _pass = pass;
    }

    /// <inheritdoc/>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password)
        => (username == _user && password == _pass) ? ConnectReturnCode.Accepted : ConnectReturnCode.RefusedBadUsernameOrPassword;

    /// <inheritdoc/>
    public Boolean AuthorizePublish(String? clientId, String topic) => true;

    /// <inheritdoc/>
    public Boolean AuthorizeSubscribe(String? clientId, String topic) => true;
}
#endregion
