using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>消息速率限制器单元测试</summary>
public class MqttMessageRateLimiterTests
{
    [System.ComponentModel.DisplayName("正常速率：不超过限制时允许通过")]
    [Fact]
    public void NormalRate_AllowsMessages()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 100,
            MaxBurstSize = 100,
        };

        for (var i = 0; i < 100; i++)
        {
            Assert.True(limiter.IsMessageAllowed("client1"));
        }
    }

    [System.ComponentModel.DisplayName("超限：突发消息超过桶容量后拒绝")]
    [Fact]
    public void ExceedBurst_BlocksMessages()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 10,
            MaxBurstSize = 3,
        };

        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.False(limiter.IsMessageAllowed("client1"));
    }

    [System.ComponentModel.DisplayName("不同客户端：独立令牌桶互不影响")]
    [Fact]
    public void DifferentClients_IndependentBuckets()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 10,
            MaxBurstSize = 2,
        };

        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.False(limiter.IsMessageAllowed("client1"));

        // 不同客户端不受影响
        Assert.True(limiter.IsMessageAllowed("client2"));
        Assert.True(limiter.IsMessageAllowed("client2"));
        Assert.False(limiter.IsMessageAllowed("client2"));
    }

    [System.ComponentModel.DisplayName("令牌补充：等待后可继续发送")]
    [Fact]
    public void TokenRefill_AllowsAfterWait()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 50, // 每秒 50 个令牌
            MaxBurstSize = 3,
        };

        // 消耗完令牌
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.False(limiter.IsMessageAllowed("client1"));

        // 等待令牌补充（50/s → 约 20ms 一个令牌）
        Thread.Sleep(100);

        // 应该可以再发送
        Assert.True(limiter.IsMessageAllowed("client1"));
        Assert.True(limiter.IsMessageAllowed("client1"));
    }

    [System.ComponentModel.DisplayName("获取可用令牌：返回当前令牌数")]
    [Fact]
    public void GetAvailableTokens_ReturnsTokenCount()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 100,
            MaxBurstSize = 5,
        };

        var tokens = limiter.GetAvailableTokens("client1");
        Assert.True(tokens >= 4.9 && tokens <= 5.1); // 初始满桶

        limiter.IsMessageAllowed("client1");
        tokens = limiter.GetAvailableTokens("client1");
        Assert.True(tokens >= 3.9 && tokens <= 4.1);
    }

    [System.ComponentModel.DisplayName("重置：清空所有令牌桶")]
    [Fact]
    public void Reset_ClearsAllBuckets()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 10,
            MaxBurstSize = 2,
        };

        limiter.IsMessageAllowed("client1");
        limiter.IsMessageAllowed("client1");

        limiter.Reset();

        Assert.True(limiter.IsMessageAllowed("client1"));
    }

    [System.ComponentModel.DisplayName("空客户端：不限制")]
    [Fact]
    public void EmptyClientId_NotLimited()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 1,
            MaxBurstSize = 1,
        };

        Assert.True(limiter.IsMessageAllowed(""));
        Assert.True(limiter.IsMessageAllowed(null));
    }
}
