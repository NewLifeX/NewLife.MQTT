using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

public class RouteDegradationTests
{
    [Fact(DisplayName = "RouteDegradation 默认值")]
    public void DefaultValues()
    {
        using var rd = new RouteDegradation();
        Assert.Equal(ClusterRouteMode.Flat, rd.CurrentMode);
        Assert.Equal(50, rd.FlatToShardedThreshold);
        Assert.Equal(30, rd.ShardedToFlatThreshold);
        Assert.Equal(1000, rd.ShardedToHierarchicalThreshold);
    }

    [Fact(DisplayName = "RouteDegradation 推荐模式计算")]
    public void RecommendedMode()
    {
        using var rd = new RouteDegradation();
        rd.UpdateNodeCount(10);
        Assert.Equal(ClusterRouteMode.Flat, rd.GetRecommendedMode());

        rd.UpdateNodeCount(100);
        Assert.Equal(ClusterRouteMode.Sharded, rd.GetRecommendedMode());

        rd.UpdateNodeCount(2000);
        Assert.Equal(ClusterRouteMode.Hierarchical, rd.GetRecommendedMode());
    }

    [Fact(DisplayName = "RouteDegradation 模式切换")]
    public void SwitchMode()
    {
        using var rd = new RouteDegradation();
        Assert.True(rd.SwitchMode(ClusterRouteMode.Sharded, "test"));
        Assert.Equal(ClusterRouteMode.Sharded, rd.CurrentMode);
        // 重复切换不应生效
        Assert.False(rd.SwitchMode(ClusterRouteMode.Sharded, "test"));
    }

    [Fact(DisplayName = "RouteDegradation 错误率统计")]
    public void ErrorRate()
    {
        using var rd = new RouteDegradation();
        rd.RecordRouteResult(false);
        rd.RecordRouteResult(true);
        rd.RecordRouteResult(true);
        // 错误率需要触发 Check 才会计算，这由定时器驱动
        // 此处验证 Record 方法不抛异常
        Assert.NotNull(rd.GetStats());
    }

    [Fact(DisplayName = "RouteDegradation 统计信息")]
    public void GetStats()
    {
        using var rd = new RouteDegradation();
        var stats = rd.GetStats();
        Assert.Contains("Mode=Flat", stats);
    }
}
