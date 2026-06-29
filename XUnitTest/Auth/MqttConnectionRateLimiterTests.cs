using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>连接速率限制器单元测试</summary>
public class MqttConnectionRateLimiterTests
{
    [System.ComponentModel.DisplayName("正常速率：窗口内少量连接允许通过")]
    [Fact]
    public void NormalRate_AllowsConnections()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 10,
            WindowDuration = TimeSpan.FromSeconds(5),
        };

        for (var i = 0; i < 10; i++)
        {
            Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        }
    }

    [System.ComponentModel.DisplayName("超限：窗口内超过限制后拒绝连接")]
    [Fact]
    public void ExceedLimit_BlocksConnections()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 3,
            WindowDuration = TimeSpan.FromSeconds(5),
        };

        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        Assert.False(limiter.IsConnectionAllowed("192.168.1.1"));
    }

    [System.ComponentModel.DisplayName("不同 IP：独立计数互不影响")]
    [Fact]
    public void DifferentIps_IndependentCounters()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 2,
            WindowDuration = TimeSpan.FromSeconds(5),
        };

        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        Assert.False(limiter.IsConnectionAllowed("192.168.1.1"));

        // 不同 IP 不受影响
        Assert.True(limiter.IsConnectionAllowed("10.0.0.1"));
        Assert.True(limiter.IsConnectionAllowed("10.0.0.1"));
        Assert.False(limiter.IsConnectionAllowed("10.0.0.1"));
    }

    [System.ComponentModel.DisplayName("获取连接数：返回当前窗口内连接数")]
    [Fact]
    public void GetConnectionCount_ReturnsWindowCount()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 100,
            WindowDuration = TimeSpan.FromSeconds(5),
        };

        limiter.IsConnectionAllowed("192.168.1.1");
        limiter.IsConnectionAllowed("192.168.1.1");
        limiter.IsConnectionAllowed("192.168.1.1");

        Assert.Equal(3, limiter.GetConnectionCount("192.168.1.1"));
    }

    [System.ComponentModel.DisplayName("重置：清空所有记录")]
    [Fact]
    public void Reset_ClearsAllRecords()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 2,
            WindowDuration = TimeSpan.FromSeconds(5),
        };

        limiter.IsConnectionAllowed("192.168.1.1");
        limiter.IsConnectionAllowed("192.168.1.1");

        limiter.Reset();

        Assert.Equal(0, limiter.GetConnectionCount("192.168.1.1"));
        Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
    }

    [System.ComponentModel.DisplayName("空 IP：不限制")]
    [Fact]
    public void EmptyIp_NotLimited()
    {
        var limiter = new MqttConnectionRateLimiter
        {
            MaxConnectionsPerWindow = 1,
        };

        Assert.True(limiter.IsConnectionAllowed(""));
        Assert.True(limiter.IsConnectionAllowed(null));
        Assert.True(limiter.IsConnectionAllowed(""));
    }
}
