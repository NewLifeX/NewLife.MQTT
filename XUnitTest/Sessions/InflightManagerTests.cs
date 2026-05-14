using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>InflightManager 飞行窗口管理器单元测试</summary>
public class InflightManagerTests : IDisposable
{
    private readonly List<PublishMessage> _sentMessages = [];
    private readonly InflightManager _manager;

    public InflightManagerTests()
    {
        _manager = new InflightManager(msg =>
        {
            _sentMessages.Add(msg);
            return Task.CompletedTask;
        });
    }

    public void Dispose() => _manager.TryDispose();

    [Fact]
    public void Constructor_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new InflightManager(null!));
    }

    [Fact]
    public void Add_StoresMessage()
    {
        var msg = new PublishMessage { Topic = "test" };
        _manager.Add(1, msg);

        // 消息已添加，可以确认
        var result = _manager.Acknowledge(1);

        Assert.True(result);
    }

    [Fact]
    public void Acknowledge_ExistingMessage_ReturnsTrue()
    {
        var msg = new PublishMessage { Topic = "test" };
        _manager.Add(1, msg);

        Assert.True(_manager.Acknowledge(1));
    }

    [Fact]
    public void Acknowledge_NonExistingMessage_ReturnsFalse()
    {
        Assert.False(_manager.Acknowledge(999));
    }

    [Fact]
    public void Acknowledge_SameMessageTwice_SecondReturnsFalse()
    {
        var msg = new PublishMessage { Topic = "test" };
        _manager.Add(1, msg);

        Assert.True(_manager.Acknowledge(1));
        Assert.False(_manager.Acknowledge(1));
    }

    [Fact]
    public void DefaultRetryTimeout()
    {
        Assert.Equal(10_000, _manager.RetryTimeout);
    }

    [Fact]
    public void DefaultMaxRetries()
    {
        Assert.Equal(3, _manager.MaxRetries);
    }

    [Fact]
    public void Dispose_ClearsMessages()
    {
        var msg = new PublishMessage { Topic = "test" };
        _manager.Add(1, msg);

        _manager.Dispose();

        Assert.False(_manager.Acknowledge(1));
    }
}
