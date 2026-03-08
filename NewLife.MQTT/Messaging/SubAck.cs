using NewLife.Buffers;

namespace NewLife.MQTT.Messaging;

/// <summary>订阅确认</summary>
public sealed class SubAck : MqttIdMessage
{
    #region 属性
    /// <summary>同意颁发的Qos</summary>
    public IList<QualityOfService> GrantedQos { get; set; } = [];

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public SubAck() => Type = MqttType.SubAck;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[Id={Id}, GrantedQos={(Int32)GrantedQos[0]}]";
    #endregion

    #region 读写方法
    /// <summary>从SpanReader读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(ref SpanReader reader, Object? context)
    {
        if (!base.OnRead(ref reader, context)) return false;

        var list = new List<QualityOfService>();
        while (reader.Available > 0)
        {
            list.Add((QualityOfService)reader.ReadByte());
        }
        GrantedQos = list;

        return true;
    }

    /// <summary>将消息写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(ref SpanWriter writer, Object? context)
    {
        if (!base.OnWrite(ref writer, context)) return false;

        foreach (var item in GrantedQos)
        {
            writer.WriteByte((Byte)item);
        }

        return true;
    }

    /// <summary>获取子消息体估算大小</summary>
    /// <returns></returns>
    protected override Int32 GetEstimatedBodySize() => 2 + GrantedQos.Count;

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion

    /// <summary>为订阅创建响应</summary>
    /// <param name="msg"></param>
    /// <param name="maxQoS"></param>
    /// <returns></returns>
    public static SubAck CreateReply(SubscribeMessage msg, QualityOfService maxQoS)
    {
        var ack = new SubAck
        {
            Id = msg.Id
        };
        var reqs = msg.Requests;
        var codes = new QualityOfService[reqs.Count];
        for (var i = 0; i < reqs.Count; i++)
        {
            var qos = reqs[i].QualityOfService;
            codes[i] = qos <= maxQoS ? qos : maxQoS;
        }

        ack.GrantedQos = codes;

        return ack;
    }
}