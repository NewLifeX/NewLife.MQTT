namespace NewLife.MQTT.Messaging;

/// <summary>发布已接收</summary>
public sealed class PubRec : MqttIdMessage
{
    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PubRec() => Type = MqttType.PubRec;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}]";
    #endregion
}