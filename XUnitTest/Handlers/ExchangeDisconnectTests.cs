#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>管理 API 断开客户端：断开必须是“关连接”，而不是只清交换机登记（否则客户端仍在线却收不到消息）</summary>
[Collection("IntegrationTests")]
public class ExchangeDisconnectTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public ExchangeDisconnectTests()
    {
        // 独立容器：避免解析到其他用例注册进全局 ObjectContainer.Current 的共享处理器（Session 会被后续连接覆写导致投递错连）
        var services = new ObjectContainer();
        services.AddSingleton(XTrace.Log);

        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();

        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateClient(String clientId, Boolean reconnect = false) => new()
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId,
        Timeout = 5000,
        Reconnect = reconnect,
        InitialReconnectDelay = 200,
    };

    private static async Task<Boolean> WaitUntil(Func<Boolean> condition, Int32 milliseconds = 8000)
    {
        for (var i = 0; i < milliseconds / 50; i++)
        {
            if (condition()) return true;

            await Task.Delay(50);
        }

        return condition();
    }

    [Fact(DisplayName = "管理断开_客户端连接真正关闭并在交换机注销")]
    public async Task DisconnectClient_ClosesConnection()
    {
        var clientId = $"kick_{Rand.NextString(4)}";
        using var client = CreateClient(clientId);
        await client.ConnectAsync();
        await client.SubscribeAsync("kick/topic");

        var exchange = (MqttExchange)_server.Exchange!;
        Assert.Equal(1, exchange.Stats.ConnectedClients);

        Assert.True(exchange.DisconnectClient(clientId, "测试断开"));

        Assert.True(await WaitUntil(() => !client.IsConnected), "客户端应在超时内感知连接已断开");
        Assert.True(await WaitUntil(() => exchange.Stats.ConnectedClients == 0), "服务端应完成会话注销");
    }

    [Fact(DisplayName = "管理断开_开启重连的客户端自动重连并恢复订阅")]
    public async Task DisconnectClient_WithReconnect_AutoReconnects()
    {
        var clientId = $"kick_re_{Rand.NextString(4)}";
        using var client = CreateClient(clientId, reconnect: true);

        var connects = 0;
        client.Connected += (s, e) => Interlocked.Increment(ref connects);

        await client.ConnectAsync();
        await client.SubscribeAsync("kick/reconnect");
        await Task.Delay(200);

        Assert.True(((MqttExchange)_server.Exchange!).DisconnectClient(clientId, "测试断开"));

        Assert.True(await WaitUntil(() => connects >= 2, 10000), $"客户端应自动重连（当前连接次数 {connects}）");

        // 重连后订阅自动恢复：另一客户端发布应能送达
        using var pub = CreateClient($"kick_pub_{Rand.NextString(4)}");
        await pub.ConnectAsync();

        var got = new TaskCompletionSource<String?>();
        client.Received += (s, e) => got.TrySetResult(e.Arg.Payload?.ToStr());
        await Task.Delay(200);
        await pub.PublishAsync("kick/reconnect", "after-kick");

        Assert.True(await Task.WhenAny(got.Task, Task.Delay(10000)) == got.Task, "重连后应恢复订阅并收到消息");
        Assert.Equal("after-kick", got.Task.Result);
    }
}
