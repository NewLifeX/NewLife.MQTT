using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttExchange 消息交换机单元测试</summary>
public class MqttExchangeTests : IDisposable
{
    private readonly MqttExchange _exchange;

    public MqttExchangeTests()
    {
        _exchange = new MqttExchange { EnableSysTopic = false };
    }

    public void Dispose() => _exchange.TryDispose();

    #region 会话管理
    [Fact]
    public void Add_NewSession_ReturnsTrue()
    {
        var handler = new TestMqttHandler();

        var result = _exchange.Add(1, handler);

        Assert.True(result);
    }

    [Fact]
    public void Add_DuplicateSession_ReturnsFalse()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        var result = _exchange.Add(1, handler);

        Assert.False(result);
    }

    [Fact]
    public void TryGetValue_ExistingSession_ReturnsTrue()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        var result = _exchange.TryGetValue(1, out var session);

        Assert.True(result);
        Assert.Same(handler, session);
    }

    [Fact]
    public void TryGetValue_NonExistingSession_ReturnsFalse()
    {
        var result = _exchange.TryGetValue(999, out var session);

        Assert.False(result);
    }

    [Fact]
    public void Remove_ExistingSession_ReturnsTrue()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        var result = _exchange.Remove(1);

        Assert.True(result);
        Assert.False(_exchange.TryGetValue(1, out _));
    }

    [Fact]
    public void Remove_NonExistingSession_ReturnsFalse()
    {
        var result = _exchange.Remove(999);

        Assert.False(result);
    }

    [Fact]
    public void Add_IncrementsConnectedClients()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        Assert.Equal(1, _exchange.Stats.ConnectedClients);
        Assert.Equal(1, _exchange.Stats.TotalConnections);
    }

    [Fact]
    public void Remove_DecrementsConnectedClients()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Remove(1);

        Assert.Equal(0, _exchange.Stats.ConnectedClients);
    }

    [Fact]
    public void RegisterClientId_StoresMapping()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.RegisterClientId(1, "client1");

        var clientIds = _exchange.GetClientIds();

        Assert.Contains("client1", clientIds);
    }
    #endregion

    #region 订阅管理
    [Fact]
    public void Subscribe_CreatesTopicEntry()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtLeastOnce);

        var topics = _exchange.GetTopics();

        Assert.Contains("test/topic", topics);
    }

    [Fact]
    public void Subscribe_CountIncreases()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtLeastOnce);

        Assert.Equal(1, _exchange.GetSubscriberCount("test/topic"));
    }

    [Fact]
    public void Subscribe_MultipleSubscribers()
    {
        var handler1 = new TestMqttHandler();
        var handler2 = new TestMqttHandler();
        _exchange.Add(1, handler1);
        _exchange.Add(2, handler2);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtLeastOnce);
        _exchange.Subscribe(2, "test/topic", QualityOfService.AtMostOnce);

        Assert.Equal(2, _exchange.GetSubscriberCount("test/topic"));
    }

    [Fact]
    public void Unsubscribe_RemovesSubscription()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtLeastOnce);
        _exchange.Unsubscribe(1, "test/topic");

        Assert.Equal(0, _exchange.GetSubscriberCount("test/topic"));
    }

    [Fact]
    public void Unsubscribe_RemovesEmptyTopic()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtLeastOnce);
        _exchange.Unsubscribe(1, "test/topic");

        Assert.DoesNotContain("test/topic", _exchange.GetTopics());
    }
    #endregion

    #region 发布消息
    [Fact]
    public void Publish_DeliversToSubscriber()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtMostOnce);

        var msg = new PublishMessage
        {
            Topic = "test/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
        };
        _exchange.Publish(msg);

        Assert.Single(handler.PublishedMessages);
    }

    [Fact]
    public void Publish_WildcardSubscription()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/#", QualityOfService.AtMostOnce);

        var msg = new PublishMessage
        {
            Topic = "test/sub/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
        };
        _exchange.Publish(msg);

        Assert.Single(handler.PublishedMessages);
    }

    [Fact]
    public void Publish_NoLocal_DoesNotDeliverToSelf()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtMostOnce, noLocal: true, retainAsPublished: false, retainHandling: 0);

        var msg = new PublishMessage
        {
            Topic = "test/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
        };
        _exchange.Publish(msg, 1); // 发布者会话ID=1

        Assert.Empty(handler.PublishedMessages);
    }

    [Fact]
    public void Publish_NoLocal_DeliversToOthers()
    {
        var handler1 = new TestMqttHandler();
        var handler2 = new TestMqttHandler();
        _exchange.Add(1, handler1);
        _exchange.Add(2, handler2);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtMostOnce, noLocal: true, retainAsPublished: false, retainHandling: 0);
        _exchange.Subscribe(2, "test/topic", QualityOfService.AtMostOnce);

        var msg = new PublishMessage
        {
            Topic = "test/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
        };
        _exchange.Publish(msg, 1);

        Assert.Empty(handler1.PublishedMessages);
        Assert.Single(handler2.PublishedMessages);
    }

    [Fact]
    public void Publish_IncrementsStats()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtMostOnce);

        var msg = new PublishMessage
        {
            Topic = "test/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
        };
        _exchange.Publish(msg);

        Assert.Equal(1, _exchange.Stats.MessagesReceived);
        Assert.Equal(1, _exchange.Stats.MessagesSent);
    }

    [Fact]
    public void Publish_SysTopicNotDistributed()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.Subscribe(1, "#", QualityOfService.AtMostOnce);

        var msg = new PublishMessage
        {
            Topic = "$SYS/broker/clients/connected",
            Payload = (ArrayPacket)"10"u8.ToArray(),
        };
        _exchange.Publish(msg);

        // $SYS 主题不分发给普通通配符订阅者
        Assert.Empty(handler.PublishedMessages);
    }
    #endregion

    #region 保留消息
    [Fact]
    public void Publish_RetainMessage_Stored()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        var msg = new PublishMessage
        {
            Topic = "test/retain",
            Payload = (ArrayPacket)"retained"u8.ToArray(),
            Retain = true,
        };
        _exchange.Publish(msg);

        var retains = _exchange.GetRetainMessages("test/retain");

        Assert.Single(retains);
    }

    [Fact]
    public void Publish_EmptyRetainMessage_ClearsRetain()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        // 先存储保留消息
        var msg = new PublishMessage
        {
            Topic = "test/retain",
            Payload = (ArrayPacket)"retained"u8.ToArray(),
            Retain = true,
        };
        _exchange.Publish(msg);

        // 空 payload 保留消息清除
        var clearMsg = new PublishMessage
        {
            Topic = "test/retain",
            Retain = true,
        };
        _exchange.Publish(clearMsg);

        var retains = _exchange.GetRetainMessages("test/retain");

        Assert.Empty(retains);
    }

    [Fact]
    public void GetRetainMessages_WildcardFilter()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        _exchange.Publish(new PublishMessage
        {
            Topic = "test/a",
            Payload = (ArrayPacket)"a"u8.ToArray(),
            Retain = true,
        });
        _exchange.Publish(new PublishMessage
        {
            Topic = "test/b",
            Payload = (ArrayPacket)"b"u8.ToArray(),
            Retain = true,
        });

        var retains = _exchange.GetRetainMessages("test/#");

        Assert.Equal(2, retains.Count);
    }

    [Fact]
    public void GetRetainedTopics_ReturnsAllRetainedTopics()
    {
        _exchange.Publish(new PublishMessage
        {
            Topic = "topic1",
            Payload = (ArrayPacket)"data1"u8.ToArray(),
            Retain = true,
        });
        _exchange.Publish(new PublishMessage
        {
            Topic = "topic2",
            Payload = (ArrayPacket)"data2"u8.ToArray(),
            Retain = true,
        });

        var topics = _exchange.GetRetainedTopics();

        Assert.Equal(2, topics.Count);
        Assert.Contains("topic1", topics);
        Assert.Contains("topic2", topics);
    }
    #endregion

    #region 持久会话
    [Fact]
    public void SavePersistentSession_StoresSession()
    {
        var subs = new Dictionary<String, QualityOfService>
        {
            ["test/topic"] = QualityOfService.AtLeastOnce,
        };

        _exchange.SavePersistentSession("client1", 1, subs);

        var session = _exchange.GetPersistentSessionInfo("client1");

        Assert.NotNull(session);
        Assert.Equal("client1", session.ClientId);
    }

    [Fact]
    public void SavePersistentSession_EmptyClientId_IsIgnored()
    {
        _exchange.SavePersistentSession("", 1, null);

        var session = _exchange.GetPersistentSessionInfo("");

        Assert.Null(session);
    }

    [Fact]
    public void ClearPersistentSession_RemovesSession()
    {
        _exchange.SavePersistentSession("client1", 1, null);
        _exchange.ClearPersistentSession("client1");

        var session = _exchange.GetPersistentSessionInfo("client1");

        Assert.Null(session);
    }

    [Fact]
    public void RestorePersistentSession_RestoresSubscriptions()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);

        var subs = new Dictionary<String, QualityOfService>
        {
            ["test/topic"] = QualityOfService.AtLeastOnce,
        };
        _exchange.SavePersistentSession("client1", 1, subs);

        // 新会话恢复
        var handler2 = new TestMqttHandler();
        _exchange.Add(2, handler2);
        var restored = _exchange.RestorePersistentSession("client1", 2);

        Assert.True(restored);
        Assert.Contains("test/topic", _exchange.GetTopics());
    }

    [Fact]
    public void RestorePersistentSession_NonExisting_ReturnsFalse()
    {
        var result = _exchange.RestorePersistentSession("nonexistent", 1);

        Assert.False(result);
    }

    [Fact]
    public void EnqueueOfflineMessage_StoresMessage()
    {
        _exchange.SavePersistentSession("client1", 1, null);

        var msg = new PublishMessage
        {
            Topic = "test/offline",
            Payload = (ArrayPacket)"offline"u8.ToArray(),
        };
        _exchange.EnqueueOfflineMessage("client1", msg);

        var session = _exchange.GetPersistentSessionInfo("client1");

        Assert.NotNull(session);
        Assert.Equal(1, session.OfflineMessages.Count);
    }

    [Fact]
    public void EnqueueOfflineMessage_EmptyClientId_IsIgnored()
    {
        var msg = new PublishMessage { Topic = "test" };
        _exchange.EnqueueOfflineMessage("", msg);

        // 不抛异常即可
    }

    [Fact]
    public void DeletePersistentSession_RemovesSession()
    {
        _exchange.SavePersistentSession("client1", 1, null);
        _exchange.DeletePersistentSession("client1");

        var session = _exchange.GetPersistentSessionInfo("client1");

        Assert.Null(session);
    }
    #endregion

    #region 管理查询
    [Fact]
    public void GetConnectedClients_ReturnsRegisteredClients()
    {
        var handler = new TestMqttHandler();
        _exchange.Add(1, handler);
        _exchange.RegisterClientId(1, "client1");

        var clients = _exchange.GetConnectedClients();

        Assert.Single(clients);
        Assert.Equal("client1", clients[0]);
    }

    [Fact]
    public void GetTopicSubscriptions_ReturnsTopicWithCounts()
    {
        var handler1 = new TestMqttHandler();
        var handler2 = new TestMqttHandler();
        _exchange.Add(1, handler1);
        _exchange.Add(2, handler2);
        _exchange.Subscribe(1, "test/topic", QualityOfService.AtMostOnce);
        _exchange.Subscribe(2, "test/topic", QualityOfService.AtLeastOnce);

        var subs = _exchange.GetTopicSubscriptions();

        Assert.True(subs.ContainsKey("test/topic"));
        Assert.Equal(2, subs["test/topic"]);
    }
    #endregion

    #region 共享订阅
    [Fact]
    public void Publish_SharedSubscription_RoundRobin()
    {
        var handler1 = new TestMqttHandler();
        var handler2 = new TestMqttHandler();
        _exchange.Add(1, handler1);
        _exchange.Add(2, handler2);
        _exchange.Subscribe(1, "$share/group1/test/topic", QualityOfService.AtMostOnce);
        _exchange.Subscribe(2, "$share/group1/test/topic", QualityOfService.AtMostOnce);

        // 发送两条消息，应轮询分发给不同订阅者
        for (var i = 0; i < 2; i++)
        {
            _exchange.Publish(new PublishMessage
            {
                Topic = "test/topic",
                Payload = (ArrayPacket)$"msg{i}".GetBytes(),
            });
        }

        // 两个订阅者合计收到2条消息
        var total = handler1.PublishedMessages.Count + handler2.PublishedMessages.Count;
        Assert.Equal(2, total);
    }
    #endregion

    #region 辅助类
    /// <summary>测试用 MqttHandler 实现</summary>
    private class TestMqttHandler : IMqttHandler
    {
        public List<PublishMessage> PublishedMessages { get; } = [];

        public MqttMessage? Process(MqttMessage message) => null;

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce) => Task.FromResult((MqttIdMessage?)null);

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce) => Task.FromResult((MqttIdMessage?)null);

        public Task<MqttIdMessage?> PublishAsync(PublishMessage message)
        {
            PublishedMessages.Add(message);
            return Task.FromResult((MqttIdMessage?)null);
        }

        public void Close(String reason) { }
    }
    #endregion
}
