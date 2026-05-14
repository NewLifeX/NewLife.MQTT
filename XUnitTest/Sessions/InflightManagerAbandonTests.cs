using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Sessions;

/// <summary>InflightManager 消息放弃回调（F046 ReceiveMaximum 修正）单元测试</summary>
public class InflightManagerAbandonTests : IDisposable
{
    private readonly List<PublishMessage> _sentMessages = [];
    private readonly InflightManager _manager;

    public InflightManagerAbandonTests()
    {
        _manager = new InflightManager(msg =>
        {
            _sentMessages.Add(msg);
            return Task.CompletedTask;
        })
        {
            RetryTimeout = 50,   // 50ms 超时，加速测试
            MaxRetries = 1,      // 最多重试 1 次（两次定时器触发后 abandon）
        };
    }

    public void Dispose() => _manager.TryDispose();

    [Fact]
    [DisplayName("InflightManager 放弃消息时触发 OnMessageAbandoned 回调")]
    public async Task OnMessageAbandoned_IsCalled_WhenMaxRetriesExceeded()
    {
        var abandonedIds = new List<UInt16>();
        _manager.OnMessageAbandoned = id => abandonedIds.Add(id);

        var msg = new PublishMessage { Topic = "test", QoS = QualityOfService.AtLeastOnce };
        _manager.Add(1, msg);

        // 等待超时 + 最大重试次数到达
        // CheckRetry 定时器每 1000ms 触发一次；MaxRetries=1 需要两个周期：
        //   t=1000ms: 第一次超时，RetryCount=0 → 重发，RetryCount=1
        //   t=2000ms: 第二次超时，RetryCount=1 >= MaxRetries=1 → 放弃
        await Task.Delay(2500);

        Assert.Contains((UInt16)1, abandonedIds);
    }

    [Fact]
    [DisplayName("InflightManager 正常 ACK 后不触发 OnMessageAbandoned")]
    public async Task OnMessageAbandoned_NotCalled_WhenAcknowledged()
    {
        var abandonedIds = new List<UInt16>();
        _manager.OnMessageAbandoned = id => abandonedIds.Add(id);

        var msg = new PublishMessage { Topic = "test", QoS = QualityOfService.AtLeastOnce };
        _manager.Add(2, msg);

        // 正常 ACK，不等超时
        _manager.Acknowledge(2);

        // 等待 2 个定时器周期，确认没有 abandon 回调
        await Task.Delay(2200);

        Assert.Empty(abandonedIds);
    }

    [Fact]
    [DisplayName("OnMessageAbandoned 未设置时不抛出异常")]
    public async Task OnMessageAbandoned_NotSet_NoException()
    {
        // 不设置 OnMessageAbandoned
        var msg = new PublishMessage { Topic = "test", QoS = QualityOfService.AtLeastOnce };
        _manager.Add(3, msg);

        // 等待超时并放弃
        await Task.Delay(2500);

        // 无异常即通过
    }
}
