using NewLife.Buffers;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT;

/// <summary>MQTT工厂</summary>
public class MqttFactory
{
    /// <summary>创建消息</summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public virtual MqttMessage CreateMessage(MqttType type)
    {
        switch (type)
        {
            case MqttType.Connect: return new ConnectMessage();
            case MqttType.ConnAck: return new ConnAck();
            case MqttType.Publish: return new PublishMessage();
            case MqttType.PubAck: return new PubAck();
            case MqttType.PubRec: return new PubRec();
            case MqttType.PubRel: return new PubRel();
            case MqttType.PubComp: return new PubComp();
            case MqttType.Subscribe: return new SubscribeMessage();
            case MqttType.SubAck: return new SubAck();
            case MqttType.UnSubscribe: return new UnsubscribeMessage();
            case MqttType.UnSubAck: return new UnsubAck();
            case MqttType.PingReq: return new PingRequest();
            case MqttType.PingResp: return new PingResponse();
            case MqttType.Disconnect: return new DisconnectMessage();
            case MqttType.Auth: return new AuthMessage();
            default:
                break;
        }

        throw new NotSupportedException($"{type}");
        //return null;
    }

    /// <summary>读取已定界的帧消息。固定头与帧体分离，由帧层定界后调用</summary>
    /// <param name="frame">帧消息</param>
    /// <param name="protocolLevel">协议版本</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">帧体尚未物化。会话必须启用 MatchQueue，否则跨轮到达的帧体会被绑定为流式体</exception>
    public virtual MqttMessage? ReadMessage(MqttFrame frame, MqttVersion protocolLevel)
    {
        var body = frame.Payload;
        if (body == null) throw new InvalidOperationException($"帧[{frame}]缺少帧体，会话必须启用 MatchQueue 以确保帧体在交付前物化");

        return ReadMessage(frame.FirstByte, body, protocolLevel);
    }

    /// <summary>读取已定界的帧。固定头字节 + 帧体（不含固定头）</summary>
    /// <param name="flag">固定头首字节</param>
    /// <param name="body">帧体（可变头部+负载）</param>
    /// <param name="protocolLevel">协议版本</param>
    /// <returns></returns>
    public virtual MqttMessage? ReadMessage(Byte flag, IPacket body, MqttVersion protocolLevel)
    {
        try
        {
            var msg = CreateMessage((MqttType)(flag >> 4));
            if (!msg.ReadBody(body, flag, protocolLevel)) return null;

            return msg;
        }
        catch (Exception ex)
        {
            // 解析数据异常时，把数据记录到埋点，方便分析异常指令
            DefaultSpan.Current?.SetTag(body);
            if (ex is not XException) DefaultSpan.Current?.SetError(ex, body);

            throw;
        }
    }

    /// <summary>读取消息</summary>
    /// <param name="pk"></param>
    /// <returns></returns>
    public virtual MqttMessage? ReadMessage(IPacket pk) => ReadMessage(pk, MqttVersion.V311);

    /// <summary>读取消息（支持 MQTT 5.0 属性）</summary>
    /// <param name="pk">数据包</param>
    /// <param name="protocolLevel">协议版本</param>
    /// <returns></returns>
    public virtual MqttMessage? ReadMessage(IPacket pk, MqttVersion protocolLevel)
    {
        try
        {
            // 多段链包先聚合为连续包：SpanReader 对链式输入退化为流式分块缓冲，
            // 其 Available 只反映已缓冲分块，会让下方“数据长度不足”校验误报并截断负载
            pk = MqttMessage.EnsureContiguous(pk);

            var msg = CreateMessage((MqttType)(pk[0] >> 4));
            // 使用 SpanReader 直接从数据包读取，避免创建 MemoryStream
            Object? ctx = protocolLevel >= MqttVersion.V500 ? (Object)protocolLevel : null;
            var reader = new SpanReader(pk) { IsLittleEndian = false };
            if (!msg.Read(ref reader, ctx)) return null;

            return msg;
        }
        catch (Exception ex)
        {
            // 解析数据异常时，把数据记录到埋点，方便分析异常指令
            DefaultSpan.Current?.SetTag(pk);
            if (ex is not XException) DefaultSpan.Current?.SetError(ex, pk);

            throw;
        }
    }
}