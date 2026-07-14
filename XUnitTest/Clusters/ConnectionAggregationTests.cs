using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

public class ConnectionAggregationTests
{
    [Fact(DisplayName = "ConnectionAggregation 默认值")]
    public void DefaultValues()
    {
        using var ca = new ConnectionAggregation();
        Assert.Equal(0, ca.DeviceConnectionCount);
        Assert.Equal(5, ca.BatchWindowMs);
        Assert.Equal(64 * 1024, ca.BatchSizeThreshold);
    }

    [Fact(DisplayName = "ConnectionAggregation 设备注册")]
    public void RegisterDevice()
    {
        using var ca = new ConnectionAggregation();
        Assert.Equal(1, ca.AggregationRatio);

        ca.RegisterDevice("dev1", "192.168.1.1:1234");
        Assert.Equal(1, ca.DeviceConnectionCount);
        Assert.Equal(1, ca.AggregatedConnectionCount);

        // 注册多个设备，聚合比变化
        ca.RegisterDevice("dev2", "192.168.1.2:1235");
        Assert.Equal(2, ca.DeviceConnectionCount);
    }

    [Fact(DisplayName = "ConnectionAggregation 设备注销")]
    public void UnregisterDevice()
    {
        using var ca = new ConnectionAggregation();
        ca.RegisterDevice("dev1", "ep1");
        ca.RegisterDevice("dev2", "ep2");
        Assert.Equal(2, ca.DeviceConnectionCount);

        ca.UnregisterDevice("dev1");
        Assert.Equal(1, ca.DeviceConnectionCount);
    }

    [Fact(DisplayName = "ConnectionAggregation 消息统计")]
    public void MessageStats()
    {
        using var ca = new ConnectionAggregation();
        ca.RecordDeviceMessage("dev1", [0x01, 0x02]);
        ca.RecordAggregatedBatch();
        var stats = ca.GetStats();
        Assert.Contains("Devices=0", stats);
    }
}
