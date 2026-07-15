using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>发布已释放</summary>
public sealed class PubRel : MqttIdMessage
{
    #region 属性
    /// <summary>原因码。MQTT 5.0，默认0x00表示成功</summary>
    public Byte ReasonCode { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PubRel()
    {
        Type = MqttType.PubRel;
        QoS = QualityOfService.AtLeastOnce;
    }

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}, Qos={(Int32)QoS}]";
    #endregion

    #region 读写方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        // MQTT 5.0：剩余长度 > 2 时包含 ReasonCode
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
    protected override Byte GetFlag()
    {
        Duplicate = false;
        QoS = QualityOfService.AtLeastOnce;

        var flag = 0;
        flag |= ((Byte)Type << 4) & 0b1111_0000;
        if (Duplicate) flag |= 0b0000_1000;
        flag |= ((Byte)QoS << 1) & 0b0000_0110;
        //if (Retain) flag |= 0b0000_0001;

        return (Byte)flag;
    }

    /// <summary>根据请求创建响应</summary>
    /// <returns></returns>
    public PubComp CreateComplete() => new() { Id = Id };
    #endregion
}