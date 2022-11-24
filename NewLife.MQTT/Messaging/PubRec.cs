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

    #region 方法
    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);

    /// <summary>根据请求创建响应</summary>
    /// <returns></returns>
    public PubRel CreateRelease() => new() { Id = Id };
    #endregion
}