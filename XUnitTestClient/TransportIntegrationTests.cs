using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTestClient;

/// <summary>多传输协议集成测试。验证 TCP/WebSocket 传输 × MQTT V311/V500 版本组合下连接、心跳、订阅发布和退订</summary>
[Collection("TransportIntegration")]
public class TransportIntegrationTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public TransportIntegrationTests()
    {
        var services = ObjectContainer.Current;
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

    /// <summary>创建 TCP 客户端</summary>
    private MqttClient CreateTcpClient(String? clientId = null, MqttVersion version = MqttVersion.V311)
    {
        return new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId ?? $"tcp_{version}_{Rand.NextString(4)}",
            Timeout = 5000,
            Reconnect = false,
            Version = version,
        };
    }

    /// <summary>创建 WebSocket 客户端</summary>
    private MqttClient CreateWsClient(String? clientId = null, MqttVersion version = MqttVersion.V311)
    {
        return new MqttClient
        {
            Log = XTrace.Log,
            Server = $"ws://127.0.0.1:{_port}",
            ClientId = clientId ?? $"ws_{version}_{Rand.NextString(4)}",
            Timeout = 5000,
            Reconnect = false,
            Version = version,
        };
    }

    #region TCP + V311 基础测试
    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：连接、心跳、断开")]
    public async Task TCP_V311_Connect_Ping_Disconnect()
    {
        using var client = CreateTcpClient("tcp_v311_basic", MqttVersion.V311);

        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：订阅、发布、接收消息")]
    public async Task TCP_V311_Subscribe_Publish_Receive()
    {
        using var pub = CreateTcpClient("tcp_v311_pub", MqttVersion.V311);
        using var sub = CreateTcpClient("tcp_v311_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("tcp/v311/test");
        await Task.Delay(100);

        var payload = "tcp_v311_" + Rand.NextString(4);
        await pub.PublishAsync("tcp/v311/test", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到TCP V311消息");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：订阅后退订不再收到消息")]
    public async Task TCP_V311_Unsubscribe()
    {
        using var pub = CreateTcpClient("tcp_v311_unsub_pub", MqttVersion.V311);
        using var sub = CreateTcpClient("tcp_v311_unsub_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedCount = 0;
        sub.Received += (s, e) => Interlocked.Increment(ref receivedCount);

        await sub.SubscribeAsync("tcp/v311/unsub");
        await Task.Delay(100);

        // 订阅中发一条应收到
        await pub.PublishAsync("tcp/v311/unsub", "msg1", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        // 退订后再发不应收到
        await sub.UnsubscribeAsync(["tcp/v311/unsub"]);
        await Task.Delay(100);

        await pub.PublishAsync("tcp/v311/unsub", "msg2", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：QoS1 发布收到 PubAck")]
    public async Task TCP_V311_QoS1()
    {
        using var client = CreateTcpClient("tcp_v311_qos1", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("tcp/v311/qos1", "qos1_data", QualityOfService.AtLeastOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：QoS2 四步握手完成")]
    public async Task TCP_V311_QoS2()
    {
        using var client = CreateTcpClient("tcp_v311_qos2", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("tcp/v311/qos2", "qos2_data", QualityOfService.ExactlyOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);

        await client.DisconnectAsync();
    }
    #endregion

    #region TCP + V500 基础测试
    [Fact]
    [System.ComponentModel.DisplayName("TCP+V500：连接、心跳、断开")]
    public async Task TCP_V500_Connect_Ping_Disconnect()
    {
        using var client = CreateTcpClient("tcp_v500_basic", MqttVersion.V500);

        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V500：订阅、发布、接收消息")]
    public async Task TCP_V500_Subscribe_Publish_Receive()
    {
        using var pub = CreateTcpClient("tcp_v500_pub", MqttVersion.V500);
        using var sub = CreateTcpClient("tcp_v500_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("tcp/v500/test");
        await Task.Delay(100);

        var payload = "tcp_v500_" + Rand.NextString(4);
        await pub.PublishAsync("tcp/v500/test", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到TCP V500消息");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V500：订阅后退订不再收到消息")]
    public async Task TCP_V500_Unsubscribe()
    {
        using var pub = CreateTcpClient("tcp_v500_unsub_pub", MqttVersion.V500);
        using var sub = CreateTcpClient("tcp_v500_unsub_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedCount = 0;
        sub.Received += (s, e) => Interlocked.Increment(ref receivedCount);

        await sub.SubscribeAsync("tcp/v500/unsub");
        await Task.Delay(100);

        await pub.PublishAsync("tcp/v500/unsub", "msg1", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await sub.UnsubscribeAsync(["tcp/v500/unsub"]);
        await Task.Delay(100);

        await pub.PublishAsync("tcp/v500/unsub", "msg2", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V500：QoS1 发布收到 PubAck")]
    public async Task TCP_V500_QoS1()
    {
        using var client = CreateTcpClient("tcp_v500_qos1", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("tcp/v500/qos1", "qos1_data", QualityOfService.AtLeastOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP+V500：QoS2 四步握手完成")]
    public async Task TCP_V500_QoS2()
    {
        using var client = CreateTcpClient("tcp_v500_qos2", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("tcp/v500/qos2", "qos2_data", QualityOfService.ExactlyOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);

        await client.DisconnectAsync();
    }
    #endregion

    #region WebSocket + V311 基础测试
    [Fact]
    [System.ComponentModel.DisplayName("WS+V311：连接、心跳、断开")]
    public async Task WS_V311_Connect_Ping_Disconnect()
    {
        using var client = CreateWsClient("ws_v311_basic", MqttVersion.V311);

        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V311：订阅、发布、接收消息")]
    public async Task WS_V311_Subscribe_Publish_Receive()
    {
        using var pub = CreateWsClient("ws_v311_pub", MqttVersion.V311);
        using var sub = CreateWsClient("ws_v311_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("ws/v311/test");
        await Task.Delay(100);

        var payload = "ws_v311_" + Rand.NextString(4);
        await pub.PublishAsync("ws/v311/test", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到WS V311消息");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V311：订阅后退订不再收到消息")]
    public async Task WS_V311_Unsubscribe()
    {
        using var pub = CreateWsClient("ws_v311_unsub_pub", MqttVersion.V311);
        using var sub = CreateWsClient("ws_v311_unsub_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedCount = 0;
        sub.Received += (s, e) => Interlocked.Increment(ref receivedCount);

        await sub.SubscribeAsync("ws/v311/unsub");
        await Task.Delay(100);

        await pub.PublishAsync("ws/v311/unsub", "msg1", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await sub.UnsubscribeAsync(["ws/v311/unsub"]);
        await Task.Delay(100);

        await pub.PublishAsync("ws/v311/unsub", "msg2", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region WebSocket + V500 基础测试
    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：连接、心跳、断开")]
    public async Task WS_V500_Connect_Ping_Disconnect()
    {
        using var client = CreateWsClient("ws_v500_basic", MqttVersion.V500);

        var ack = await client.ConnectAsync();
        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：订阅、发布、接收消息")]
    public async Task WS_V500_Subscribe_Publish_Receive()
    {
        using var pub = CreateWsClient("ws_v500_pub", MqttVersion.V500);
        using var sub = CreateWsClient("ws_v500_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("ws/v500/test");
        await Task.Delay(100);

        var payload = "ws_v500_" + Rand.NextString(4);
        await pub.PublishAsync("ws/v500/test", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到WS V500消息");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：订阅后退订不再收到消息")]
    public async Task WS_V500_Unsubscribe()
    {
        using var pub = CreateWsClient("ws_v500_unsub_pub", MqttVersion.V500);
        using var sub = CreateWsClient("ws_v500_unsub_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedCount = 0;
        sub.Received += (s, e) => Interlocked.Increment(ref receivedCount);

        await sub.SubscribeAsync("ws/v500/unsub");
        await Task.Delay(100);

        await pub.PublishAsync("ws/v500/unsub", "msg1", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await sub.UnsubscribeAsync(["ws/v500/unsub"]);
        await Task.Delay(100);

        await pub.PublishAsync("ws/v500/unsub", "msg2", QualityOfService.AtMostOnce);
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：QoS1 发布收到 PubAck")]
    public async Task WS_V500_QoS1()
    {
        using var client = CreateWsClient("ws_v500_qos1", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("ws/v500/qos1", "qos1_data", QualityOfService.AtLeastOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：QoS2 四步握手完成")]
    public async Task WS_V500_QoS2()
    {
        using var client = CreateWsClient("ws_v500_qos2", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("ws/v500/qos2", "qos2_data", QualityOfService.ExactlyOnce);
        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);

        await client.DisconnectAsync();
    }
    #endregion

    #region 跨传输协议交叉测试
    [Fact]
    [System.ComponentModel.DisplayName("跨协议：TCP发布 → WS订阅接收")]
    public async Task TCP_Publish_WS_Subscribe()
    {
        using var pub = CreateTcpClient("cross_tcp_pub", MqttVersion.V311);
        using var sub = CreateWsClient("cross_ws_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/tcp_to_ws");
        await Task.Delay(100);

        var payload = "tcp2ws_" + Rand.NextString(4);
        await pub.PublishAsync("cross/tcp_to_ws", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到跨协议消息（TCP→WS）");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("跨协议：WS发布 → TCP订阅接收")]
    public async Task WS_Publish_TCP_Subscribe()
    {
        using var pub = CreateWsClient("cross_ws_pub", MqttVersion.V311);
        using var sub = CreateTcpClient("cross_tcp_sub2", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/ws_to_tcp");
        await Task.Delay(100);

        var payload = "ws2tcp_" + Rand.NextString(4);
        await pub.PublishAsync("cross/ws_to_tcp", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到跨协议消息（WS→TCP）");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("跨协议+版本：TCP V500发布 → WS V311订阅")]
    public async Task TCP_V500_Publish_WS_V311_Subscribe()
    {
        using var pub = CreateTcpClient("cross_tv5_pub", MqttVersion.V500);
        using var sub = CreateWsClient("cross_wv3_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/v500tcp_to_v311ws");
        await Task.Delay(100);

        var payload = "v500tcp2v311ws_" + Rand.NextString(4);
        await pub.PublishAsync("cross/v500tcp_to_v311ws", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到跨版本跨协议消息（TCP V500→WS V311）");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("跨协议+版本：WS V500发布 → TCP V311订阅")]
    public async Task WS_V500_Publish_TCP_V311_Subscribe()
    {
        using var pub = CreateWsClient("cross_wv5_pub", MqttVersion.V500);
        using var sub = CreateTcpClient("cross_tv3_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await sub.SubscribeAsync("cross/v500ws_to_v311tcp");
        await Task.Delay(100);

        var payload = "v500ws2v311tcp_" + Rand.NextString(4);
        await pub.PublishAsync("cross/v500ws_to_v311tcp", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到跨版本跨协议消息（WS V500→TCP V311）");
        Assert.Equal(payload, await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 多客户端并发混合测试
    [Fact]
    [System.ComponentModel.DisplayName("混合并发：TCP和WS客户端同时连接发布")]
    public async Task Mixed_TCP_WS_Concurrent_Connect_Publish()
    {
        var clients = new List<MqttClient>();

        try
        {
            // 创建 TCP + WS 各 2 个客户端，V311 + V500 各半
            clients.Add(CreateTcpClient("mix_tcp_v311", MqttVersion.V311));
            clients.Add(CreateTcpClient("mix_tcp_v500", MqttVersion.V500));
            clients.Add(CreateWsClient("mix_ws_v311", MqttVersion.V311));
            clients.Add(CreateWsClient("mix_ws_v500", MqttVersion.V500));

            // 并发连接
            var connectTasks = clients.Select(c => c.ConnectAsync()).ToArray();
            await Task.WhenAll(connectTasks);

            foreach (var c in clients)
                Assert.True(c.IsConnected);

            // 各自发布一条消息
            var pubTasks = clients.Select((c, i) =>
                c.PublishAsync($"mix/topic{i}", $"msg_{i}", QualityOfService.AtMostOnce)).ToArray();
            await Task.WhenAll(pubTasks);

            // 心跳测试
            var pingTasks = clients.Select(c => c.PingAsync()).ToArray();
            await Task.WhenAll(pingTasks);

            foreach (var t in pingTasks)
                Assert.NotNull(await t);
        }
        finally
        {
            foreach (var c in clients)
            {
                try { await c.DisconnectAsync(); } catch { }
                c.TryDispose();
            }
        }
    }

    [Fact]
    [System.ComponentModel.DisplayName("混合并发：所有协议和版本订阅同一主题均收到消息")]
    public async Task Mixed_All_Subscribers_Receive_Same_Topic()
    {
        using var pub = CreateTcpClient("mix_fanout_pub", MqttVersion.V311);
        using var subTcpV311 = CreateTcpClient("mix_sub_tv3", MqttVersion.V311);
        using var subTcpV500 = CreateTcpClient("mix_sub_tv5", MqttVersion.V500);
        using var subWsV311 = CreateWsClient("mix_sub_wv3", MqttVersion.V311);
        using var subWsV500 = CreateWsClient("mix_sub_wv5", MqttVersion.V500);

        await pub.ConnectAsync();
        await subTcpV311.ConnectAsync();
        await subTcpV500.ConnectAsync();
        await subWsV311.ConnectAsync();
        await subWsV500.ConnectAsync();

        var tcpV311Received = new TaskCompletionSource<String>();
        var tcpV500Received = new TaskCompletionSource<String>();
        var wsV311Received = new TaskCompletionSource<String>();
        var wsV500Received = new TaskCompletionSource<String>();

        subTcpV311.Received += (s, e) => tcpV311Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subTcpV500.Received += (s, e) => tcpV500Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subWsV311.Received += (s, e) => wsV311Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subWsV500.Received += (s, e) => wsV500Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subTcpV311.SubscribeAsync("mix/fanout");
        await subTcpV500.SubscribeAsync("mix/fanout");
        await subWsV311.SubscribeAsync("mix/fanout");
        await subWsV500.SubscribeAsync("mix/fanout");
        await Task.Delay(200);

        var payload = "fanout_" + Rand.NextString(4);
        await pub.PublishAsync("mix/fanout", payload, QualityOfService.AtMostOnce);

        var timeout = Task.Delay(5000);
        var allReceived = Task.WhenAll(tcpV311Received.Task, tcpV500Received.Task, wsV311Received.Task, wsV500Received.Task);
        var winner = await Task.WhenAny(allReceived, timeout);

        Assert.True(winner == allReceived, "5秒内并非所有订阅者都收到消息");
        Assert.Equal(payload, await tcpV311Received.Task);
        Assert.Equal(payload, await tcpV500Received.Task);
        Assert.Equal(payload, await wsV311Received.Task);
        Assert.Equal(payload, await wsV500Received.Task);

        await pub.DisconnectAsync();
        await subTcpV311.DisconnectAsync();
        await subTcpV500.DisconnectAsync();
        await subWsV311.DisconnectAsync();
        await subWsV500.DisconnectAsync();
    }
    #endregion

    #region 通配符订阅跨协议测试
    [Fact]
    [System.ComponentModel.DisplayName("通配符：WS客户端通配符订阅 + TCP客户端发布")]
    public async Task Wildcard_WS_Subscribe_TCP_Publish()
    {
        using var pub = CreateTcpClient("wild_tcp_pub", MqttVersion.V311);
        using var sub = CreateWsClient("wild_ws_sub", MqttVersion.V500);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Topic);

        await sub.SubscribeAsync("sensor/+/temp");
        await Task.Delay(100);

        await pub.PublishAsync("sensor/room1/temp", "25.5");

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到通配符匹配消息");
        Assert.Equal("sensor/room1/temp", await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("通配符：TCP客户端 # 通配符订阅 + WS客户端发布")]
    public async Task Wildcard_MultiLevel_TCP_Subscribe_WS_Publish()
    {
        using var pub = CreateWsClient("wild2_ws_pub", MqttVersion.V500);
        using var sub = CreateTcpClient("wild2_tcp_sub", MqttVersion.V311);

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        sub.Received += (s, e) => received.TrySetResult(e.Arg.Topic);

        await sub.SubscribeAsync("device/#");
        await Task.Delay(100);

        await pub.PublishAsync("device/sensor/temp/room1", "22.0");

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3秒内未收到#通配符匹配消息");
        Assert.Equal("device/sensor/temp/room1", await received.Task);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }
    #endregion

    #region 连接事件与心跳测试
    [Fact]
    [System.ComponentModel.DisplayName("TCP：连接和断开事件正确触发")]
    public async Task TCP_Connect_Disconnect_Events()
    {
        using var client = CreateTcpClient("tcp_event_test", MqttVersion.V311);

        var connected = false;
        var disconnected = false;
        client.Connected += (s, e) => connected = true;
        client.Disconnected += (s, e) => disconnected = true;

        await client.ConnectAsync();
        Assert.True(connected);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.True(disconnected);
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS：连接和断开事件正确触发")]
    public async Task WS_Connect_Disconnect_Events()
    {
        using var client = CreateWsClient("ws_event_test", MqttVersion.V500);

        var connected = false;
        var disconnected = false;
        client.Connected += (s, e) => connected = true;
        client.Disconnected += (s, e) => disconnected = true;

        await client.ConnectAsync();
        Assert.True(connected);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.True(disconnected);
    }

    [Fact]
    [System.ComponentModel.DisplayName("TCP：多次心跳均正常响应")]
    public async Task TCP_Multiple_Pings()
    {
        using var client = CreateTcpClient("tcp_multi_ping", MqttVersion.V311);
        await client.ConnectAsync();

        for (var i = 0; i < 5; i++)
        {
            var ping = await client.PingAsync();
            Assert.NotNull(ping);
        }

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS：多次心跳均正常响应")]
    public async Task WS_Multiple_Pings()
    {
        using var client = CreateWsClient("ws_multi_ping", MqttVersion.V500);
        await client.ConnectAsync();

        for (var i = 0; i < 5; i++)
        {
            var ping = await client.PingAsync();
            Assert.NotNull(ping);
        }

        await client.DisconnectAsync();
    }
    #endregion

    #region 多主题订阅与退订
    [Fact]
    [System.ComponentModel.DisplayName("TCP+V311：多主题同时订阅和退订")]
    public async Task TCP_V311_MultiTopic_Subscribe_Unsubscribe()
    {
        using var client = CreateTcpClient("tcp_multi_topic", MqttVersion.V311);
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(["topic/a", "topic/b", "topic/c/#"]);
        Assert.NotNull(rs);
        Assert.Equal(3, rs!.GrantedQos.Count);

        await client.UnsubscribeAsync(["topic/a", "topic/b", "topic/c/#"]);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("WS+V500：多主题同时订阅和退订")]
    public async Task WS_V500_MultiTopic_Subscribe_Unsubscribe()
    {
        using var client = CreateWsClient("ws_multi_topic", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.SubscribeAsync(["topic/x", "topic/y", "topic/z/#"]);
        Assert.NotNull(rs);
        Assert.Equal(3, rs!.GrantedQos.Count);

        await client.UnsubscribeAsync(["topic/x", "topic/y", "topic/z/#"]);

        await client.DisconnectAsync();
    }
    #endregion
}
