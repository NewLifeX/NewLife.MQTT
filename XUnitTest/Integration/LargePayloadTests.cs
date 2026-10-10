#nullable enable
using System;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using NewLife.Data;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>大载荷端到端测试：载荷超过接收缓冲（默认 8KB）时帧体跨多轮到达，
/// v12 消息泵把体绑定为流式体（头部先行交付），会话须经匹配队列在交付前物化为内存体，供同步解码</summary>
[Collection("IntegrationTests")]
public class LargePayloadTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public LargePayloadTests()
    {
        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);

        _server = new MqttServer
        {
            Port = 0,
            EnableWebSocket = true,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();

        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateClient(String? clientId = null, String? server = null) => new()
    {
        Log = XTrace.Log,
        Server = server ?? $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"large_{Rand.NextString(6)}",
        Timeout = 15_000,
        Reconnect = false,
    };

    [Theory(DisplayName = "大载荷_QoS0_跨轮帧体完整往返")]
    [InlineData(20_000)]      // 超过单轮接收缓冲（8KB），帧体跨轮到达
    [InlineData(200_000)]
    [InlineData(1_000_000)]   // 1MB：多轮 + 物化后重新编码投递
    public async Task LargePayload_QoS0_RoundTrip(Int32 size)
    {
        using var pub = CreateClient("large_pub0");
        using var sub = CreateClient("large_sub0");
        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var topic = $"large/qos0/{size}";
        var payload = Rand.NextBytes(size);
        var received = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToArray() ?? []);

        await sub.SubscribeAsync(topic);
        await Task.Delay(200);

        await pub.PublishAsync(topic, new ArrayPacket(payload), QualityOfService.AtMostOnce);

        var got = await Task.WhenAny(received.Task, Task.Delay(15_000)) == received.Task
            ? received.Task.Result
            : throw new TimeoutException($"QoS0 {size} 字节载荷未在超时内到达");

        Assert.Equal(payload.Length, got.Length);
        Assert.Equal(payload, got);
    }

    [Fact(DisplayName = "大载荷_QoS1_跨轮帧体完整往返并收到PUBACK")]
    public async Task LargePayload_QoS1_RoundTrip()
    {
        using var pub = CreateClient("large_pub1");
        using var sub = CreateClient("large_sub1");
        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var topic = "large/qos1";
        var payload = Rand.NextBytes(200_000);
        var received = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToArray() ?? []);

        await sub.SubscribeAsync(topic);
        await Task.Delay(200);

        var ack = await pub.PublishAsync(topic, new ArrayPacket(payload), QualityOfService.AtLeastOnce);
        Assert.NotNull(ack);

        var got = await Task.WhenAny(received.Task, Task.Delay(15_000)) == received.Task
            ? received.Task.Result
            : throw new TimeoutException("QoS1 大载荷未在超时内到达");

        Assert.Equal(payload.Length, got.Length);
        Assert.Equal(payload, got);
    }

    [Fact(DisplayName = "大载荷_WebSocket桥接_跨轮帧体完整往返")]
    public async Task LargePayload_WebSocket_RoundTrip()
    {
        var wsServer = $"ws://127.0.0.1:{_server.WebSocketPort}{_server.WebSocketPath}";
        using var pub = CreateClient("large_wspub", wsServer);
        using var sub = CreateClient("large_wssub", wsServer);
        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var topic = "large/ws";
        var payload = Rand.NextBytes(200_000);
        var received = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToArray() ?? []);

        await sub.SubscribeAsync(topic);
        await Task.Delay(200);

        await pub.PublishAsync(topic, new ArrayPacket(payload), QualityOfService.AtMostOnce);

        var got = await Task.WhenAny(received.Task, Task.Delay(15_000)) == received.Task
            ? received.Task.Result
            : throw new TimeoutException("WebSocket 大载荷未在超时内到达");

        Assert.Equal(payload.Length, got.Length);
        Assert.Equal(payload, got);
    }
}
