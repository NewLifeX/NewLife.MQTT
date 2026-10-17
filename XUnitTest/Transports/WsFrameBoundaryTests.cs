#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Model;
using NewLife.Net;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>WebSocket 帧边界测试。一条 WS 消息可能承载半个或多个 MQTT 帧，两种边界都必须正确恢复</summary>
public class WsFrameBoundaryTests
{
    private static MqttServer CreateServer() => new()
    {
        Port = 0,
        EnableWebSocket = true,
        ServiceProvider = new ObjectContainer().BuildServiceProvider(),
        Log = XTrace.Log,
        SessionLog = XTrace.Log,
    };

    private static async Task<WebSocketClient> ConnectWebSocket(MqttServer server)
    {
        var ws = new WebSocketClient(new Uri($"ws://127.0.0.1:{server.WebSocketPort}{server.WebSocketPath}"))
        {
            RequestHeaders = new Dictionary<String, String?> { ["Sec-WebSocket-Protocol"] = "mqtt" },
            Log = XTrace.Log,
            Timeout = 10_000,
        };

        await ws.OpenAsync();

        return ws;
    }

    [Fact(DisplayName = "WS 帧边界：帧拆到两条消息时仍能定界并收到CONNACK")]
    public async Task SplitConnectFrame_ReceivesConnAck()
    {
        using var server = CreateServer();
        server.Start();

        using var ws = await ConnectWebSocket(server);

        var connect = new ConnectMessage
        {
            ClientId = "ws_split",
            CleanSession = true,
            KeepAliveInSeconds = 60,
        };

        using var pk = connect.ToPacket();
        var data = pk.ToArray();
        var half = data.Length / 2;

        // 前半帧与后半帧分两条 WS 消息发送，服务端必须保留残余并在第二轮拼接后定界
        await ws.SendBinaryAsync(new ArrayPacket(data, 0, half), default);
        await Task.Delay(100);
        await ws.SendBinaryAsync(new ArrayPacket(data, half, data.Length - half), default);

        var reply = await ws.ReceiveMessageAsync();
        Assert.NotNull(reply);
        Assert.Equal(WebSocketMessageType.Binary, reply.Type);

        var ack = new MqttFactory().ReadMessage(reply.Payload!) as ConnAck;
        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
    }

    [Fact(DisplayName = "WS 帧边界：一条消息承载多帧时逐帧处理")]
    public async Task MultipleFramesInOneMessage_AllProcessed()
    {
        using var server = CreateServer();
        server.Start();

        using var ws = await ConnectWebSocket(server);

        using var pk = new ConnectMessage { ClientId = "ws_multi", CleanSession = true }.ToPacket();
        await ws.SendBinaryAsync(new ArrayPacket(pk.ToArray()), default);

        var ack = await ws.ReceiveMessageAsync();
        Assert.NotNull(ack);
        Assert.IsType<ConnAck>(new MqttFactory().ReadMessage(ack.Payload!));

        // 一条 WS 消息内串联两个 PINGREQ，服务端应逐个处理并返回两个 PINGRESP
        using var ping = new PingRequest().ToPacket();
        var bytes = ping.ToArray();
        var two = new Byte[bytes.Length * 2];
        bytes.CopyTo(two, 0);
        bytes.CopyTo(two, bytes.Length);

        await ws.SendBinaryAsync(new ArrayPacket(two), default);

        var reply1 = await ws.ReceiveMessageAsync();
        Assert.NotNull(reply1);
        Assert.IsType<PingResponse>(new MqttFactory().ReadMessage(reply1.Payload!));

        var reply2 = await ws.ReceiveMessageAsync();
        Assert.NotNull(reply2);
        Assert.IsType<PingResponse>(new MqttFactory().ReadMessage(reply2.Payload!));
    }
}
