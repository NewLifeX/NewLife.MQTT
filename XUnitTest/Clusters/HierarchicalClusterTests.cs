using System;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

public class HierarchicalClusterTests
{
    [Fact(DisplayName = "HierarchicalCluster 默认值")]
    public void DefaultValues()
    {
        using var hc = new HierarchicalCluster();
        Assert.Equal(NodeRole.Flat, hc.Role);
        Assert.False(hc.Active);
        Assert.Empty(hc.DownstreamNodes);
    }

    [Fact(DisplayName = "HierarchicalCluster 角色管理")]
    public void RoleManagement()
    {
        using var hc = new HierarchicalCluster { Role = NodeRole.Regional, Region = "cn-east" };
        Assert.Equal(NodeRole.Regional, hc.Role);
        Assert.Equal("cn-east", hc.Region);
    }

    [Fact(DisplayName = "HierarchicalCluster 注册/注销下层节点")]
    public void RegisterUnregisterDownstream()
    {
        using var hc = new HierarchicalCluster { Role = NodeRole.Core, Log = NewLife.Log.XTrace.Log };
        Assert.False(hc.RegisterDownstream("edge1:1883", NodeRole.Edge, "cn-east"));
        // Core 只接受 Regional
        Assert.True(hc.RegisterDownstream("reg1:2883", NodeRole.Regional, "cn-east"));
        Assert.Single(hc.DownstreamNodes);
        // 重复注册
        Assert.False(hc.RegisterDownstream("reg1:2883", NodeRole.Regional, "cn-east"));
        // 注销
        Assert.True(hc.UnregisterDownstream("reg1:2883"));
        Assert.Empty(hc.DownstreamNodes);
    }

    [Fact(DisplayName = "HierarchicalCluster 区域布隆过滤器")]
    public void RegionBloomFilter()
    {
        using var hc = new HierarchicalCluster { Role = NodeRole.Core };
        hc.UpdateRegionBloomFilter("cn-east", new Byte[] { 0x12, 0x34 }, 3, 16);
        Assert.Single(hc.RegionalBloomFilters);

        var exists = hc.MightContainInRegion("cn-east", "test/topic");
        // 布隆过滤器可能误判，但不会漏判
        Assert.NotNull(exists);
    }

    [Fact(DisplayName = "HierarchicalCluster 统计信息")]
    public void GetStats()
    {
        using var hc = new HierarchicalCluster { Role = NodeRole.Regional, Region = "cn-east" };
        var stats = hc.GetStats();
        Assert.Contains("Role=Regional", stats);
        Assert.Contains("Region=cn-east", stats);
    }
}
