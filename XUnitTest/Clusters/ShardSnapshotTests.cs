using System;
using System.Text;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

public class ShardSnapshotTests
{
    [Fact(DisplayName = "ShardSnapshot 默认值")]
    public void DefaultValues()
    {
        using var ss = new ShardSnapshot();
        Assert.Equal(1024, ss.ShardCount);
        Assert.Equal(30, ss.SnapshotTimeout);
        Assert.Equal(16, ss.Parallelism);
    }

    [Fact(DisplayName = "ShardSnapshot 导出/导入")]
    public void ExportImport()
    {
        using var ss = new ShardSnapshot();
        // 直接导出的空快照（无数据）
        var emptyData = ss.ExportSnapshot();
        Assert.NotNull(emptyData);
        Assert.True(emptyData.Length == 2, "空快照应为 []"); // []

        // 通过 import 添加数据
        var testJson = "[" +
            "{\"s\":0,\"t\":\"test/topic1\",\"e\":\"node1:1883\",\"q\":1}," +
            "{\"s\":1,\"t\":\"test/topic2\",\"e\":\"node2:1883\",\"q\":0}" +
            "]";
        var importCount = ss.ImportSnapshot(Encoding.UTF8.GetBytes(testJson));
        Assert.Equal(2, importCount);

        // 导出并验证
        var data = ss.ExportSnapshot();
        Assert.NotNull(data);
        var exportedJson = Encoding.UTF8.GetString(data);
        Assert.Contains("test/topic1", exportedJson);
        Assert.Contains("test/topic2", exportedJson);

        // 导入到新实例
        using var ss2 = new ShardSnapshot();
        var count = ss2.ImportSnapshot(data);
        Assert.Equal(2, count);
    }

    [Fact(DisplayName = "ShardSnapshot 空快照")]
    public void EmptySnapshot()
    {
        using var ss = new ShardSnapshot();
        var data = ss.ExportSnapshot();
        Assert.NotNull(data);
        Assert.Equal("[]", Encoding.UTF8.GetString(data));
    }

    [Fact(DisplayName = "ShardSnapshot 统计信息")]
    public void GetStats()
    {
        using var ss = new ShardSnapshot();
        var stats = ss.GetStats();
        Assert.Contains("Shards=", stats);
        Assert.Contains("TotalSubscriptions=", stats);
    }
}
