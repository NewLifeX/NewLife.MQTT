using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>取消订阅</summary>
public sealed class UnsubscribeMessage : MqttIdMessage
{
    #region 属性
    /// <summary>主题过滤器</summary>
    public IList<String> TopicFilters { get; set; } = [];

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public UnsubscribeMessage()
    {
        Type = MqttType.UnSubscribe;
        QoS = QualityOfService.AtLeastOnce;
    }

    /// <summary>已重载</summary>
    public override String ToString() => TopicFilters == null ? Type + "" : $"{Type}[Topic={TopicFilters[0]}]";
    #endregion

    #region 方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        var list = new List<String>();
        while (reader.Available > 0)
        {
            list.Add(ReadString(ref reader));
        }
        TopicFilters = list;

        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

        foreach (var item in TopicFilters)
        {
            WriteString(ref writer, item);
        }

        return true;
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
    public UnsubAck CreateAck() => new() { Id = Id };
    #endregion
}