#nullable enable
using System;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.Model;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>WebSocket 会话生命周期测试。桥接会话必须走 NetSession 标准启动流程，断开后才能从交换机注销</summary>
[Collection("IntegrationTests")]
public class WsSessionLifecycleTests
{
    private static MqttServer CreateServer() => new()
    {
        Port = 0,
        EnableWebSocket = true,
        ServiceProvider = new ObjectContainer().BuildServiceProvider(),
        Log = XTrace.Log,
        SessionLog = XTrace.Log,
    };

    private static MqttClient CreateClient(MqttServer server) => new()
    {
        Log = XTrace.Log,
        Server = $"ws://127.0.0.1:{server.WebSocketPort}{server.WebSocketPath}",
        ClientId = $"ws_life_{Rand.NextString(4)}",
        Timeout = 10_000,
        Reconnect = false,
    };

    /// <summary>轮询等待在线数归零，避免依赖固定延时</summary>
    private static async Task<Int32> WaitForOffline(MqttServer server, Int32 milliseconds = 5000)
    {
        var exchange = server.Exchange!;
        var count = exchange.Stats.ConnectedClients;
        for (var i = 0; i < milliseconds / 100; i++)
        {
            if (count <= 0) break;

            await Task.Delay(100);
            count = exchange.Stats.ConnectedClients;
        }

        return count;
    }

    [Fact(DisplayName = "WS：正常断开（DISCONNECT）后会话从交换机注销")]
    public async Task WsSession_NormalDisconnect_RemovedFromExchange()
    {
        using var server = CreateServer();
        server.Start();

        using var client = CreateClient(server);
        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);

        await client.SubscribeAsync($"ws/life/{Rand.NextString(4)}");
        Assert.Equal(1, server.Exchange!.Stats.ConnectedClients);

        await client.DisconnectAsync();

        Assert.Equal(0, await WaitForOffline(server));
    }

    [Fact(DisplayName = "WS：客户端强制断开后会话从交换机注销")]
    public async Task WsSession_AbnormalDisconnect_RemovedFromExchange()
    {
        using var server = CreateServer();
        server.Start();

        var client = CreateClient(server);
        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);

        await client.SubscribeAsync($"ws/life/{Rand.NextString(4)}");
        Assert.Equal(1, server.Exchange!.Stats.ConnectedClients);

        // 直接释放客户端，不发送 DISCONNECT，模拟设备掉线
        client.Dispose();

        Assert.Equal(0, await WaitForOffline(server));
    }
}
