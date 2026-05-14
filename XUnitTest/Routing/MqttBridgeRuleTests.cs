using System;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttBridgeRule 桥接规则单元测试</summary>
public class MqttBridgeRuleTests
{
    #region MqttBridgeRule
    [Fact]
    public void MqttBridgeRule_DefaultValues()
    {
        var rule = new MqttBridgeRule();

        Assert.Equal("#", rule.LocalTopic);
        Assert.Equal("#", rule.RemoteTopic);
        Assert.Equal(BridgeDirection.Both, rule.Direction);
        Assert.Null(rule.TopicPrefix);
        Assert.Equal(QualityOfService.ExactlyOnce, rule.MaxQoS);
    }

    [Fact]
    public void MqttBridgeRule_SetProperties()
    {
        var rule = new MqttBridgeRule
        {
            LocalTopic = "device/#",
            RemoteTopic = "cloud/#",
            Direction = BridgeDirection.Out,
            TopicPrefix = "prefix/",
            MaxQoS = QualityOfService.AtLeastOnce,
        };

        Assert.Equal("device/#", rule.LocalTopic);
        Assert.Equal("cloud/#", rule.RemoteTopic);
        Assert.Equal(BridgeDirection.Out, rule.Direction);
        Assert.Equal("prefix/", rule.TopicPrefix);
        Assert.Equal(QualityOfService.AtLeastOnce, rule.MaxQoS);
    }

    [Theory]
    [InlineData(BridgeDirection.Out)]
    [InlineData(BridgeDirection.In)]
    [InlineData(BridgeDirection.Both)]
    public void BridgeDirection_AllValues(BridgeDirection direction)
    {
        var rule = new MqttBridgeRule { Direction = direction };

        Assert.Equal(direction, rule.Direction);
    }
    #endregion

    #region MqttBridge
    [Fact]
    public void MqttBridge_DefaultValues()
    {
        using var bridge = new MqttBridge();

        Assert.Equal("Bridge", bridge.Name);
        Assert.Null(bridge.RemoteServer);
        Assert.Null(bridge.ClientId);
        Assert.Null(bridge.UserName);
        Assert.Null(bridge.Password);
        Assert.NotNull(bridge.Rules);
        Assert.Empty(bridge.Rules);
        Assert.Null(bridge.Exchange);
    }

    [Fact]
    public void MqttBridge_SetProperties()
    {
        using var bridge = new MqttBridge
        {
            Name = "TestBridge",
            RemoteServer = "tcp://remote:1883",
            ClientId = "bridge_client",
            UserName = "user",
            Password = "pass",
        };

        Assert.Equal("TestBridge", bridge.Name);
        Assert.Equal("tcp://remote:1883", bridge.RemoteServer);
        Assert.Equal("bridge_client", bridge.ClientId);
        Assert.Equal("user", bridge.UserName);
        Assert.Equal("pass", bridge.Password);
    }

    [Fact]
    public void MqttBridge_ForwardToRemote_NotStarted_NoException()
    {
        using var bridge = new MqttBridge();
        var msg = new PublishMessage { Topic = "test" };

        // 未启动时不应抛异常
        bridge.ForwardToRemote(msg);
    }

    [Fact]
    public void MqttBridge_Rules_CanAdd()
    {
        using var bridge = new MqttBridge();
        bridge.Rules.Add(new MqttBridgeRule
        {
            LocalTopic = "local/#",
            RemoteTopic = "remote/#",
            Direction = BridgeDirection.Both,
        });

        Assert.Single(bridge.Rules);
    }
    #endregion
}
