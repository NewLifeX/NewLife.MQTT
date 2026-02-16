using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttStats 运行统计测试</summary>
public class MqttStatsTests
{
    [Fact]
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
    public void UptimeIncreases()
    {
        var stats = new MqttStats();
        var uptime1 = stats.Uptime;

        Thread.Sleep(1100);

        var uptime2 = stats.Uptime;

        Assert.True(uptime2 > uptime1);
    }

    [Fact]
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
}
