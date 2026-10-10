using NewLife.Data;
using NewLife.Messaging;

namespace NewLife.MQTT.Messaging;

/// <summary>MQTT 帧消息。消息泵定界产出的帧载体，携带固定头字段与帧体</summary>
/// <remarks>
/// <para>v12 消息泵要求协议编解码器无状态：本类只承载"每次定界"的头部字段，帧体由泵绑定到 <see cref="Message.Payload"/> 或 <see cref="Message.Body"/>。</para>
/// <para>MQTT 协议版本属于连接态，由会话层在解码时提供，见 <see cref="MqttFactory.ReadMessage(MqttFrame, MqttVersion)"/>。</para>
/// <para>发送路径由调用方设置 <see cref="Mqtt"/> 与 <see cref="Version"/>，编解码器据此构建整帧。</para>
/// </remarks>
public class MqttFrame : Message
{
    #region 属性
    /// <summary>固定头首字节：报文类型（高4位）+ 标志位（低4位）</summary>
    public Byte FirstByte { get; set; }

    /// <summary>剩余长度（变长编码的解码值）</summary>
    public Int32 RemainingLength { get; set; }

    /// <summary>报文类型</summary>
    public MqttType Type => (MqttType)(FirstByte >> 4);

    /// <summary>待发送的MQTT消息。发送路径由调用方设置，接收路径为空</summary>
    public MqttMessage? Mqtt { get; set; }

    /// <summary>协议版本。发送路径按连接版本构建</summary>
    public MqttVersion Version { get; set; } = MqttVersion.V311;
    #endregion

    #region 方法
    /// <summary>读取报文标识符。发送路径取消息实例，接收路径从帧体零拷贝解析；不适用时返回0</summary>
    /// <returns></returns>
    public virtual UInt16 GetId()
    {
        if (Mqtt is MqttIdMessage idm) return idm.Id;

        var pk = Payload;
        if (pk == null || pk.Total < 2) return 0;

        switch (Type)
        {
            case MqttType.PubAck:
            case MqttType.PubRec:
            case MqttType.PubRel:
            case MqttType.PubComp:
            case MqttType.Subscribe:
            case MqttType.SubAck:
            case MqttType.UnSubscribe:
            case MqttType.UnSubAck:
                return (UInt16)((pk[0] << 8) | pk[1]);
            case MqttType.Publish:
                // 仅 QoS>0 的发布消息含标识符：主题长度(2) + 主题 + 标识符
                if (((FirstByte & 0b0000_0110) >> 1) == 0) return 0;
                if (pk.Total < 2) return 0;

                var pos = 2 + ((pk[0] << 8) | pk[1]);
                if (pk.Total < pos + 2) return 0;
                return (UInt16)((pk[pos] << 8) | pk[pos + 1]);
            default:
                return 0;
        }
    }

    /// <summary>根据固定头首字节推断消息方向</summary>
    /// <param name="firstByte">固定头首字节</param>
    /// <returns></returns>
    public static MessageKinds GetKind(Byte firstByte) => (MqttType)(firstByte >> 4) switch
    {
        MqttType.ConnAck or MqttType.PubAck or MqttType.PubRec or MqttType.PubComp or MqttType.SubAck or MqttType.UnSubAck or MqttType.PingResp => MessageKinds.Response,
        MqttType.Disconnect => MessageKinds.OneWay,
        _ => MessageKinds.Request,
    };
    #endregion
}
