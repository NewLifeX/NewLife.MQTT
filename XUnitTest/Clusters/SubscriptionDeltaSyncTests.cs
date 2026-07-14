using System;
using System.Collections.Generic;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

public class SubscriptionDeltaSyncTests
{
    [Fact(DisplayName = "SubscriptionDeltaSync 默认值")]
    public void DefaultValues()
    {
        using var sds = new SubscriptionDeltaSync();
        Assert.True(sds.EnableDeltaPush);
        Assert.Equal(100, sds.BatchAckSize);
        Assert.Equal(100, sds.BatchAckWindowMs);
    }

    [Fact(DisplayName = "SubscriptionDeltaSync 广播模式（无路由表时）")]
    public void BroadcastWithoutRouter()
    {
        using var sds = new SubscriptionDeltaSync();
        var nodes = new List<String> { "node1", "node2", "node3" };
        var result = sds.GetTargetNodes("test/topic", nodes);
        Assert.Equal(3, result.Count); // 无路由表时全量广播
    }

    [Fact(DisplayName = "SubscriptionDeltaSync 定向推送")]
    public void DeltaPush()
    {
        // 创建真实路由表
        var ring = new ConsistentHashRing(64, 4, 2);
        ring.AddNode("node1");
        ring.AddNode("node2");
        ring.AddNode("node3");

        var router = new TopicRouter();
        using var sds = new SubscriptionDeltaSync { Router = router };
        var allNodes = new List<String> { "node1", "node2", "node3" };
        var result = sds.GetTargetNodes("test/topic", allNodes);
        Assert.NotEmpty(result);
        Assert.True(result.Count <= 3);
    }

    [Fact(DisplayName = "SubscriptionDeltaSync 关闭定向推送时回退广播")]
    public void DisableDeltaPush()
    {
        var ring = new ConsistentHashRing(64, 4, 2);
        ring.AddNode("node1");
        ring.AddNode("node2");
        var router = new TopicRouter();
        using var sds = new SubscriptionDeltaSync { Router = router, EnableDeltaPush = false };
        var allNodes = new List<String> { "node1", "node2" };
        // 关闭定向推送，回退到全量广播
        var result = sds.GetTargetNodes("test/topic", allNodes);
        Assert.Equal(2, result.Count);
    }

    [Fact(DisplayName = "SubscriptionDeltaSync 统计信息")]
    public void GetStats()
    {
        using var sds = new SubscriptionDeltaSync();
        sds.RecordSubscriptionPush("node1", "test/topic", true);
        var stats = sds.GetStats();
        Assert.Contains("Pushed=", stats);
    }
}
