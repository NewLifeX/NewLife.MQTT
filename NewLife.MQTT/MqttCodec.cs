using System.Buffers;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.ProxyProtocol;
using NewLife;

namespace NewLife.MQTT;

/// <summary>MQTT 帧编解码器。在 v12 消息泵上定界 MQTT 帧：解析固定头（类型+标志+变长剩余长度），帧体交由泵绑定</summary>
/// <remarks>
/// <para>无状态、可跨连接共享；MQTT 协议版本属于连接态，解码由会话层用连接版本调用 <see cref="MqttFactory.ReadMessage(MqttFrame, MqttVersion)"/>。</para>
/// <para>帧体跨轮到达时由泵绑定为流式体，会话需启用匹配队列（<c>SessionBase.MatchQueue</c>）以确保交付前物化为内存，供同步解码。</para>
/// <para>服务端可识别并剥离前置代理（nginx/haproxy）写入的 PROXY 协议头，定界为 <see cref="MqttProxyFrame"/> 交由会话层修正客户端地址。</para>
/// </remarks>
public class MqttCodec : IMessageCodec, IMessageMatcher
{
    /// <summary>剩余长度上限。MQTT 规范 268435455（约256MB）</summary>
    public const Int32 MaxRemainingLength = 268_435_455;

    /// <summary>识别并剥离 PROXY 协议头。默认true，用于前置代理场景</summary>
    public Boolean AcceptProxyProtocol { get; set; } = true;

    private static readonly Byte[] _ProxyMagic = "PROXY ".GetBytes();

    #region 定界
    /// <summary>定界并构造帧消息。固定头不足返回null；剩余长度非法返回损坏帧</summary>
    /// <param name="buffer">帧首窗口（只读序列，可跨段）</param>
    /// <returns></returns>
    public virtual ParseResult? TryParse(ReadOnlySequence<Byte> buffer)
    {
        if (AcceptProxyProtocol && TryParseProxy(buffer, out var proxy)) return proxy;

        return TryParseMqtt(buffer);
    }

    private static ParseResult? TryParseMqtt(in ReadOnlySequence<Byte> buffer)
    {
        // 固定头：1字节类型/标志 + 1~4字节变长剩余长度
        if (buffer.Length < 2) return null;

        var reader = new SequenceReader<Byte>(buffer);
        reader.TryRead(out var flag);

        var remaining = 0;
        var multiplier = 1;
        var len = 0;
        while (true)
        {
            // 变长长度最多4字节，第4字节仍带延续位即为损坏
            if (len == 4) return new ParseResult { Invalid = true };
            if (!reader.TryRead(out var b)) return null;
            len++;

            remaining += (b & 0x7F) * multiplier;
            if ((b & 0x80) == 0) break;
            multiplier *= 128;
        }

        if (remaining > MaxRemainingLength) return new ParseResult { Invalid = true };

        var frame = new MqttFrame
        {
            FirstByte = flag,
            RemainingLength = remaining,
            Kind = MqttFrame.GetKind(flag),
        };

        return new ParseResult { Message = frame, HeaderSize = 1 + len, BodyLength = remaining };
    }

    /// <summary>尝试把当前位置定界为 PROXY 协议头</summary>
    /// <param name="buffer">帧首窗口</param>
    /// <param name="result">定界结果；<paramref name="handled"/> 为 true 时有效</param>
    /// <returns>是否已处理该位置（含"检测到代理头但未到齐"的等待语义）</returns>
    private static Boolean TryParseProxy(in ReadOnlySequence<Byte> buffer, out ParseResult? result)
    {
        result = null;
        if (buffer.Length == 0) return false;

        var first = buffer.Slice(0, 1).First.Span[0];

        // v1：文本行 "PROXY ...\r\n"
        if (first == (Byte)'P')
        {
            if (!IsMagicPrefix(buffer, _ProxyMagic)) return false;

            var span = GetPrefix(buffer, 6 + 65536);
            var msg = new ProxyMessage();
            var n = msg.Read(span);
            if (n > 0)
            {
                result = new ParseResult
                {
                    Message = new MqttProxyFrame { V1 = msg, Kind = MessageKinds.OneWay },
                    HeaderSize = 0,
                    BodyLength = n,
                };
                return true;
            }

            // 行未到齐则等待；已达上限仍未闭合视为损坏
            if (buffer.Length <= 6 + 65536) return true;

            result = new ParseResult { Invalid = true };
            return true;
        }

        // v2：二进制魔数 + 4字节头 + 可变长度体
        if (first == (Byte)'\r')
        {
            if (!IsMagicPrefix(buffer, ProxyMessageV2.Magic)) return false;

            var span = GetPrefix(buffer, 16 + 65536);
            if (span.Length < 16) return true;      // 固定头未到齐

            var total = 16 + ((span[14] << 8) | span[15]);
            if (buffer.Length < total) return true; // 变长体未到齐

            var head = GetPrefix(buffer, total);
            var msg = new ProxyMessageV2();
            var n = msg.Read(head);
            if (n <= 0)
            {
                result = new ParseResult { Invalid = true };
                return true;
            }

            result = new ParseResult
            {
                Message = new MqttProxyFrame { V2 = msg, Kind = MessageKinds.OneWay },
                HeaderSize = 0,
                BodyLength = total,
            };
            return true;
        }

        return false;
    }

    private static ReadOnlySpan<Byte> GetPrefix(in ReadOnlySequence<Byte> buffer, Int32 max)
    {
        var len = (Int32)Math.Min(buffer.Length, max);
        var slice = buffer.Slice(0, len);

        return slice.IsSingleSegment ? slice.First.Span : slice.ToArray();
    }

    private static Boolean IsMagicPrefix(in ReadOnlySequence<Byte> buffer, ReadOnlySpan<Byte> magic)
    {
        var n = (Int32)Math.Min(buffer.Length, magic.Length);
        var slice = buffer.Slice(0, n);
        var span = slice.IsSingleSegment ? slice.First.Span : slice.ToArray();

        return span.SequenceEqual(magic[..n]);
    }
    #endregion

    #region 构建
    /// <summary>构建整帧。仅接受携带 MQTT 消息的帧</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public virtual IOwnerPacket? Build(IMessage message)
    {
        if (message is not MqttFrame frame || frame.Mqtt == null) return null;

        return frame.Mqtt.ToPacket(frame.Version);
    }

    /// <summary>头部构建。MQTT 剩余长度需要整帧内容才能确定，不支持头部与帧体分段发送</summary>
    /// <param name="message">消息</param>
    /// <param name="bodyLength">消息体字节数</param>
    /// <returns></returns>
    public virtual IOwnerPacket BuildHeader(IMessage message, Int64 bodyLength) => throw new NotSupportedException(L.T("The MQTT protocol does not support sending headers and bodies separately."));
    #endregion

    #region 配对
    /// <summary>请求-响应配对。沿用既有规则：方向、报文类型相邻或发布确认、标识符相等</summary>
    /// <param name="request">挂起的请求消息</param>
    /// <param name="response">收到的响应消息</param>
    /// <returns></returns>
    public virtual Boolean Match(IMessage request, IMessage response)
    {
        if (request is not MqttFrame req || response is not MqttFrame res) return false;

        // 请求非响应、应答为响应
        if (req.Reply || !res.Reply) return false;

        // 类型匹配：应答类型紧跟请求类型，或发布消息对应 PUBACK/PUBREC
        var reqType = req.Type;
        var resType = res.Type;
        if ((Int32)reqType + 1 != (Int32)resType &&
            !(reqType == MqttType.Publish && (resType == MqttType.PubAck || resType == MqttType.PubRec)))
            return false;

        // 标识符匹配：双方都带标识符时要求相等
        var reqId = req.GetId();
        var resId = res.GetId();
        if (reqId != 0 && resId != 0 && reqId != resId) return false;

        return true;
    }
    #endregion
}
