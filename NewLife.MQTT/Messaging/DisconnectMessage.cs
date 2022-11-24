
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

    #region 方法
    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}