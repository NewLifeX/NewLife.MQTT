using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>消息类型特性</summary>
public class MqttTypeAttribute : Attribute
{
    /// <summary>类型</summary>
    public MqttType Kind { get; set; }

    /// <summary>实例化消息类型特性</summary>
    /// <param name="kind"></param>
    public MqttTypeAttribute(MqttType kind) => Kind = kind;
}