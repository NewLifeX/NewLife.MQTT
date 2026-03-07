using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>订阅请求</summary>
public sealed class SubscribeMessage : MqttIdMessage
{
    #region 属性
    /// <summary>请求集合</summary>
    public IList<Subscription> Requests { get; set; } = [];

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public SubscribeMessage()
    {
        Type = MqttType.Subscribe;
        QoS = QualityOfService.AtLeastOnce;
    }

    /// <summary>已重载</summary>
    public override String ToString() => Requests == null ? Type + "" : $"{Type}[Topic={Requests[0].TopicFilter}, Qos={(Int32)Requests[0].QualityOfService}]";
    #endregion

    #region 读写方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        var list = new List<Subscription>();
        while (reader.Available > 0)
        {
            var topicFilter = ReadString(ref reader);
            var options = reader.ReadByte();

            // MQTT 5.0 订阅选项字节格式：
            // Bit 0-1: QoS (0/1/2)
            // Bit 2: No Local
            // Bit 3: Retain As Published
            // Bit 4-5: Retain Handling (0/1/2)
            var qos = (QualityOfService)(options & 0x03);
            var ss = new Subscription(topicFilter, qos)
            {
                NoLocal = (options & 0x04) != 0,
                RetainAsPublished = (options & 0x08) != 0,
                RetainHandling = (Byte)((options >> 4) & 0x03),
            };
            list.Add(ss);
        }
        Requests = list;

        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

        foreach (var item in Requests)
        {
            WriteString(ref writer, item.TopicFilter);

            // MQTT 5.0 订阅选项字节
            var options = (Byte)item.QualityOfService;
            if (item.NoLocal) options |= 0x04;
            if (item.RetainAsPublished) options |= 0x08;
            options |= (Byte)((item.RetainHandling & 0x03) << 4);
            writer.WriteByte(options);
        }

        return true;
    }

    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize()
    {
        var size = 64;
        foreach (var item in Requests)
        {
            size += 3 + (item.TopicFilter?.Length ?? 0) * 3;
        }
        return size;
    }

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
    public SubAck CreateAck() => new() { Id = Id };
    #endregion
}