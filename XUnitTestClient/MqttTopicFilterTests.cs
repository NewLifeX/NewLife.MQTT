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

    [Fact]
    public void ExtractActualTopicFilter_NonSharedSubscription()
    {
        // 测试非共享订阅
        var result = MqttTopicFilter.ExtractActualTopicFilter("test/topic");
        Assert.Equal("test/topic", result);
    }

    [Fact]
    public void ExtractActualTopicFilter_SharedSubscription()
    {
        // 测试共享订阅格式: $share/group/topic
        var result = MqttTopicFilter.ExtractActualTopicFilter("$share/locat/Yh/Drive/+/OrderLock");
        Assert.Equal("Yh/Drive/+/OrderLock", result);
    }

    [Fact]
    public void ExtractActualTopicFilter_SharedSubscriptionWithMultiLevelWildcard()
    {
        // 测试共享订阅格式带多级通配符
        var result = MqttTopicFilter.ExtractActualTopicFilter("$share/group/test/#");
        Assert.Equal("test/#", result);
    }

    [Fact]
    public void IsValidTopicFilter_SharedSubscription()
    {
        // 测试共享订阅是否被认为是有效的
        Assert.True(MqttTopicFilter.IsValidTopicFilter("$share/locat/Yh/Drive/+/OrderLock"));
        Assert.True(MqttTopicFilter.IsValidTopicFilter("$share/group/test/#"));
        Assert.True(MqttTopicFilter.IsValidTopicFilter("$share/group/test/+/topic"));
    }

    [Fact]
    public void IsMatch_SharedSubscription_SingleLevelWildcard()
    {
        // 测试问题场景：订阅 $share/locat/Yh/Drive/+/OrderLock，发布到 Yh/Drive/1111/OrderLock
        var topicName = "Yh/Drive/1111/OrderLock";
        var topicFilter = "$share/locat/Yh/Drive/+/OrderLock";
        
        Assert.True(MqttTopicFilter.IsMatch(topicName, topicFilter));
    }

    [Fact]
    public void IsMatch_SharedSubscription_MultiLevelWildcard()
    {
        // 测试共享订阅与多级通配符
        var topicName = "test/topic/subtopic/data";
        var topicFilter = "$share/group/test/#";
        
        Assert.True(MqttTopicFilter.IsMatch(topicName, topicFilter));
    }

    [Fact]
    public void IsMatch_SharedSubscription_ExactMatch()
    {
        // 测试共享订阅精确匹配
        var topicName = "test/topic";
        var topicFilter = "$share/group/test/topic";
        
        Assert.True(MqttTopicFilter.IsMatch(topicName, topicFilter));
    }

    [Fact]
    public void IsMatch_SharedSubscription_NoMatch()
    {
        // 测试共享订阅不匹配的情况
        var topicName = "test/other";
        var topicFilter = "$share/group/test/topic";
        
        Assert.False(MqttTopicFilter.IsMatch(topicName, topicFilter));
    }
}
