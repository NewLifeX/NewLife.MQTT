
namespace NewLife.MQTT.Messaging;

/// <summary>断开连接</summary>
public sealed class DisconnectMessage : MqttMessage
{
    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public DisconnectMessage() => Type = MqttType.Disconnect;
    #endregion
}