using System.Security.Cryptography;
using System.Text;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;

namespace NewLife.MQTT.WebSocket;

/// <summary>WebSocket客户端编解码器</summary>
/// <remarks>
/// 用于 MQTT 客户端通过 WebSocket 连接到服务端。
/// 支持 ws:// 和 wss://（需配合 TLS）协议。
/// 负责发送 WebSocket 握手请求、处理握手响应、编解码 WebSocket 帧。
/// </remarks>
public class WebSocketClientCodec : Handler
{
    #region 属性
    /// <summary>是否已完成握手</summary>
    private Boolean _handshaked;

    /// <summary>WebSocket路径</summary>
    public String Path { get; set; } = "/mqtt";

    /// <summary>主机名</summary>
    public String Host { get; set; } = "localhost";

    /// <summary>端口</summary>
    public Int32 Port { get; set; } = 80;

    /// <summary>WebSocket密钥（握手时使用）</summary>
    private String? _wsKey;

    /// <summary>WebSocket GUID，用于握手</summary>
    private const String WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>待发送缓冲区（握手完成前缓存数据）</summary>
    private readonly List<IPacket> _pendingWrites = [];
    #endregion

    #region 方法
    /// <summary>处理收到的数据</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public override Object Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket pk) return base.Read(context, message);

        if (!_handshaked)
        {
            // 尝试解析握手响应
            var response = pk.ToStr();
            if (response.Contains("101") && response.Contains("Upgrade"))
            {
                // 握手成功
                _handshaked = true;

                // 发送握手完成前缓存的数据
                var session = context.Owner as ISocketSession;
                if (session != null)
                {
                    foreach (var pending in _pendingWrites)
                    {
                        var frame = EncodeFrame(pending);
                        session.Send(frame.ToArray());
                    }
                    _pendingWrites.Clear();
                }

                // 握手响应不传递给后续处理器
                return null;
            }

            return null;
        }

        // 解析 WebSocket 帧
        return DecodeFrame(pk) ?? base.Read(context, message);
    }

    /// <summary>处理发送数据——封装为WebSocket帧</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public override Object Write(IHandlerContext context, Object message)
    {
        if (message is not IPacket pk) return base.Write(context, message);

        if (!_handshaked)
        {
            // 检查是否需要发送握手请求
            if (_wsKey == null)
            {
                SendHandshake(context);
            }

            // 尚未握手完成，缓存数据
            _pendingWrites.Add(pk);
            return null;
        }

        // 封装为WebSocket帧（客户端发送需要掩码）
        return EncodeFrame(pk);
    }

    /// <summary>发送 WebSocket 握手请求</summary>
    private void SendHandshake(IHandlerContext context)
    {
        // 生成随机 WebSocket Key
        var keyBytes = new Byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        _wsKey = Convert.ToBase64String(keyBytes);

        // 构建 HTTP 升级请求
        var sb = new StringBuilder();
        sb.Append($"GET {Path} HTTP/1.1\r\n");
        sb.Append($"Host: {Host}:{Port}\r\n");
        sb.Append("Upgrade: websocket\r\n");
        sb.Append("Connection: Upgrade\r\n");
        sb.Append($"Sec-WebSocket-Key: {_wsKey}\r\n");
        sb.Append("Sec-WebSocket-Version: 13\r\n");
        sb.Append("Sec-WebSocket-Protocol: mqtt\r\n");
        sb.Append("\r\n");

        var request = Encoding.UTF8.GetBytes(sb.ToString());
        var session = context.Owner as ISocketSession;
        session?.Send(request);
    }
    #endregion

    #region 辅助方法
    /// <summary>解析WebSocket帧</summary>
    private IPacket? DecodeFrame(IPacket pk)
    {
        var data = pk.GetSpan();
        if (data.Length < 2) return null;

        var opcode = data[0] & 0x0F;
        var mask = (data[1] & 0x80) != 0;
        var payloadLen = data[1] & 0x7F;

        var offset = 2;
        if (payloadLen == 126)
        {
            if (data.Length < 4) return null;
            payloadLen = (data[2] << 8) | data[3];
            offset = 4;
        }
        else if (payloadLen == 127)
        {
            if (data.Length < 10) return null;
            payloadLen = 0;
            for (var i = 0; i < 8; i++)
                payloadLen = (payloadLen << 8) | data[2 + i];
            offset = 10;
        }

        Byte[]? maskKey = null;
        if (mask)
        {
            if (data.Length < offset + 4) return null;
            maskKey = [data[offset], data[offset + 1], data[offset + 2], data[offset + 3]];
            offset += 4;
        }

        if (data.Length < offset + payloadLen) return null;

        var payload = new Byte[payloadLen];
        for (var i = 0; i < payloadLen; i++)
        {
            payload[i] = mask && maskKey != null ? (Byte)(data[offset + i] ^ maskKey[i % 4]) : data[offset + i];
        }

        switch (opcode)
        {
            case 0x1: // 文本帧
            case 0x2: // 二进制帧
                return new ArrayPacket(payload);
            case 0x8: // 关闭帧
                return null;
            case 0x9: // Ping帧 → 回 Pong
                var session = ((IHandlerContext)null)?.Owner as ISocketSession;
                // Pong处理
                return null;
            case 0xA: // Pong帧
                return null;
            default:
                return null;
        }
    }

    /// <summary>封装WebSocket帧（客户端需要掩码）</summary>
    private IPacket EncodeFrame(IPacket pk)
    {
        var data = pk.GetSpan();
        var length = data.Length;

        // 计算帧头长度（含4字节掩码）
        var headerLen = 2 + 4; // FIN+opcode + mask+len + maskkey
        if (length > 125 && length <= 65535)
            headerLen += 2;
        else if (length > 65535)
            headerLen += 8;

        var frame = new Byte[headerLen + length];
        var offset = 0;

        // FIN=1, opcode=0x02(二进制帧)
        frame[offset++] = 0x82;

        // 掩码位=1 + 长度
        if (length <= 125)
        {
            frame[offset++] = (Byte)(0x80 | length);
        }
        else if (length <= 65535)
        {
            frame[offset++] = 0x80 | 126;
            frame[offset++] = (Byte)(length >> 8);
            frame[offset++] = (Byte)(length & 0xFF);
        }
        else
        {
            frame[offset++] = 0x80 | 127;
            for (var i = 7; i >= 0; i--)
                frame[offset++] = (Byte)((length >> (8 * i)) & 0xFF);
        }

        // 生成掩码（客户端必须使用掩码）
        var maskKey = new Byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(maskKey);
        }
        frame[offset++] = maskKey[0];
        frame[offset++] = maskKey[1];
        frame[offset++] = maskKey[2];
        frame[offset++] = maskKey[3];

        // 写入掩码后的数据
        for (var i = 0; i < length; i++)
        {
            frame[offset + i] = (Byte)(data[i] ^ maskKey[i % 4]);
        }

        return new ArrayPacket(frame);
    }
    #endregion
}
