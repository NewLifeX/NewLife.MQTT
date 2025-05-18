namespace NewLife.MQTT.Messaging;

/// <summary>带Id的消息</summary>
/// <remarks>
/// 很多类型的控制包的可变包头结构都包含了2字节的唯一标识字段。这些控制包是PUBLISH（QoS > 0），PUBACK，PUBREC，PUBREL，PUBCOMP，SUBSCRIBE，SUBACK，UNSUBSCRIBE，UNSUBACK。
/// SUBSCRIBE，UNSUBSCRIBE，PUBLISH（QoS > 0 的时候）控制包必须包含非零的唯一标识。
/// 每次客户端发送上述控制包的时候，必须分配一个未使用过的唯一标识。
/// 如果一个客户端重新发送一个特别的控制包，必须使用相同的唯一标识符。
/// 唯一标识会在客户端收到相应的确认包之后变为可用。例如PUBLIST在QoS1的时候对应PUBACK；
/// 在QoS2时对应PUBCOMP。对于SUBSCRIBE和UNSUBSCRIBE对应SUBACK和UNSUBACK。
/// 服务端发送QoS>0的PUBLISH时，上述内容同样适用。
/// QoS为0的PUBLISH包不允许包含唯一标识。
/// PUBACK，PUBREC，PUBREL包的唯一标识必须和对应的PUBLISH相同。
/// 同样的SUBACK和UNSUBACK的唯一标识必须与对应的SUBSCRIBE和UNSUBSCRIBE包相同。
/// </remarks>
public abstract class MqttIdMessage : MqttMessage
{
    #region 属性
    /// <summary>标识</summary>
    /// <remarks>大端字节序</remarks>
    public UInt16 Id { get; set; }
    #endregion

    #region 读写方法
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(Stream stream, Object? context)
    {
        // 读Id
        Id = stream.ReadBytes(2).ToUInt16(0, false);

        return true;
    }

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(Stream stream, Object? context)
    {
        // 写Id
        stream.Write(Id.GetBytes(false));

        return true;
    }
    #endregion
}