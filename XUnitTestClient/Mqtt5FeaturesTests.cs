using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Reflection;
using NewLife.Security;
using Xunit;

namespace XUnitTestClient;

/// <summary>MQTT 5.0 特性补充测试：消息过期、ServerKeepAlive、SASL/SCRAM-SHA-256、管理 API、ClusterDiscovery</summary>
public class Mqtt5FeaturesTests
{
    #region FakeHandler 辅助实现
    /// <summary>用于单元测试的轻量 MQTT 处理器，拦截 PublishAsync 以收集分发的消息</summary>
    private sealed class FakeHandler : IMqttHandler
    {
        private static Int32 _idCounter;
        private readonly Action<PublishMessage> _onPublish;

        public Int32 Id { get; } = Interlocked.Increment(ref _idCounter);

        public FakeHandler(Action<PublishMessage> onPublish) => _onPublish = onPublish;

        public MqttMessage? Process(MqttMessage message) => null;

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce) =>
            Task.FromResult<MqttIdMessage?>(null);

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce) =>
            Task.FromResult<MqttIdMessage?>(null);

        public Task<MqttIdMessage?> PublishAsync(PublishMessage message)
        {
            _onPublish(message);
            return Task.FromResult<MqttIdMessage?>(null);
        }

        public void Close(String reason) { }
    }
    #endregion

    #region 消息过期（F025 MessageExpiryInterval）
    [Fact]
    [System.ComponentModel.DisplayName("消息过期：过期消息不分发给订阅者")]
    public void MessageExpiry_ExpiredMessageNotDelivered()
    {
        var exchange = new MqttExchange();

        var msg = new PublishMessage
        {
            Topic = "test/expiry",
            Payload = new ArrayPacket("hello".GetBytes()),
            ReceivedAt = DateTime.Now.AddSeconds(-3),
            Properties = new MqttProperties(),
        };
        msg.Properties.SetUInt32(MqttPropertyId.MessageExpiryInterval, 1);

        var received = new List<PublishMessage>();
        var handler = new FakeHandler(m => received.Add(m));
        exchange.Add(handler.Id, handler);
        exchange.Subscribe(handler.Id, "test/expiry", QualityOfService.AtMostOnce);

        exchange.Publish(msg);

        Assert.Empty(received);
    }

    [Fact]
    [System.ComponentModel.DisplayName("消息过期：未过期消息正常分发")]
    public void MessageExpiry_FreshMessageDelivered()
    {
        var exchange = new MqttExchange();

        var msg = new PublishMessage
        {
            Topic = "test/fresh",
            Payload = new ArrayPacket("hello".GetBytes()),
            ReceivedAt = DateTime.Now,
            Properties = new MqttProperties(),
        };
        msg.Properties.SetUInt32(MqttPropertyId.MessageExpiryInterval, 60);

        var received = new List<PublishMessage>();
        var handler = new FakeHandler(m => received.Add(m));
        exchange.Add(handler.Id, handler);
        exchange.Subscribe(handler.Id, "test/fresh", QualityOfService.AtMostOnce);

        exchange.Publish(msg);

        Assert.Single(received);
    }

    [Fact]
    [System.ComponentModel.DisplayName("消息过期：无过期属性时始终分发")]
    public void MessageExpiry_NoPropertyAlwaysDelivered()
    {
        var exchange = new MqttExchange();

        var msg = new PublishMessage
        {
            Topic = "test/noexpiry",
            Payload = new ArrayPacket("hello".GetBytes()),
        };

        var received = new List<PublishMessage>();
        var handler = new FakeHandler(m => received.Add(m));
        exchange.Add(handler.Id, handler);
        exchange.Subscribe(handler.Id, "test/noexpiry", QualityOfService.AtMostOnce);

        exchange.Publish(msg);

        Assert.Single(received);
    }

    [Fact]
    [System.ComponentModel.DisplayName("消息过期：过期 Retain 消息在新订阅时不推送")]
    public void MessageExpiry_ExpiredRetainNotSentOnSubscribe()
    {
        var exchange = new MqttExchange();

        var retainMsg = new PublishMessage
        {
            Topic = "test/retain-expiry",
            Payload = new ArrayPacket("stale".GetBytes()),
            Retain = true,
            ReceivedAt = DateTime.Now.AddSeconds(-10),
            Properties = new MqttProperties(),
        };
        retainMsg.Properties.SetUInt32(MqttPropertyId.MessageExpiryInterval, 5);
        exchange.Publish(retainMsg);

        var received = new List<PublishMessage>();
        var handler = new FakeHandler(m => received.Add(m));
        exchange.Add(handler.Id, handler);
        exchange.Subscribe(handler.Id, "test/retain-expiry", QualityOfService.AtMostOnce);

        Assert.Empty(received);
    }
    #endregion

    #region ServerKeepAlive（F026）
    [Fact]
    [System.ComponentModel.DisplayName("ServerKeepAlive：在 CONNACK 属性中正确设置")]
    public void ServerKeepAlive_SetInConnAckProperties()
    {
        var caps = new MqttSessionCapabilities();
        caps.ServerKeepAlive = 120;

        var props = caps.BuildConnAckProperties();

        Assert.NotNull(props);
        var ka = props.GetUInt16(MqttPropertyId.ServerKeepAlive);
        Assert.Equal((UInt16)120, ka);
    }

    [Fact]
    [System.ComponentModel.DisplayName("ServerKeepAlive：值为0时不写入属性")]
    public void ServerKeepAlive_ZeroNotWritten()
    {
        var caps = new MqttSessionCapabilities();
        caps.ServerKeepAlive = 0;

        var props = caps.BuildConnAckProperties();

        if (props != null)
        {
            var ka = props.GetUInt16(MqttPropertyId.ServerKeepAlive);
            Assert.Null(ka);
        }
    }

    [Fact]
    [System.ComponentModel.DisplayName("ServerKeepAlive：MqttExchange 属性可读写")]
    public void ServerKeepAlive_ExchangePropertyWorks()
    {
        var exchange = new MqttExchange { ServerKeepAlive = 60 };
        Assert.Equal((UInt16)60, exchange.ServerKeepAlive);
    }
    #endregion

    #region SASL / SCRAM-SHA-256（F027）
    private sealed class TestCredentialStore : IMqttSaslCredentialStore
    {
        private readonly Dictionary<String, String> _users;
        public TestCredentialStore(Dictionary<String, String> users) => _users = users;
        public String? GetPassword(String username) => _users.TryGetValue(username, out var p) ? p : null;
    }

    [Fact]
    [System.ComponentModel.DisplayName("SCRAM-SHA-256：正确密码完整握手成功")]
    public void ScramSha256_CorrectPassword_AuthSuccess()
    {
        var store = new TestCredentialStore(new Dictionary<String, String> { ["alice"] = "pa$$word" });
        var server = new ScramSha256Mechanism(store);

        var cnonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var clientFirstBare = $"n=alice,r={cnonce}";
        var clientFirst = $"n,,{clientFirstBare}";

        var step1 = server.Process(Encoding.UTF8.GetBytes(clientFirst));
        Assert.False(step1.IsComplete);
        Assert.NotNull(step1.ServerData);

        var serverFirst = Encoding.UTF8.GetString(step1.ServerData!);
        var pairs = ParsePairs(serverFirst);
        var serverNonce = pairs["r"];
        var saltB64 = pairs["s"];
        var iterations = Int32.Parse(pairs["i"]);

        Assert.StartsWith(cnonce, serverNonce);

        var salt = Convert.FromBase64String(saltB64);
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("pa$$word"),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        var withoutProof = $"c=biws,r={serverNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{withoutProof}";
        var authBytes = Encoding.UTF8.GetBytes(authMessage);

        var clientKey = HMACSHA256.HashData(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));
        var storedKey = SHA256.HashData(clientKey);
        var clientSig = HMACSHA256.HashData(storedKey, authBytes);

        var proof = new Byte[clientKey.Length];
        for (var i = 0; i < proof.Length; i++)
            proof[i] = (Byte)(clientKey[i] ^ clientSig[i]);

        var clientFinal = $"{withoutProof},p={Convert.ToBase64String(proof)}";

        var step2 = server.Process(Encoding.UTF8.GetBytes(clientFinal));
        Assert.True(step2.IsComplete);
        Assert.True(step2.Success);
        Assert.True(server.IsAuthenticated);
        Assert.Equal("alice", server.AuthenticatedUser);
        Assert.StartsWith("v=", Encoding.UTF8.GetString(step2.ServerData!));
    }

    [Fact]
    [System.ComponentModel.DisplayName("SCRAM-SHA-256：错误密码握手失败")]
    public void ScramSha256_WrongPassword_AuthFails()
    {
        var store = new TestCredentialStore(new Dictionary<String, String> { ["bob"] = "correct" });
        var server = new ScramSha256Mechanism(store);

        var cnonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var clientFirstBare = $"n=bob,r={cnonce}";
        var clientFirst = $"n,,{clientFirstBare}";

        var step1 = server.Process(Encoding.UTF8.GetBytes(clientFirst));
        Assert.False(step1.IsComplete);

        var serverFirst = Encoding.UTF8.GetString(step1.ServerData!);
        var pairs = ParsePairs(serverFirst);
        var serverNonce = pairs["r"];
        var salt = Convert.FromBase64String(pairs["s"]);
        var iterations = Int32.Parse(pairs["i"]);

        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("wrongpassword"),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        var withoutProof = $"c=biws,r={serverNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{withoutProof}";
        var authBytes = Encoding.UTF8.GetBytes(authMessage);

        var clientKey = HMACSHA256.HashData(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));
        var storedKey = SHA256.HashData(clientKey);
        var clientSig = HMACSHA256.HashData(storedKey, authBytes);

        var proof = new Byte[clientKey.Length];
        for (var i = 0; i < proof.Length; i++)
            proof[i] = (Byte)(clientKey[i] ^ clientSig[i]);

        var step2 = server.Process(Encoding.UTF8.GetBytes($"{withoutProof},p={Convert.ToBase64String(proof)}"));
        Assert.True(step2.IsComplete);
        Assert.False(step2.Success);
        Assert.False(server.IsAuthenticated);
    }

    [Fact]
    [System.ComponentModel.DisplayName("SCRAM-SHA-256：用户不存在时握手失败")]
    public void ScramSha256_UnknownUser_AuthFails()
    {
        var store = new TestCredentialStore(new Dictionary<String, String>());
        var server = new ScramSha256Mechanism(store);

        var cnonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var step1 = server.Process(Encoding.UTF8.GetBytes($"n,,n=ghost,r={cnonce}"));
        Assert.False(step1.IsComplete);

        var serverNonce = ParsePairs(Encoding.UTF8.GetString(step1.ServerData!))["r"];
        var clientFinal = $"c=biws,r={serverNonce},p={Convert.ToBase64String(new Byte[32])}";

        var step2 = server.Process(Encoding.UTF8.GetBytes(clientFinal));
        Assert.True(step2.IsComplete);
        Assert.False(step2.Success);
    }

    [Fact]
    [System.ComponentModel.DisplayName("SCRAM-SHA-256：机制名称正确")]
    public void ScramSha256_Name_IsCorrect()
    {
        var mech = new ScramSha256Mechanism(new TestCredentialStore([]));
        Assert.Equal("SCRAM-SHA-256", mech.Name);
    }

    [Fact]
    [System.ComponentModel.DisplayName("SCRAM-SHA-256：空 clientData 直接返回失败")]
    public void ScramSha256_NullClientData_ReturnsFail()
    {
        var mech = new ScramSha256Mechanism(new TestCredentialStore([]));

        var step = mech.Process(null);
        Assert.True(step.IsComplete);
        Assert.False(step.Success);
    }

    private static Dictionary<String, String> ParsePairs(String input)
    {
        var result = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var segment in input.Split(','))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
                result[segment[..eq]] = segment[(eq + 1)..];
        }
        return result;
    }
    #endregion

    #region HTTP 管理 API（F042）
    [Fact]
    [System.ComponentModel.DisplayName("MqttExchange：GetConnectedClients 返回已注册客户端")]
    public void Exchange_GetConnectedClients_ReturnsRegistered()
    {
        var exchange = new MqttExchange();

        var h1 = new FakeHandler(_ => { });
        var h2 = new FakeHandler(_ => { });
        exchange.Add(h1.Id, h1);
        exchange.RegisterClientId(h1.Id, "client-A");
        exchange.Add(h2.Id, h2);
        exchange.RegisterClientId(h2.Id, "client-B");

        var clients = exchange.GetConnectedClients();
        Assert.Contains("client-A", clients);
        Assert.Contains("client-B", clients);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttExchange：GetRetainedTopics 返回保留消息主题")]
    public void Exchange_GetRetainedTopics_ReturnsTopics()
    {
        var exchange = new MqttExchange();

        exchange.Publish(new PublishMessage
        {
            Topic = "sys/status",
            Payload = new ArrayPacket("online".GetBytes()),
            Retain = true,
        });

        var topics = exchange.GetRetainedTopics();
        Assert.Contains("sys/status", topics);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttExchange：GetTopicSubscriptions 返回订阅统计")]
    public void Exchange_GetTopicSubscriptions_ReturnsStats()
    {
        var exchange = new MqttExchange();

        var h1 = new FakeHandler(_ => { });
        var h2 = new FakeHandler(_ => { });
        exchange.Add(h1.Id, h1);
        exchange.Add(h2.Id, h2);
        exchange.Subscribe(h1.Id, "sensor/+", QualityOfService.AtMostOnce);
        exchange.Subscribe(h2.Id, "sensor/+", QualityOfService.AtMostOnce);

        var subs = exchange.GetTopicSubscriptions();
        Assert.True(subs.ContainsKey("sensor/+"));
        Assert.Equal(2, subs["sensor/+"]);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttManagementServer：正常启动和停止")]
    public void ManagementServer_StartStop_NoException()
    {
        var exchange = new MqttExchange();
        var server = new MqttManagementServer
        {
            Port = Rand.Next(35000, 36000),
            Exchange = exchange,
        };

        server.Start();
        server.Stop("test");
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttManagementController：Stats/Clients/Topics/Retained 接口均返回非空")]
    public void ManagementController_AllEndpoints_ReturnData()
    {
        var exchange = new MqttExchange();
        var mgmtServer = new MqttManagementServer
        {
            Port = Rand.Next(36000, 37000),
            Exchange = exchange,
        };
        mgmtServer.Start();

        try
        {
            var controller = new MqttManagementController();
            controller.SetValue("_server", mgmtServer);

            Assert.NotNull(controller.Stats());
            Assert.NotNull(controller.Clients());
            Assert.NotNull(controller.Topics());
            Assert.NotNull(controller.Retained());
        }
        finally
        {
            mgmtServer.Stop("test");
        }
    }
    #endregion

    #region ClusterDiscovery（F034）
    [Fact]
    [System.ComponentModel.DisplayName("ClusterDiscovery：启动后 DiscoveryPort 已赋值")]
    public void ClusterDiscovery_Start_BindsPort()
    {
        var clusterPort = Rand.Next(40000, 41000);
        var cluster = new NewLife.MQTT.Clusters.ClusterServer { Port = clusterPort };

        var discovery = new NewLife.MQTT.Clusters.ClusterDiscovery
        {
            Cluster = cluster,
            DiscoveryPort = clusterPort + 1,
            Log = XTrace.Log,
        };

        discovery.Start();
        try
        {
            Assert.Equal(clusterPort + 1, discovery.DiscoveryPort);
        }
        finally
        {
            discovery.Stop();
        }
    }

    [Fact]
    [System.ComponentModel.DisplayName("ClusterDiscovery：可安全多次 Stop")]
    public void ClusterDiscovery_MultipleStop_Safe()
    {
        var clusterPort = Rand.Next(41000, 42000);
        var cluster = new NewLife.MQTT.Clusters.ClusterServer { Port = clusterPort };

        var discovery = new NewLife.MQTT.Clusters.ClusterDiscovery
        {
            Cluster = cluster,
            DiscoveryPort = clusterPort + 1,
        };

        discovery.Start();
        discovery.Stop();
        discovery.Stop();
    }

    [Fact]
    [System.ComponentModel.DisplayName("ClusterDiscovery：默认 DiscoveryPort 为 ClusterPort+1")]
    public void ClusterDiscovery_DefaultDiscoveryPort_IsClusterPortPlusOne()
    {
        var clusterPort = Rand.Next(42000, 43000);
        var cluster = new NewLife.MQTT.Clusters.ClusterServer { Port = clusterPort };

        var discovery = new NewLife.MQTT.Clusters.ClusterDiscovery
        {
            Cluster = cluster,
        };

        discovery.Start();
        try
        {
            Assert.Equal(clusterPort + 1, discovery.DiscoveryPort);
        }
        finally
        {
            discovery.Stop();
        }
    }
    #endregion
}
