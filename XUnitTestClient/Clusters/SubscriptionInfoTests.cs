using System.Collections.Generic;
using NewLife.MQTT.Clusters;
using NewLife.Serialization;
using Xunit;

namespace XUnitTestClient.Clusters;

public class SubscriptionInfoTests
{
    [Fact]
    public void Test1()
    {
        var infos = new List<SubscriptionInfo>
        {
            new() { Topic = "topic1", Endpoint = "tcp://123"},
            new() { Topic = "topic2", Endpoint = "tcp://456"},
        };
        var p = new
        {
            infos,
        };
        var json = p.ToJson();

        var dic = JsonParser.Decode(json);
        var data = dic["infos"];
        Assert.NotNull(data);

        var fs = JsonHelper.Convert<SubscriptionInfo[]>(data);
        Assert.NotNull(fs);
        Assert.Equal(infos.Count, fs.Length);
    }
}
