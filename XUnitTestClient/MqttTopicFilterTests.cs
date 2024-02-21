using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NewLife.MQTT;
using Xunit;

namespace XUnitTestClient;

public class MqttTopicFilterTests
{
    [Fact]
    public void Test()
    {
        var sub = new string[] { "/test/#", "/test/+/test/test", "/test/+/#" };

        var pub = "/test/test/test/test";
        foreach (var item in sub)
        {
            Assert.True(MqttTopicFilter.IsMatch(pub, item));
        }
        var sub1 = new string[] { "test/#", "/test/sss/test/test", "/test//#" };

        foreach (var item in sub1)
        {
            Assert.False(MqttTopicFilter.IsMatch(pub, item));
        }
    }

    [Fact]
    public void Expand2()
    {
        var topic = "/a/b";
        var ts = MqttTopicFilter.Expand(topic);
        Assert.Equal(5, ts.Count);

        //File.WriteAllLines("aa.txt", ts.Select(e => $"Assert.Contains(\"{e}\", ts);").ToArray());

        Assert.Contains("/a/b", ts);
        Assert.Contains("/a/+", ts);
        Assert.Contains("/a/#", ts);
        Assert.Contains("/+/b", ts);
        Assert.Contains("/#", ts);
    }

    [Fact]
    public void Expand3()
    {
        var topic = "/a/b/c";
        var ts = MqttTopicFilter.Expand(topic);
        Assert.Equal(9, ts.Count);

        //File.WriteAllLines("aa.txt", ts.Select(e => $"Assert.Contains(\"{e}\", ts);").ToArray());

        Assert.Contains("/a/b/c", ts);
        Assert.Contains("/a/b/+", ts);
        Assert.Contains("/a/b/#", ts);
        Assert.Contains("/a/+/c", ts);
        Assert.Contains("/a/#", ts);
        Assert.Contains("/+/b/c", ts);
        Assert.Contains("/+/b/+", ts);
        Assert.Contains("/+/+/c", ts);
        Assert.Contains("/#", ts);
    }

    [Fact]
    public void Expand4()
    {
        var topic = "/a/b/c/d";
        var ts = MqttTopicFilter.Expand(topic);
        Assert.Equal(16, ts.Count);

        //File.WriteAllLines("aa.txt", ts.Select(e=>$"Assert.Contains(\"{e}\", ts);").ToArray());

        Assert.Contains("/a/b/c/d", ts);
        Assert.Contains("/a/b/c/+", ts);
        Assert.Contains("/a/b/c/#", ts);
        Assert.Contains("/a/b/+/d", ts);
        //Assert.Contains("/a/b/+/+", ts);
        Assert.Contains("/a/b/#", ts);
        Assert.Contains("/a/+/c/d", ts);
        Assert.Contains("/a/+/c/+", ts);
        Assert.Contains("/a/+/+/d", ts);
        //Assert.Contains("/a/+/+/+", ts);
        Assert.Contains("/a/#", ts);
        Assert.Contains("/+/b/c/d", ts);
        Assert.Contains("/+/b/c/+", ts);
        Assert.Contains("/+/b/+/d", ts);
        //Assert.Contains("/+/b/+/+", ts);
        Assert.Contains("/+/+/c/d", ts);
        Assert.Contains("/+/+/c/+", ts);
        Assert.Contains("/+/+/+/d", ts);
        //Assert.Contains("/+/+/+/+", ts);
        Assert.Contains("/#", ts);
    }

    [Fact]
    public void Expand5()
    {
        var topic = "/a/b/c/d/e";
        var ts = MqttTopicFilter.Expand(topic);
        Assert.Equal(29, ts.Count);

        //File.WriteAllLines("aa.txt", ts.Select(e => $"Assert.Contains(\"{e}\", ts);").ToArray());

        Assert.Contains("/a/b/c/d/e", ts);
        Assert.Contains("/a/b/c/d/+", ts);
        Assert.Contains("/a/b/c/d/#", ts);
        Assert.Contains("/a/b/c/+/e", ts);
        Assert.Contains("/a/b/c/#", ts);
        Assert.Contains("/a/b/+/d/e", ts);
        Assert.Contains("/a/b/+/d/+", ts);
        Assert.Contains("/a/b/+/+/e", ts);
        Assert.Contains("/a/b/#", ts);
        Assert.Contains("/a/+/c/d/e", ts);
        Assert.Contains("/a/+/c/d/+", ts);
        Assert.Contains("/a/+/c/+/e", ts);
        Assert.Contains("/a/+/+/d/e", ts);
        Assert.Contains("/a/+/+/d/+", ts);
        Assert.Contains("/a/+/+/+/e", ts);
        Assert.Contains("/a/#", ts);
        Assert.Contains("/+/b/c/d/e", ts);
        Assert.Contains("/+/b/c/d/+", ts);
        Assert.Contains("/+/b/c/+/e", ts);
        Assert.Contains("/+/b/+/d/e", ts);
        Assert.Contains("/+/b/+/d/+", ts);
        Assert.Contains("/+/b/+/+/e", ts);
        Assert.Contains("/+/+/c/d/e", ts);
        Assert.Contains("/+/+/c/d/+", ts);
        Assert.Contains("/+/+/c/+/e", ts);
        Assert.Contains("/+/+/+/d/e", ts);
        Assert.Contains("/+/+/+/d/+", ts);
        Assert.Contains("/+/+/+/+/e", ts);
        Assert.Contains("/#", ts);
    }
}
