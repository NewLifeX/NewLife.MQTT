using System;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttSessionCapabilities MQTT 5.0 会话能力单元测试</summary>
public class MqttSessionCapabilitiesTests
{
    #region 默认值
    [Fact]
    public void DefaultValues()
    {
        var caps = new MqttSessionCapabilities();

        Assert.Equal((UInt16)65535, caps.ReceiveMaximum);
        Assert.Equal(UInt32.MaxValue, caps.MaximumPacketSize);
        Assert.Equal((UInt16)0, caps.TopicAliasMaximum);
        Assert.Equal((UInt16)0, caps.ServerTopicAliasMaximum);
        Assert.Equal((UInt32)0, caps.SessionExpiryInterval);
        Assert.Equal(0, caps.InflightCount);
        Assert.Equal((UInt16)0, caps.ServerKeepAlive);
        Assert.NotNull(caps.ClientTopicAliases);
        Assert.NotNull(caps.ServerTopicAliases);
    }
    #endregion

    #region ApplyConnectProperties
    [Fact]
    public void ApplyConnectProperties_NullProperties_NoChange()
    {
        var caps = new MqttSessionCapabilities();
        caps.ApplyConnectProperties(null);

        Assert.Equal((UInt16)65535, caps.ReceiveMaximum);
    }

    [Fact]
    public void ApplyConnectProperties_SetsReceiveMaximum()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt16(MqttPropertyId.ReceiveMaximum, 100);

        caps.ApplyConnectProperties(props);

        Assert.Equal((UInt16)100, caps.ReceiveMaximum);
    }

    [Fact]
    public void ApplyConnectProperties_SetsMaxPacketSize()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt32(MqttPropertyId.MaximumPacketSize, 1024);

        caps.ApplyConnectProperties(props);

        Assert.Equal((UInt32)1024, caps.MaximumPacketSize);
    }

    [Fact]
    public void ApplyConnectProperties_SetsTopicAliasMaximum()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt16(MqttPropertyId.TopicAliasMaximum, 10);

        caps.ApplyConnectProperties(props);

        Assert.Equal((UInt16)10, caps.TopicAliasMaximum);
    }

    [Fact]
    public void ApplyConnectProperties_SetsSessionExpiryInterval()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);

        caps.ApplyConnectProperties(props);

        Assert.Equal((UInt32)3600, caps.SessionExpiryInterval);
    }

    [Fact]
    public void ApplyConnectProperties_ZeroReceiveMaximum_KeepsDefault()
    {
        var caps = new MqttSessionCapabilities();
        var props = new MqttProperties();
        props.SetUInt16(MqttPropertyId.ReceiveMaximum, 0);

        caps.ApplyConnectProperties(props);

        // 0 不是有效值，应保持默认
        Assert.Equal((UInt16)65535, caps.ReceiveMaximum);
    }
    #endregion

    #region ResolveTopicAlias
    [Fact]
    public void ResolveTopicAlias_NoAlias_ReturnsTrue()
    {
        var caps = new MqttSessionCapabilities();
        var msg = new PublishMessage { Topic = "test/topic" };

        Assert.True(caps.ResolveTopicAlias(msg));
    }

    [Fact]
    public void ResolveTopicAlias_NewAlias_CreatesMapping()
    {
        var caps = new MqttSessionCapabilities();
        var msg = new PublishMessage { Topic = "test/topic" };
        msg.Properties = new MqttProperties();
        msg.Properties.SetUInt16(MqttPropertyId.TopicAlias, 1);

        Assert.True(caps.ResolveTopicAlias(msg));
        Assert.Equal("test/topic", caps.ClientTopicAliases[1]);
    }

    [Fact]
    public void ResolveTopicAlias_ExistingAlias_RestoresTopic()
    {
        var caps = new MqttSessionCapabilities();

        // 先建立映射
        var msg1 = new PublishMessage { Topic = "test/topic" };
        msg1.Properties = new MqttProperties();
        msg1.Properties.SetUInt16(MqttPropertyId.TopicAlias, 1);
        caps.ResolveTopicAlias(msg1);

        // 然后只用别名（无主题名）
        var msg2 = new PublishMessage { Topic = "" };
        msg2.Properties = new MqttProperties();
        msg2.Properties.SetUInt16(MqttPropertyId.TopicAlias, 1);

        Assert.True(caps.ResolveTopicAlias(msg2));
        Assert.Equal("test/topic", msg2.Topic);
    }

    [Fact]
    public void ResolveTopicAlias_UnknownAlias_ReturnsFalse()
    {
        var caps = new MqttSessionCapabilities();
        var msg = new PublishMessage { Topic = "" };
        msg.Properties = new MqttProperties();
        msg.Properties.SetUInt16(MqttPropertyId.TopicAlias, 99);

        Assert.False(caps.ResolveTopicAlias(msg));
    }

    [Fact]
    public void ResolveTopicAlias_ZeroAlias_ReturnsTrue()
    {
        var caps = new MqttSessionCapabilities();
        var msg = new PublishMessage { Topic = "test" };
        msg.Properties = new MqttProperties();
        msg.Properties.SetUInt16(MqttPropertyId.TopicAlias, 0);

        Assert.True(caps.ResolveTopicAlias(msg));
    }
    #endregion

    #region AssignTopicAlias
    [Fact]
    public void AssignTopicAlias_ZeroMaximum_DoesNothing()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 0 };
        var msg = new PublishMessage { Topic = "test" };

        caps.AssignTopicAlias(msg);

        Assert.Null(msg.Properties);
    }

    [Fact]
    public void AssignTopicAlias_AssignsNewAlias()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 10 };
        var msg = new PublishMessage { Topic = "test/topic" };

        caps.AssignTopicAlias(msg);

        Assert.NotNull(msg.Properties);
        var alias = msg.Properties.GetUInt16(MqttPropertyId.TopicAlias);
        Assert.NotNull(alias);
        Assert.Equal((UInt16)1, alias.Value);
    }

    [Fact]
    public void AssignTopicAlias_ReusesExistingAlias()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 10 };

        // 第一次分配
        var msg1 = new PublishMessage { Topic = "test/topic" };
        caps.AssignTopicAlias(msg1);

        // 第二次应复用
        var msg2 = new PublishMessage { Topic = "test/topic" };
        caps.AssignTopicAlias(msg2);

        var alias1 = msg1.Properties!.GetUInt16(MqttPropertyId.TopicAlias);
        var alias2 = msg2.Properties!.GetUInt16(MqttPropertyId.TopicAlias);
        Assert.Equal(alias1, alias2);
    }

    [Fact]
    public void AssignTopicAlias_DifferentTopics_DifferentAliases()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 10 };

        var msg1 = new PublishMessage { Topic = "topic/a" };
        var msg2 = new PublishMessage { Topic = "topic/b" };
        caps.AssignTopicAlias(msg1);
        caps.AssignTopicAlias(msg2);

        var alias1 = msg1.Properties!.GetUInt16(MqttPropertyId.TopicAlias);
        var alias2 = msg2.Properties!.GetUInt16(MqttPropertyId.TopicAlias);
        Assert.NotEqual(alias1, alias2);
    }
    #endregion

    #region BuildConnAckProperties
    [Fact]
    public void BuildConnAckProperties_EmptyByDefault()
    {
        var caps = new MqttSessionCapabilities();

        var props = caps.BuildConnAckProperties();

        Assert.NotNull(props);
        // ServerTopicAliasMaximum 和 ServerKeepAlive 均为0，不写入属性
        Assert.Null(props.GetUInt16(MqttPropertyId.TopicAliasMaximum));
        Assert.Null(props.GetUInt16(MqttPropertyId.ServerKeepAlive));
    }

    [Fact]
    public void BuildConnAckProperties_IncludesServerTopicAliasMaximum()
    {
        var caps = new MqttSessionCapabilities { ServerTopicAliasMaximum = 100 };

        var props = caps.BuildConnAckProperties();

        Assert.Equal((UInt16)100, props.GetUInt16(MqttPropertyId.TopicAliasMaximum));
    }

    [Fact]
    public void BuildConnAckProperties_IncludesServerKeepAlive()
    {
        var caps = new MqttSessionCapabilities { ServerKeepAlive = 60 };

        var props = caps.BuildConnAckProperties();

        Assert.Equal((UInt16)60, props.GetUInt16(MqttPropertyId.ServerKeepAlive));
    }
    #endregion

    #region CanSendQosMessage
    [Fact]
    public void CanSendQosMessage_BelowMaximum_ReturnsTrue()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 10, InflightCount = 5 };

        Assert.True(caps.CanSendQosMessage());
    }

    [Fact]
    public void CanSendQosMessage_AtMaximum_ReturnsFalse()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 10, InflightCount = 10 };

        Assert.False(caps.CanSendQosMessage());
    }

    [Fact]
    public void CanSendQosMessage_AboveMaximum_ReturnsFalse()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 10, InflightCount = 15 };

        Assert.False(caps.CanSendQosMessage());
    }
    #endregion
}
