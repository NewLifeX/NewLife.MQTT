using System;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>PersistentSession 持久化会话单元测试</summary>
public class PersistentSessionTests
{
    [Fact]
    public void DefaultValues()
    {
        var session = new PersistentSession();

        Assert.NotNull(session.Subscriptions);
        Assert.Empty(session.Subscriptions);
        Assert.NotNull(session.OfflineMessages);
        Assert.True(session.OfflineMessages.IsEmpty);
        Assert.True(session.CreateTime <= DateTime.Now);
        Assert.True(session.UpdateTime <= DateTime.Now);
    }

    [Fact]
    public void SetClientId()
    {
        var session = new PersistentSession { ClientId = "test-client" };

        Assert.Equal("test-client", session.ClientId);
    }

    [Fact]
    public void SetSessionId()
    {
        var session = new PersistentSession { SessionId = 42 };

        Assert.Equal(42, session.SessionId);
    }

    [Fact]
    public void AddSubscription()
    {
        var session = new PersistentSession();
        session.Subscriptions["test/topic"] = QualityOfService.AtLeastOnce;

        Assert.Single(session.Subscriptions);
        Assert.Equal(QualityOfService.AtLeastOnce, session.Subscriptions["test/topic"]);
    }

    [Fact]
    public void EnqueueOfflineMessage()
    {
        var session = new PersistentSession();
        var msg = new PublishMessage { Topic = "test" };
        session.OfflineMessages.Enqueue(msg);

        Assert.Equal(1, session.OfflineMessages.Count);
        Assert.True(session.OfflineMessages.TryDequeue(out var result));
        Assert.Same(msg, result);
    }

    [Fact]
    public void MultipleSubscriptions()
    {
        var session = new PersistentSession();
        session.Subscriptions["topic/a"] = QualityOfService.AtMostOnce;
        session.Subscriptions["topic/b"] = QualityOfService.AtLeastOnce;
        session.Subscriptions["topic/c"] = QualityOfService.ExactlyOnce;

        Assert.Equal(3, session.Subscriptions.Count);
    }

    [Fact]
    public void OfflineMessages_FifoOrder()
    {
        var session = new PersistentSession();
        var msg1 = new PublishMessage { Topic = "topic1" };
        var msg2 = new PublishMessage { Topic = "topic2" };
        session.OfflineMessages.Enqueue(msg1);
        session.OfflineMessages.Enqueue(msg2);

        session.OfflineMessages.TryDequeue(out var first);
        session.OfflineMessages.TryDequeue(out var second);

        Assert.Equal("topic1", first?.Topic);
        Assert.Equal("topic2", second?.Topic);
    }
}
