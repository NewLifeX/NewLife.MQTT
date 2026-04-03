using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.MQTT.Messaging;

/// <summary>发布消息</summary>
public sealed class PublishMessage : MqttIdMessage
{
    #region 属性
    /// <summary>主题</summary>
    public String Topic { get; set; } = null!;

    /// <summary>负载数据</summary>
    public IPacket? Payload { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }

    /// <summary>服务端接收时间戳（服务端内部使用，不序列化）。用于 MessageExpiryInterval 过期检查</summary>
    public DateTime ReceivedAt { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PublishMessage() => Type = MqttType.Publish;

    /// <summary>已重载</summary>
    public override String ToString()
    {
        return QoS > 0 ?
            $"{Type}[Id={Id}, QoS={(Int32)QoS}, Topic={Topic}, Retain={Retain}]" :
            $"{Type}[QoS={(Int32)QoS}, Topic={Topic}, Retain={Retain}]";
    }
    #endregion

    #region 方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文，传入 MqttVersion 时按 MQTT 5.0 读取属性</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        Topic = reader.ReadLengthString(2);

        if (QoS > 0)
        {
            if (!base.OnRead(ref reader, context)) return false;
        }

        // MQTT 5.0 属性（MqttVersion >= V500 时读取）
        if (context is MqttVersion ver && ver >= MqttVersion.V500)
        {
            Properties = new MqttProperties();
            Properties.Read(ref reader);
        }

        //Payload = ReadData(ref reader);
        if (reader.Available > 0)
            Payload = reader.ReadPacket(reader.Available);

        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文，传入 MqttVersion 时按 MQTT 5.0 写入属性</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        writer.WriteLengthString(Topic, 2);

        if (QoS > 0)
        {
            if (!base.OnWrite(ref writer, context)) return false;
        }

        // MQTT 5.0 属性
        if (Properties != null)
        {
            Properties.Write(ref writer);
        }
        else if (context is MqttVersion ver && ver >= MqttVersion.V500)
        {
            // MQTT 5.0 连接但无属性时写入空属性长度（1字节，值=0）
            writer.WriteByte(0);
        }

        //WriteData(ref writer, Payload);
        if (Payload != null && Payload.Total > 0)
        {
            writer.Write(Payload);
        }

        return true;
    }

    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize()
    {
        var size = 64 +
            (Topic?.Length ?? 0) * 3 +
            (Payload?.Total ?? 0);

        // MQTT 5.0 属性
        if (Properties != null && Properties.Count > 0)
            size += 5 + Properties.GetEstimatedSize();

        return size;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag()
    {
        // 不再强制清零 Duplicate 和 Retain，由调用方控制
        // Duplicate 用于消息重发标记，Retain 用于保留消息
        var flag = 0;
        flag |= ((Byte)Type << 4) & 0b1111_0000;
        if (Duplicate) flag |= 0b0000_1000;
        flag |= ((Byte)QoS << 1) & 0b0000_0110;
        if (Retain) flag |= 0b0000_0001;

        return (Byte)flag;
    }

    /// <summary>根据请求创建响应</summary>
    /// <returns></returns>
    public PubRec CreateReceive() => new() { Id = Id };

    /// <summary>根据请求创建响应</summary>
    /// <returns></returns>
    public PubAck CreateAck() => new() { Id = Id };
    #endregion
}