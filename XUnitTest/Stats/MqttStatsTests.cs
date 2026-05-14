using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Stats;

/// <summary>MqttStats 运行统计测试</summary>
public class MqttStatsTests
{
    [Fact]
    [System.ComponentModel.DisplayName("Stats：ConnectedClients Interlocked 递增线程安全")]
    public void ConnectedClientsIncrementIsThreadSafe()
    {
        var stats = new MqttStats();

        Parallel.For(0, 1000, _ =>
        {
            Interlocked.Increment(ref stats.ConnectedClients);
        });

        Assert.Equal(1000, stats.ConnectedClients);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：消息计数器多线程并发递增安全")]
    public void MessageCountersAreThreadSafe()
    {
        var stats = new MqttStats();

        Parallel.For(0, 500, _ =>
        {
            Interlocked.Increment(ref stats.MessagesReceived);
            Interlocked.Increment(ref stats.MessagesSent);
        });

        Assert.Equal(500, stats.MessagesReceived);
        Assert.Equal(500, stats.MessagesSent);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：CalculateRates 经过足够时间后速率大于 0")]
    public void CalculateRatesWorks()
    {
        var stats = new MqttStats();

        // 模拟一些消息
        stats.MessagesReceived = 100;
        stats.MessagesSent = 50;

        // 强制经过足够时间
        Thread.Sleep(600);

        stats.CalculateRates();

        // 速率应该大于0（100条消息 / ~0.6秒）
        Assert.True(stats.MessagesReceivedPerSecond > 0);
        Assert.True(stats.MessagesSentPerSecond > 0);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：Uptime 随时间增加")]
    public void UptimeIncreases()
    {
        var stats = new MqttStats();
        var uptime1 = stats.Uptime;

        Thread.Sleep(1100);

        var uptime2 = stats.Uptime;

        Assert.True(uptime2 > uptime1);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：MaxConnectedClients 降低连接后保持历史最大值")]
    public void MaxConnectedClientsTracking()
    {
        var stats = new MqttStats();

        // 模拟连接高峰
        stats.ConnectedClients = 50;
        stats.MaxConnectedClients = 50;

        // 降低连接
        stats.ConnectedClients = 30;

        // 最大值应保持
        Assert.Equal(50, stats.MaxConnectedClients);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：默认值：StartTime 接近当前时间")]
    public void DefaultValues_StartTimeNearNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var stats = new MqttStats();
        var after = DateTime.Now.AddSeconds(1);

        Assert.True(stats.StartTime >= before && stats.StartTime <= after);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：Version 默认值不为空")]
    public void DefaultValues_VersionNotEmpty()
    {
        var stats = new MqttStats();

        Assert.False(String.IsNullOrEmpty(stats.Version));
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：CalculateRates 间隔不足 0.5s 时跳过计算")]
    public void CalculateRates_TooSoon_SkipsUpdate()
    {
        var stats = new MqttStats();
        stats.MessagesReceived = 1000;
        stats.MessagesSent = 500;

        // 不等待，立即调用（elapsed < 0.5s 应跳过）
        stats.CalculateRates();

        // 未经过足够时间，速率应仍为 0
        Assert.Equal(0, stats.MessagesReceivedPerSecond);
        Assert.Equal(0, stats.MessagesSentPerSecond);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：订阅统计字段可正常读写")]
    public void SubscriptionStats_ReadWrite()
    {
        var stats = new MqttStats();
        stats.Subscriptions = 42;
        stats.Topics = 10;
        stats.RetainedMessages = 5;

        Assert.Equal(42, stats.Subscriptions);
        Assert.Equal(10, stats.Topics);
        Assert.Equal(5, stats.RetainedMessages);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：字节统计字段可正常读写")]
    public void ByteStats_ReadWrite()
    {
        var stats = new MqttStats();
        stats.BytesReceived = 1024 * 1024;
        stats.BytesSent = 2048 * 1024;

        Assert.Equal(1024 * 1024, stats.BytesReceived);
        Assert.Equal(2048 * 1024, stats.BytesSent);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Stats：TotalConnections 累计连接数正常累加")]
    public void TotalConnections_Accumulate()
    {
        var stats = new MqttStats();

        for (var i = 0; i < 100; i++)
            Interlocked.Increment(ref stats.TotalConnections);

        Assert.Equal(100, stats.TotalConnections);
    }
}
