using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>消息速率限制器并发安全测试。验证修复 D3：令牌桶线程安全</summary>
public class MqttMessageRateLimiterConcurrencyTests
{
    [System.ComponentModel.DisplayName("并发消费：多个线程同时消费令牌，总数不超过桶容量+补充量")]
    [Fact]
    public void ConcurrentConsume_NoOverConsumption()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 0, // 不补充，验证桶容量上限
            MaxBurstSize = 50,
        };

        // 并发消费，允许数必须精确等于桶容量，不允许超发
        var allowed = 0;
        var threadCount = 8;
        var perThread = 10;
        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                if (limiter.IsMessageAllowed("client-concurrent"))
                    Interlocked.Increment(ref allowed);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // 桶容量 50，共 80 次尝试，允许数必须 ≤ 50（无锁实现可能超发）
        Assert.True(allowed <= 50, $"允许数 {allowed} 超过桶容量 50，说明存在竞态");
        Assert.Equal(50, allowed);
    }

    [System.ComponentModel.DisplayName("并发消费+补充：允许数不超过容量且不重复消费")]
    [Fact]
    public void ConcurrentConsume_WithRefill_NoOverConsumption()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 1000,
            MaxBurstSize = 20,
        };

        // 并发消费，允许数上限 = 桶容量 + 补充量（1000/s * 短暂时间）
        var allowed = 0;
        var threadCount = 4;
        var perThread = 50;
        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                if (limiter.IsMessageAllowed("client-refill"))
                    Interlocked.Increment(ref allowed);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // 桶容量 20，200 次尝试并发。即使补充，也远小于 200（无锁实现可能接近 200 超发）
        Assert.True(allowed <= 120, $"允许数 {allowed} 异常超发，说明存在竞态");
        Assert.True(allowed >= 20, $"允许数 {allowed} 低于桶容量 20");
    }

    [System.ComponentModel.DisplayName("可用令牌数：并发读取不抛异常")]
    [Fact]
    public void AvailableTokens_ConcurrentRead_NoThrow()
    {
        var limiter = new MqttMessageRateLimiter
        {
            MaxMessagesPerSecond = 100,
            MaxBurstSize = 10,
        };

        // 预热
        limiter.IsMessageAllowed("client-tokens");
        limiter.IsMessageAllowed("client-tokens");

        // 并发读取
        var tasks = Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                var tokens = limiter.GetAvailableTokens("client-tokens");
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.True(limiter.GetAvailableTokens("client-tokens") >= 0);
    }}
