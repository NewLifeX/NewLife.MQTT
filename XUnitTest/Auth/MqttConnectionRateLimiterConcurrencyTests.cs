using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>连接速率限制器并发安全测试。验证修复 D4：检查-记录原子化</summary>
public class MqttConnectionRateLimiterConcurrencyTests
{
    [System.ComponentModel.DisplayName("并发连接：总数不超过窗口上限")]
    [Fact]
    public void ConcurrentConnections_NoExceedLimit()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 20,
            WindowDuration = TimeSpan.FromSeconds(60),
        };

        // 并发建立连接，允许数必须精确等于上限，不允许超限
        var allowed = 0;
        var threadCount = 8;
        var perThread = 10;
        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                if (limiter.IsConnectionAllowed("192.168.1.100"))
                    System.Threading.Interlocked.Increment(ref allowed);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // 窗口上限 20，共 80 次并发尝试。无原子保护时可能超发
        Assert.True(allowed <= 20, $"允许数 {allowed} 超过窗口上限 20，说明存在竞态");
        Assert.Equal(20, allowed);
    }

    [System.ComponentModel.DisplayName("并发连接：不同 IP 互不影响")]
    [Fact]
    public void ConcurrentConnections_DifferentIps_Independent()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 5,
            WindowDuration = TimeSpan.FromSeconds(60),
        };

        var tasks = Enumerable.Range(0, 4).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < 5; j++)
            {
                Assert.True(limiter.IsConnectionAllowed($"192.168.1.{10 + i}"));
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // 每个 IP 恰好 5 个
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(5, limiter.GetConnectionCount($"192.168.1.{10 + i}"));
        }
    }

    [System.ComponentModel.DisplayName("连接计数：窗口过期记录被清理")]
    [Fact]
    public void GetConnectionCount_WindowExpired_Cleared()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 100,
            WindowDuration = TimeSpan.FromMilliseconds(200),
        };

        Assert.True(limiter.IsConnectionAllowed("192.168.1.200"));
        Assert.Equal(1, limiter.GetConnectionCount("192.168.1.200"));

        // 等待窗口过期（Thread.Sleep 至少等待指定时长）
        System.Threading.Thread.Sleep(300);

        Assert.Equal(0, limiter.GetConnectionCount("192.168.1.200"));
    }
}
