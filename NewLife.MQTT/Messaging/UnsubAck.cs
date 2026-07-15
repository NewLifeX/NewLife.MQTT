using NewLife.Buffers;

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

    #region 读写方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        // MQTT 5.0：UNSUBACK 载荷为 PacketId + Properties(变长) + ReasonCodes
        if (context is MqttVersion ver && ver >= MqttVersion.V500)
        {
            Properties = new MqttProperties();
            Properties.Read(ref reader);

            ReasonCodes = [];
            while (reader.Available > 0)
                ReasonCodes.Add(reader.ReadByte());
        }
        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

        if (context is MqttVersion ver && ver >= MqttVersion.V500)
        {
            // 写入 Properties（即使为空也需要写入空长度）
            Properties ??= new MqttProperties();
            Properties.Write(ref writer);

            if (ReasonCodes != null)
            {
                foreach (var rc in ReasonCodes)
                    writer.WriteByte(rc);
            }
        }
        return true;
    }

    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize()
    {
        var size = 2;
        if (ReasonCodes != null) size += ReasonCodes.Count;
        if (Properties != null) size += Properties.GetEstimatedSize();
        return size;
    }
    #endregion

    #region 方法
    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}