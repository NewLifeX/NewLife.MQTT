namespace NewLife.MQTT.Messaging;

/// <summary>取消订阅确认</summary>
public sealed class UnsubAck : MqttIdMessage
{
    #region 属性
    /// <summary>原因码列表。MQTT 5.0，每个主题过滤器对应一个原因码</summary>
    public IList<Byte>? ReasonCodes { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public UnsubAck() => Type = MqttType.UnSubAck;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}]";
    #endregion

    #region 方法
    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}