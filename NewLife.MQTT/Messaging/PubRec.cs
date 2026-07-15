using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>发布已接收</summary>
public sealed class PubRec : MqttIdMessage
{
    #region 属性
    /// <summary>原因码。MQTT 5.0，默认0x00表示成功</summary>
    public Byte ReasonCode { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PubRec() => Type = MqttType.PubRec;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}]";
    #endregion

    #region 读写方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        // MQTT 5.0：剩余长度 > 2 时包含 ReasonCode；若 == 2 则 ReasonCode 隐含为 0x00
        if (reader.Available > 0)
        {
            ReasonCode = reader.ReadByte();
            if (reader.Available > 0)
            {
                Properties = new MqttProperties();
                Properties.Read(ref reader);
            }
        }
        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

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
        if (ReasonCode != 0 || Properties != null)
            return 3 + (Properties?.GetEstimatedSize() ?? 0);
        return 2;
    }
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