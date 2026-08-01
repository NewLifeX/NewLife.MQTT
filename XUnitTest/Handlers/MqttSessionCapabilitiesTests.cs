using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>MQTT 5.0 会话能力测试。验证修复 D6/D7：InflightCount 原子操作与条件递减</summary>
public class MqttSessionCapabilitiesTests
{
    [System.ComponentModel.DisplayName("流控：递增后 CanSendQosMessage 受 ReceiveMaximum 限制")]
    [Fact]
    public void FlowControl_InflightCount_LimitsSending()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 5 };

        for (var i = 0; i < 5; i++)
        {
            Assert.True(caps.CanSendQosMessage());
            caps.IncrementInflight();
        }

        // 达到上限后拒绝
        Assert.False(caps.CanSendQosMessage());

        // 确认一条后可继续发送
        caps.DecrementInflight();
        Assert.True(caps.CanSendQosMessage());
        Assert.Equal(4, caps.InflightCount);
    }

    [System.ComponentModel.DisplayName("条件递减：计数为 0 时递减不产生负数")]
    [Fact]
    public void DecrementInflight_AtZero_NoNegative()
    {
        var caps = new MqttSessionCapabilities();

        // 重复递减（如重复 ACK），计数不应变负
        caps.DecrementInflight();
        caps.DecrementInflight();
        caps.DecrementInflight();

        Assert.Equal(0, caps.InflightCount);
    }

    [System.ComponentModel.DisplayName("并发递增递减：总数保持正确且不为负")]
    [Fact]
    public void ConcurrentIncrementDecrement_Consistent()
    {
        var caps = new MqttSessionCapabilities { ReceiveMaximum = 65535 };

        var incrementTasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() => caps.IncrementInflight())).ToArray();
        Task.WaitAll(incrementTasks);
        Assert.Equal(1000, caps.InflightCount);

        var decrementTasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() => caps.DecrementInflight())).ToArray();
        Task.WaitAll(decrementTasks);
        Assert.Equal(0, caps.InflightCount);

        // 再多递减也不为负
        caps.DecrementInflight();
        Assert.Equal(0, caps.InflightCount);
    }

    [System.ComponentModel.DisplayName("主题别名：分配后复用且并发安全")]
    [Fact]
    public void TopicAlias_AssignAndReuse()
    {
        var caps = new MqttSessionCapabilities { TopicAliasMaximum = 10 };
        var msg1 = new NewLife.MQTT.Messaging.PublishMessage { Topic = "test/topic" };

        caps.AssignTopicAlias(msg1);
        Assert.True(msg1.Properties?.GetUInt16(NewLife.MQTT.Messaging.MqttPropertyId.TopicAlias) > 0);

        // 第二次分配同一主题复用别名
        var msg2 = new NewLife.MQTT.Messaging.PublishMessage { Topic = "test/topic" };
        caps.AssignTopicAlias(msg2);
        var alias1 = msg1.Properties!.GetUInt16(NewLife.MQTT.Messaging.MqttPropertyId.TopicAlias);
        var alias2 = msg2.Properties!.GetUInt16(NewLife.MQTT.Messaging.MqttPropertyId.TopicAlias);
        Assert.Equal(alias1, alias2);
    }
}
