using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.MQTT.Messaging;

/// <summary>MQTT（Message Queue Telemetry Transport）,遥测传输协议</summary>
/// <remarks>
/// 提供订阅/发布模式，更为简约、轻量，易于使用，针对受限环境（带宽低、网络延迟高、网络通信不稳定），可以简单概括为物联网打造，官方总结特点如下：
/// 1.使用发布/订阅消息模式，提供一对多的消息发布，解除应用程序耦合。
/// 2.对负载内容屏蔽的消息传输。
/// 3.使用 TCP/IP 提供网络连接。
/// 4.有三种消息发布服务质量：
/// “至多一次”，消息发布完全依赖底层 TCP/IP 网络。会发生消息丢失或重复。这一级别可用于如下情况，环境传感器数据，丢失一次读记录无所谓，因为不久后还会有第二次发送。
/// “至少一次”，确保消息到达，但消息重复可能会发生。
/// “只有一次”，确保消息到达一次。这一级别可用于如下情况，在计费系统中，消息重复或丢失会导致不正确的结果。
/// 5.小型传输，开销很小（固定长度的头部是 2 字节），协议交换最小化，以降低网络流量。
/// 6.使用 Last Will 和 Testament 特性通知有关各方客户端异常中断的机制。
/// </remarks>
public abstract class MqttMessage : IAccessor
{
    #region 属性
    /// <summary>消息类型</summary>
    public MqttType Type { get; set; }

    /// <summary>打开标识。值为1时表示当前消息先前已经被传送过</summary>
    /// <remarks>
    /// 保证消息可靠传输，默认为0，只占用一个字节，表示第一次发送。不能用于检测消息重复发送等。
    /// 只适用于客户端或服务器端尝试重发PUBLISH, PUBREL, SUBSCRIBE 或 UNSUBSCRIBE消息，注意需要满足以下条件：
    /// 当QoS > 0
    /// 消息需要回复确认
    /// 此时，在可变头部需要包含消息ID。当值为1时，表示当前消息先前已经被传送过。
    /// </remarks>
    public Boolean Duplicate { get; set; }

    /// <summary>QoS等级。0/1/2</summary>
    public QualityOfService QoS { get; set; }

    /// <summary>保持。仅针对PUBLISH消息。不同值，不同含义</summary>
    /// <remarks>
    /// 1：表示发送的消息需要一直持久保存（不受服务器重启影响），不但要发送给当前的订阅者，并且以后新来的订阅了此Topic name的订阅者会马上得到推送。
    /// 备注：新来乍到的订阅者，只会取出最新的一个RETAIN flag = 1的消息推送。
    /// 0：仅仅为当前订阅者推送此消息。
    /// 假如服务器收到一个空消息体(zero-length payload)、RETAIN = 1、已存在Topic name的PUBLISH消息，服务器可以删除掉对应的已被持久化的PUBLISH消息。
    /// </remarks>
    public Boolean Retain { get; set; }

    /// <summary>长度。7位压缩编码整数</summary>
    /// <remarks>
    /// 在当前消息中剩余的byte(字节)数，包含可变头部和负荷(内容)。
    /// 单个字节最大值：01111111，16进制：0x7F，10进制为127。
    /// MQTT协议规定，第八位（最高位）若为1，则表示还有后续字节存在。
    /// </remarks>
    public Int32 Length { get; set; }
    #endregion

    #region 构造
    /// <summary>已重载</summary>
    public override String ToString()
    {
        return Type switch
        {
            MqttType.Connect => $"{GetType().Name}[Type={Type}]",
            MqttType.ConnAck or MqttType.Disconnect => $"{GetType().Name}[Type={Type}]",
            MqttType.Publish => $"{GetType().Name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
            MqttType.PubAck or MqttType.PubRec or MqttType.PubRel or MqttType.PubComp => $"{GetType().Name}[Type={Type}]",
            MqttType.Subscribe => $"{GetType().Name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
            MqttType.SubAck or MqttType.UnSubscribe or MqttType.UnSubAck => $"{GetType().Name}[Type={Type}]",
            MqttType.PingReq or MqttType.PingResp => $"{GetType().Name}[Type={Type}]",
            _ => $"{GetType().Name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
        };
    }
    #endregion

    #region 核心读写方法
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Read(Stream stream, Object? context)
    {
        var flag = stream.ReadByte();
        if (flag < 0) return false;

        Type = (MqttType)((flag & 0b1111_0000) >> 4);
        Duplicate = ((flag & 0b0000_1000) >> 3) > 0;
        QoS = (QualityOfService)((flag & 0b0000_0110) >> 1);
        Retain = ((flag & 0b0000_0001) >> 0) > 0;

        Length = stream.ReadEncodedInt();

        if (Length > 0 && stream.CanSeek && stream.Length < Length) throw new InvalidDataException("消息负载的数据长度不足！");

        return OnRead(stream, context);
    }

    /// <summary>子消息读取</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected virtual Boolean OnRead(Stream stream, Object? context) => true;

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    public virtual Boolean Write(Stream stream, Object? context)
    {
        // 子消息先写入，再写头部，因为头部需要负载长度
        var ms = new MemoryStream();
        if (!OnWrite(ms, context)) return false;

        var flag = GetFlag();

        Length = (Int32)ms.Length;

        stream.Write((Byte)flag);
        stream.WriteEncodedInt(Length);

        ms.Position = 0;
        ms.CopyTo(stream);

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected virtual Byte GetFlag()
    {
        var flag = 0;
        flag |= ((Byte)Type << 4) & 0b1111_0000;
        if (Duplicate) flag |= 0b0000_1000;
        flag |= ((Byte)QoS << 1) & 0b0000_0110;
        if (Retain) flag |= 0b0000_0001;

        return (Byte)flag;
    }

    /// <summary>子消息写入</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected virtual Boolean OnWrite(Stream stream, Object? context) => true;

    /// <summary>消息转为字节数组</summary>
    /// <returns></returns>
    public virtual Byte[] ToArray()
    {
        var ms = new MemoryStream();
        Write(ms, null);
        return ms.ToArray();
    }

    /// <summary>转数据包</summary>
    /// <returns></returns>
    public virtual Packet ToPacket() => ToArray();
    #endregion

    #region 辅助
    /// <summary>是否响应</summary>
    public virtual Boolean Reply =>
        Type is MqttType.ConnAck or
        MqttType.PubAck or
        MqttType.PubRec or
        MqttType.PubComp or
        MqttType.SubAck or
        MqttType.UnSubAck or
        MqttType.PingResp;

    /// <summary>读字符串</summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected String ReadString(Stream stream)
    {
        var len = stream.ReadBytes(2).ToUInt16(0, false);
        return stream.ReadBytes(len).ToStr();
    }

    /// <summary>读字节数组</summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected Byte[] ReadData(Stream stream)
    {
        var len = stream.ReadBytes(2).ToUInt16(0, false);
        return stream.ReadBytes(len);
    }

    /// <summary>写字符串</summary>
    /// <param name="stream"></param>
    /// <param name="value"></param>
    protected void WriteString(Stream stream, String? value) => WriteData(stream, value?.GetBytes());

    /// <summary>写字节数组</summary>
    /// <param name="stream"></param>
    /// <param name="buf"></param>
    protected void WriteData(Stream stream, Byte[]? buf)
    {
        var len = buf == null ? 0 : buf.Length;
        stream.Write(((UInt16)len).GetBytes(false));
        if (len > 0 && buf != null) stream.Write(buf);
    }

    /// <summary>写字节数组</summary>
    /// <param name="stream"></param>
    /// <param name="pk"></param>
    protected void WriteData(Stream stream, Packet? pk)
    {
        var len = pk == null ? 0 : pk.Total;
        stream.Write(((UInt16)len).GetBytes(false));
        if (len > 0 && pk != null) pk.CopyTo(stream);
    }
    #endregion
}