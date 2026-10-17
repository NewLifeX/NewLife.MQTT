using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Sessions;

/// <summary>InflightManager 飞行窗口管理器单元测试</summary>
public class InflightManagerTests : IDisposable
{
    private readonly Object _syncRoot = new();
    private readonly List<PublishMessage> _sentMessages = [];
    private readonly InflightManager _manager;
    private Int32 _sendCount;

    public InflightManagerTests()
    {
        _manager = new InflightManager(msg =>
        {
            lock (_syncRoot) _sentMessages.Add(msg);
            Interlocked.Increment(ref _sendCount);
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
    public void DefaultCheckInterval()
    {
        Assert.Equal(1_000, _manager.CheckInterval);
    }

    [Fact]
    public async Task Retry_ResendsMessageWithDuplicateFlag()
    {
        _manager.RetryTimeout = 10;
        _manager.CheckInterval = 10;
        // 足够大的上限，确保测试线程被调度前不会因重试次数耗尽而移除消息
        _manager.MaxRetries = 100;

        var msg = new PublishMessage { Topic = "test", QoS = QualityOfService.AtLeastOnce };
        _manager.Add(1, msg);

        // 定时器线程在并行测试下可能被延迟调度，轮询上限放宽，命中后立即返回
        var deadline = DateTime.Now.AddSeconds(10);
        while (Volatile.Read(ref _sendCount) == 0 && DateTime.Now < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(Volatile.Read(ref _sendCount) > 0);

        // 确认后停止后续重试，再安全读取发送记录
        Assert.True(_manager.Acknowledge(1));
        List<PublishMessage> sent;
        lock (_syncRoot) sent = [.. _sentMessages];
        Assert.Contains(msg, sent);
        Assert.True(msg.Duplicate);
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
