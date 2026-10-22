using NewLife.MQTT.Messaging;
using NewLife.Messaging;

namespace NewLife.MQTT.EventBus;

/// <summary>MQTT事件上下文</summary>
/// <remarks>在 <see cref="EventContext"/> 基础上携带原始MQTT消息，便于订阅方读取主题、QoS、属性等协议信息。</remarks>
public class MqttEventContext : EventContext
{
    /// <summary>原始MQTT消息</summary>
    public PublishMessage? Message { get; set; }
}
