using System.Buffers;
using System.Runtime.CompilerServices;
using NewLife.Data;
using NewLife.Http;
using NewLife.Messaging;
using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MQTT.Http;

/// <summary>MQTT over WebSocket 桥接处理器。每条 WebSocket 连接桥接一个 <see cref="MqttSession"/>，二进制消息承载 MQTT 帧</summary>
/// <remarks>
/// <para>v12 起 MQTT 服务端不再用管线编解码器承载 WebSocket，改由独立 <see cref="HttpServer"/> 承载本处理器：
/// 接收方向把 WS 负载定界为 MQTT 帧送入会话，发送方向把会话产出的帧写成 WS 二进制消息。</para>
/// <para>由于没有协议泵，请求-响应配对由 <see cref="MqttSession"/> 自行完成，见 <see cref="MqttSession.SendMessageAsync(MqttMessage, Boolean, CancellationToken)"/>。</para>
/// </remarks>
public class MqttWebSocketHandler : WebSocketHandler
{
    private readonly MqttServer _server;
    private readonly MqttCodec _codec = new();
    private readonly ConditionalWeakTable<WebSocket, MqttSession> _sessions = new();
    private readonly ConditionalWeakTable<WebSocket, MqttFrameDeframer> _deframers = new();

    /// <summary>实例化</summary>
    /// <param name="server">MQTT 服务端</param>
    public MqttWebSocketHandler(MqttServer server) => _server = server ?? throw new ArgumentNullException(nameof(server));

    /// <summary>处理请求。客户端声明 mqtt 子协议时回写</summary>
    /// <param name="context">Http 上下文</param>
    /// <returns>处理任务</returns>
    public override Task ProcessRequestAsync(IHttpContext context)
    {
        var task = base.ProcessRequestAsync(context);

        // MQTT over WebSocket 子协议约定：仅在客户端声明 mqtt 时回写，未声明则保持原样
        var protocols = context.Request.Headers["Sec-WebSocket-Protocol"];
        if (!protocols.IsNullOrEmpty() && protocols.Split(',').Any(e => e.Trim().EqualIgnoreCase("mqtt")))
            context.Response.Headers["Sec-WebSocket-Protocol"] = "mqtt";

        return task;
    }

    /// <summary>处理消息。二进制负载定界为 MQTT 帧后送入会话</summary>
    /// <param name="socket">WebSocket 会话</param>
    /// <param name="message">消息</param>
    public override void ProcessMessage(WebSocket socket, WsMessage message)
    {
        if (message.Type == WebSocketMessageType.Close)
        {
            if (_sessions.TryGetValue(socket, out var closed)) closed.Close(L.T("WebSocket closed"));

            base.ProcessMessage(socket, message);
            return;
        }

        if (message.Type != WebSocketMessageType.Binary || message.Payload == null || message.Payload.Total == 0)
        {
            base.ProcessMessage(socket, message);
            return;
        }

        var session = _sessions.GetValue(socket, e => CreateSession(e));

        // 一条 WS 消息可能承载半个 MQTT 帧，残余由定界器保留到下一轮拼接后定界
        var deframer = _deframers.GetValue(socket, e => new MqttFrameDeframer());
        if (!deframer.Process(_codec, message.Payload, frame => session.Process(frame)) && deframer.Overflow)
        {
            _server.WriteLog("WebSocket [{0}] packet residue exceeded the limit; closing the connection", session.SessionId);
            socket.Close(1009, L.T("MQTT packet size exceeded"));
        }
    }

    /// <summary>为 WebSocket 连接创建 MQTT 会话。底层传输会话用于会话标识与生命周期，发送走 WS 二进制消息</summary>
    /// <param name="socket">WebSocket 会话</param>
    /// <returns></returns>
    private MqttSession CreateSession(WebSocket socket)
    {
        var context = socket.Context;
        var transport = context?.Connection?.Session ?? context?.Socket as ISocketSession;
        var session = new MqttSession();

        if (!session.AttachWebSocket(_server, transport, pk =>
        {
            var data = _codec.Build(pk)!;
            try
            {
                var total = data.Total;
                socket.Send(data, WebSocketMessageType.Binary);
                return total;
            }
            finally { data.TryDispose(); }
        }))
        {
            socket.Close(1008, L.T("Connection rate limit exceeded"));
            return session;
        }

        // 底层连接断开时释放会话，触发处理器关闭与交换机注销
        if (transport != null) transport.OnDisposed += (s, e) => session.Close(L.T("WebSocket connection closed"));

        return session;
    }
}
