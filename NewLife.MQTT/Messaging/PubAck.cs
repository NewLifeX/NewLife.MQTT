namespace NewLife.MQTT.Messaging;

/// <summary>发布确认</summary>
public sealed class PubAck : MqttIdMessage
{
    #region 属性
    /// <summary>原因码。MQTT 5.0，默认0x00表示成功</summary>
    public Byte ReasonCode { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PubAck() => Type = MqttType.PubAck;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}]";
    #endregion

    #region 方法
    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}