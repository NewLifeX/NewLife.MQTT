using NewLife.Data;
using NewLife.Http;
using NewLife.Model;
using NewLife.Net;
using WebSocket = NewLife.Http.WebSocket;
using WebSocketMessageType = NewLife.Http.WebSocketMessageType;

namespace NewLife.MQTT.Http;

/// <summary>WebSocket消息编码器</summary>
public class WebSocketServerCodec : Handler
{
    /// <summary>连接关闭时，清空粘包编码器</summary>
    /// <param name="context"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend ss) ss["_websocket"] = null;

        return base.Close(context, reason);
    }

    /// <summary>读取数据</summary>
    /// <param name="context"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket pk) return base.Read(context, message);
        if (context.Owner is not IExtend ss) return base.Read(context, message);

        // 连接必须是ws/wss协议
        if (context.Owner is not ISocketRemote session || session.Remote.Type != NetType.Tcp) return base.Read(context, message);

        // 如果是websocket，第一个包必须是握手
        var isWs = ss["isWs"] as Boolean?;
        if (isWs != null && !isWs.Value) return base.Read(context, message);

        var websocket = ss["_websocket"] as WebSocket;
        if (websocket == null)
        {
            var request = new HttpRequest();
            if (request.Parse(pk) && request.IsCompleted)
            {
                var ctx = new DefaultHttpContext(session, request, null!, null)
                {
                    ServiceProvider = session as IServiceProvider
                };

                // 处理 WebSocket 握手
                websocket = WebSocket.Handshake(ctx);
                if (websocket != null)
                {
                    var rs = ctx.Response;
                    if (rs != null)
                    {
                        rs.Headers["Sec-WebSocket-Protocol"] = "mqtt";
                        rs.Headers = rs.Headers.ToDictionary(e => e.Key.ToLower(), e => e.Value, StringComparer.OrdinalIgnoreCase);

                        session.Send(rs.Build());

                        ss["_websocket"] = websocket;
                        ss["isWs"] = true;

                        // 禁止向后传递
                        return null;
                    }
                }
            }
        }
        if (websocket != null)
        {
            var msg = new WebSocketMessage();
            if (msg.Read(pk)) message = msg.Payload!;
        }
        else
            ss["isWs"] = false;

        try
        {
            return base.Read(context, message);
        }
        finally { message.TryDispose(); }
    }

    /// <summary>发送消息时，写入数据</summary>
    /// <param name="context"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        // 仅编码websocket连接
        if (context.Owner is IExtend ss && ss["_websocket"] is WebSocket)
            if (message is IPacket pk)
            {
                var msg = new WebSocketMessage
                {
                    Type = WebSocketMessageType.Binary,
                    Payload = pk,
                };
                message = msg.ToPacket();
            }

        try
        {
            return base.Write(context, message);
        }
        finally { message.TryDispose(); }
    }
}
