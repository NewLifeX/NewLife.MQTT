using System.Text;
using NewLife.Buffers;
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
public abstract class MqttMessage : ISpanSerializable
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
        var name = GetType().Name;
        return Type switch
        {
            MqttType.Connect => $"{name}[Type={Type}]",
            MqttType.ConnAck or MqttType.Disconnect => $"{name}[Type={Type}]",
            MqttType.Publish => $"{name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
            MqttType.PubAck or MqttType.PubRec or MqttType.PubRel or MqttType.PubComp => $"{name}[Type={Type}]",
            MqttType.Subscribe => $"{name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
            MqttType.SubAck or MqttType.UnSubscribe or MqttType.UnSubAck => $"{name}[Type={Type}]",
            MqttType.PingReq or MqttType.PingResp => $"{name}[Type={Type}]",
            _ => $"{name}[Type={Type}, QoS={(Int32)QoS}, Duplicate={Duplicate}, Retain={Retain}]",
        };
    }
    #endregion

    #region 核心读写方法
    /// <summary>从SpanReader反序列化读取消息</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Read(ref SpanReader reader, Object? context)
    {
        var flag = reader.ReadByte();

        Type = (MqttType)((flag & 0b1111_0000) >> 4);
        Duplicate = ((flag & 0b0000_1000) >> 3) > 0;
        QoS = (QualityOfService)((flag & 0b0000_0110) >> 1);
        Retain = ((flag & 0b0000_0001) >> 0) > 0;

        var len = Length = reader.ReadEncodedInt();
        if (len > 0 && reader.Available < len) throw new InvalidDataException("消息负载的数据长度不足！");

        return OnRead(ref reader, context);
    }

    /// <summary>子消息读取</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected virtual Boolean OnRead(ref SpanReader reader, Object? context) => true;

    /// <summary>将消息序列化写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Write(ref SpanWriter writer, Object? context)
    {
        // 使用池化缓冲区写入消息体，避免 GC 分配，且不需要移位处理变长头部
        using var pk = new OwnerPacket(GetEstimatedBodySize());
        var bodyWriter = new SpanWriter(pk) { IsLittleEndian = false };
        if (!OnWrite(ref bodyWriter, context)) return false;

        var len = Length = bodyWriter.WrittenCount;

        // 写固定头：标记位 + 变长长度 + 消息体
        writer.WriteByte(GetFlag());
        writer.WriteEncodedInt(len);
        if (len > 0) writer.Write(bodyWriter.WrittenSpan);

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
    /// <param name="writer">Span写入器</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected virtual Boolean OnWrite(ref SpanWriter writer, Object? context) => true;

    /// <summary>获取子消息体估算大小，子类可覆盖以优化缓冲区分配</summary>
    /// <returns></returns>
    protected virtual Int32 GetEstimatedBodySize() => 256;

    /// <summary>获取消息总估算大小（含固定头部），用于缓冲区预分配</summary>
    /// <returns></returns>
    public Int32 GetEstimatedSize() => 5 + GetEstimatedBodySize();

    /// <summary>转数据包</summary>
    /// <returns></returns>
    public virtual IOwnerPacket ToPacket()
    {
        var pk = new OwnerPacket(GetEstimatedSize());
        var writer = new SpanWriter(pk) { IsLittleEndian = false };
        Write(ref writer, null);
        pk.Resize(writer.WrittenCount);
        return pk;
    }
    #endregion

    #region ISpanSerializable
    /// <summary>从SpanReader反序列化读取</summary>
    /// <param name="reader">Span读取器</param>
    void ISpanSerializable.Read(ref SpanReader reader) => Read(ref reader, null);

    /// <summary>将对象成员序列化写入SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    void ISpanSerializable.Write(ref SpanWriter writer) => Write(ref writer, null);
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

    /// <summary>读字符串（2字节大端长度前缀 + UTF-8数据）</summary>
    /// <param name="reader">Span读取器</param>
    /// <returns></returns>
    protected String ReadString(ref SpanReader reader)
    {
        var len = reader.ReadUInt16();
        if (len == 0) return String.Empty;
        return reader.ReadString(len);
    }

    /// <summary>读字节数组（2字节大端长度前缀 + 数据）</summary>
    /// <param name="reader">Span读取器</param>
    /// <returns></returns>
    protected Byte[] ReadData(ref SpanReader reader)
    {
        var len = reader.ReadUInt16();
        return reader.ReadBytes(len).ToArray();
    }

    /// <summary>写字符串（2字节大端长度前缀 + UTF-8数据）</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="value">字符串值</param>
    protected void WriteString(ref SpanWriter writer, String? value)
    {
        if (value.IsNullOrEmpty())
        {
            writer.Write((UInt16)0);
            return;
        }

        // 直接计算字节数写入长度前缀，再用 Write(String, -1) 零分配写入 UTF-8 数据
        var byteCount = Encoding.UTF8.GetByteCount(value);
        writer.Write((UInt16)byteCount);
        writer.Write(value, -1);
    }

    /// <summary>写字节数组（2字节大端长度前缀 + 数据）</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="buf">字节数组</param>
    protected void WriteData(ref SpanWriter writer, Byte[]? buf)
    {
        var len = buf == null ? 0 : buf.Length;
        writer.Write((UInt16)len);
        if (len > 0 && buf != null) writer.Write(buf);
    }

    /// <summary>写数据包（2字节大端长度前缀 + 数据）</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="pk">数据包</param>
    protected void WriteData(ref SpanWriter writer, IPacket? pk)
    {
        var len = pk == null ? 0 : pk.Total;
        writer.Write((UInt16)len);
        if (len > 0 && pk != null)
        {
            var span = pk.GetSpan();
            writer.Write(span);
        }
    }
    #endregion
}