namespace NewLife.MQTT.Messaging;

/// <summary>取消订阅确认</summary>
public sealed class UnsubAck : MqttIdMessage
{
    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public UnsubAck() => Type = MqttType.UnSubAck;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}]";
    #endregion

    /// <summary>根据请求创建响应</summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public static UnsubAck CreateReply(UnsubscribeMessage msg)
    {
        return new UnsubAck
        {
            Id = msg.Id
        };
    }
}