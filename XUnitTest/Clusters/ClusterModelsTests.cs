using System;
using System.Collections.Generic;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient.Clusters;

/// <summary>Cluster 数据模型单元测试</summary>
public class ClusterModelsTests
{
    #region SessionInfo
    [Fact]
    public void SessionInfo_DefaultValues()
    {
        var info = new SessionInfo();

        Assert.NotNull(info.Subscriptions);
        Assert.Empty(info.Subscriptions);
    }

    [Fact]
    public void SessionInfo_SetClientId()
    {
        var info = new SessionInfo { ClientId = "client1" };

        Assert.Equal("client1", info.ClientId);
    }

    [Fact]
    public void SessionInfo_AddSubscriptions()
    {
        var info = new SessionInfo
        {
            ClientId = "client1",
            Subscriptions = new Dictionary<String, Int32>
            {
                ["topic/a"] = 0,
                ["topic/b"] = 1,
                ["topic/c"] = 2,
            },
        };

        Assert.Equal(3, info.Subscriptions.Count);
        Assert.Equal(0, info.Subscriptions["topic/a"]);
        Assert.Equal(1, info.Subscriptions["topic/b"]);
        Assert.Equal(2, info.Subscriptions["topic/c"]);
    }
    #endregion

    #region NodeInfo
    [Fact]
    public void NodeInfo_DefaultValues()
    {
        var info = new NodeInfo();

        Assert.Null(info.Version);
    }

    [Fact]
    public void NodeInfo_SetProperties()
    {
        var info = new NodeInfo
        {
            EndPoint = "192.168.1.1:18883",
            Version = "1.0.0",
        };

        Assert.Equal("192.168.1.1:18883", info.EndPoint);
        Assert.Equal("1.0.0", info.Version);
    }
    #endregion

    #region PublishInfo
    [Fact]
    public void PublishInfo_DefaultValues()
    {
        var info = new PublishInfo();

        Assert.Null(info.RemoteEndpoint);
    }

    [Fact]
    public void PublishInfo_SetProperties()
    {
        var msg = new PublishMessage
        {
            Topic = "test/topic",
            QoS = QualityOfService.AtLeastOnce,
        };

        var info = new PublishInfo
        {
            Message = msg,
            RemoteEndpoint = "10.0.0.1:1883",
        };

        Assert.Same(msg, info.Message);
        Assert.Equal("test/topic", info.Message.Topic);
        Assert.Equal("10.0.0.1:1883", info.RemoteEndpoint);
    }
    #endregion
}
