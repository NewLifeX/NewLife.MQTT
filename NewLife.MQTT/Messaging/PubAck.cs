using NewLife.Buffers;

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
    public override String ToString() => $"{Type}[Id={Id}, ReasonCode=0x{ReasonCode:X2}]";
    #endregion

    #region 读写方法
    /// <summary>从SpanReader读取消息。MQTT 5.0 PUBACK 在 PacketId 后可附带 ReasonCode 和 Properties</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        // MQTT 5.0：Remaining Length > 2 时包含 ReasonCode；若 == 2 则 ReasonCode 隐含为 0x00 (Success)
        if (reader.Available > 0)
        {
            ReasonCode = reader.ReadByte();

            // 还有剩余字节时读取 Properties
            if (reader.Available > 0)
            {
                Properties = new MqttProperties();
                Properties.Read(ref reader);
            }
        }

        return true;
    }

    /// <summary>将消息写入SpanWriter。仅在 MQTT 5.0 连接且 ReasonCode 非 0 或有 Properties 时写入额外字段</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文，传入 MqttVersion 时按 MQTT 5.0 写入 ReasonCode</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

        // MQTT 5.0 协议且有非成功原因码或有属性时写入 ReasonCode
        if (context is MqttVersion ver && ver >= MqttVersion.V500 && (ReasonCode != 0 || Properties != null))
        {
            writer.WriteByte(ReasonCode);

            if (Properties != null)
                Properties.Write(ref writer);
        }

        return true;
    }

    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize()
    {
        // PacketId(2) + ReasonCode(1，可选) + Properties(可选)
        if (ReasonCode != 0 || Properties != null)
            return 3 + (Properties?.GetEstimatedSize() ?? 0);
        return 2;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}