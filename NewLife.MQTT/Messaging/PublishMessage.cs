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
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(Stream stream, Object? context)
    {
        Topic = ReadString(stream);

        if (QoS > 0)
        {
            if (!base.OnRead(stream, context)) return false;
        }

        //Payload = ReadData(stream);
        Payload = (ArrayPacket)stream.ReadBytes(-1);

        return true;
    }

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(Stream stream, Object? context)
    {
        WriteString(stream, Topic);

        if (QoS > 0)
        {
            if (!base.OnWrite(stream, context)) return false;
        }

        //WriteData(stream, Payload);
        Payload?.CopyTo(stream);

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag()
    {
        Duplicate = false;
        //QoS = QualityOfService.AtLeastOnce;
        Retain = false;

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