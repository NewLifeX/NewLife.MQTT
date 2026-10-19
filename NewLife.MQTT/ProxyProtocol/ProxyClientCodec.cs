using System.Buffers;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Net;

namespace NewLife.MQTT.ProxyProtocol;

/// <summary>客户端 PROXY 协议前缀编解码器。为每个发送帧追加 PROXY 头，兼容旧 ProxyCodec 客户端行为</summary>
/// <remarks>
/// v12 协议泵下编解码器无法访问会话上下文，因此本装饰器由客户端在建立连接时注入会话引用，用于取本地/远程地址构造代理头。
/// </remarks>
public class ProxyClientCodec : IMessageCodec, IMessageCodecDecorator
{
    /// <summary>内层协议</summary>
    public IMessageCodec Inner { get; }

    /// <summary>会话。用于取本地/远程地址构造代理头</summary>
    public ISocketRemote? Session { get; set; }

    /// <summary>实例化客户端代理编解码器</summary>
    /// <param name="inner">内层协议</param>
    public ProxyClientCodec(IMessageCodec inner) => Inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>定界。客户端接收方向不识别代理头，直接交给内层</summary>
    /// <param name="buffer">帧首窗口</param>
    /// <returns></returns>
    public ParseResult? TryParse(ReadOnlySequence<Byte> buffer) => Inner.TryParse(buffer);

    /// <summary>构建整帧：在内层整帧前追加 PROXY 头</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public IOwnerPacket? Build(IMessage message)
    {
        var frame = Inner.Build(message);
        if (frame == null) return null;

        var header = BuildProxyHeader();
        if (header == null) return frame;

        var pk = new OwnerPacket(header.Length + frame.Total);
        var writer = new SpanWriter(pk);
        writer.Write(header);
        writer.Write(frame);
        pk.Resize(writer.WrittenCount);

        frame.TryDispose();

        return pk;
    }

    /// <summary>头部构建。代理前缀依赖整帧，不支持分段发送</summary>
    /// <param name="message">消息</param>
    /// <param name="bodyLength">消息体字节数</param>
    /// <returns></returns>
    public IOwnerPacket BuildHeader(IMessage message, Int64 bodyLength) => throw new NotSupportedException(L.T("The PROXY prefix requires a complete frame; sending header and body separately is not supported."));

    private Byte[]? BuildProxyHeader()
    {
        var msg = BuildProxyMessage(Session);

        return msg?.ToPacket().GetBytes();
    }

    /// <summary>按会话地址构建 PROXY v1 代理头。会话为空时返回null</summary>
    /// <param name="session">会话</param>
    /// <returns></returns>
    public static Byte[]? BuildProxyHeader(ISocketRemote? session)
    {
        var msg = BuildProxyMessage(session);

        return msg?.ToPacket().GetBytes();
    }

    private static ProxyMessage? BuildProxyMessage(ISocketRemote? session)
    {
        if (session == null) return null;

        return new ProxyMessage
        {
            Protocol = "TCP4",
            ClientIP = session.Local.Address + "",
            ClientPort = session.Local.Port,
            ProxyIP = session.Remote.Address + "",
            ProxyPort = session.Remote.Port,
        };
    }
}
