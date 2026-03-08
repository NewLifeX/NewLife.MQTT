using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>认证交换消息。MQTT 5.0 新增</summary>
/// <remarks>
/// AUTH报文用于增强认证流程，支持挑战/响应式的认证方式（如SASL）。
/// 可由客户端或服务端发送，用于在 CONNECT/CONNACK 之后继续认证交换。
/// 仅 MQTT 5.0 支持此报文类型。
/// </remarks>
public sealed class AuthMessage : MqttMessage
{
    #region 属性
    /// <summary>原因码。MQTT 5.0</summary>
    /// <remarks>
    /// 0x00=成功（认证完成），0x18=继续认证，0x19=重新认证
    /// </remarks>
    public Byte ReasonCode { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public AuthMessage() => Type = MqttType.Auth;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[ReasonCode=0x{ReasonCode:X2}]";
    #endregion

    #region 方法
    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize()
    {
        // ReasonCode 1字节 + 可选属性长度前缀(1字节) + 属性数据
        if (Properties != null && Properties.Count > 0)
            return 1 + 5 + Properties.GetEstimatedSize();
        return 2;
    }

    /// <summary>子消息读取</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        // AUTH 报文的可变报头：原因码 + 属性
        if (Length == 0)
        {
            // 如果剩余长度为0，原因码为0x00（成功）
            ReasonCode = 0x00;
            return true;
        }

        ReasonCode = reader.ReadByte();

        // 读取属性
        if (Length > 1)
        {
            Properties = new MqttProperties();
            Properties.Read(ref reader);
        }

        return true;
    }

    /// <summary>子消息写入</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        // 如果原因码为0且无属性，可省略可变报头
        if (ReasonCode == 0x00 && (Properties == null || Properties.Count == 0))
            return true;

        writer.WriteByte(ReasonCode);

        // 写入属性
        if (Properties != null && Properties.Count > 0)
            Properties.Write(ref writer);
        else
            writer.WriteEncodedInt(0);

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}
