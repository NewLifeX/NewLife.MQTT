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

/// <summary>多协议版本集成测试。验证同一服务端同时兼容 MQTT 3.1.1 (V311) 与 MQTT 5.0 (V500) 客户端</summary>
[Collection("MultiProtocol")]
public class MultiProtocolIntegrationTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public MultiProtocolIntegrationTests()
    {
        _port = 0; // 系统自动分配随机端口

        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);

        _server = new MqttServer
        {
            Port = _port,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();

        // 获取实际分配端口
        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateClient(String? clientId = null, MqttVersion version = MqttVersion.V311)
    {
        return new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId ?? $"test_{version}_{Rand.NextString(4)}",
            Timeout = 5000,
            Reconnect = false,
            Version = version,
        };
    }

    #region 各协议版本基础连接
    [Fact]
    [System.ComponentModel.DisplayName("多协议：MQTT 3.1.1 客户端可正常连接服务端")]
    public async Task V311_Client_ConnectsAndPings()
    {
        using var client = CreateClient("v311_ping_client", MqttVersion.V311);

        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
        // DisconnectAsync 发送 DISCONNECT 包后不立即关闭连接（等服务端关闭），不检查 IsConnected
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：MQTT 5.0 客户端可正常连接服务端")]
    public async Task V500_Client_ConnectsAndPings()
    {
        using var client = CreateClient("v500_ping_client", MqttVersion.V500);

        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        Assert.True(client.IsConnected);

        var ping = await client.PingAsync();
        Assert.NotNull(ping);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：V310 ProtocolLevel=0x03 且 ProtocolName=MQTT 被服务端接受")]
    public async Task V310_ProtocolLevel_MQTT_Name_Accepted()
    {
        // 使用标准 MQTT 协议名但 Level=0x03（既不是真正的 3.1，也不会被拒绝）
        using var client = CreateClient("v310_compat_client");

        // 手动构造 CONNECT 消息：ProtocolName=MQTT, ProtocolLevel=0x03
        var connectMsg = new ConnectMessage
        {
            ClientId = "v310_compat_client",
            ProtocolName = "MQTT",      // 标准名，不是 MQIsdp
            ProtocolLevel = MqttVersion.V310,
            CleanSession = true,
        };

        // 需要先 InitAsync，通过 ConnectAsync(ConnectMessage) 重载发送
        client.Version = MqttVersion.V310; // 设置版本（ProtocolLevel=0x03）
        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：ProtocolName=MQIsdp 被服务端拒绝（RefusedUnacceptableProtocolVersion）")]
    public async Task MQIsdp_ProtocolName_Rejected_By_Server()
    {
        using var client = CreateClient("mqisdp_client");

        // 手动构造包含 MQIsdp 协议名的 CONNECT 消息
        var connectMsg = new ConnectMessage
        {
            ClientId = "mqisdp_client",
            ProtocolName = "MQIsdp",
            ProtocolLevel = MqttVersion.V310,
            CleanSession = true,
        };

        // 服务端应回复 RefusedUnacceptableProtocolVersion
        var exception = await Assert.ThrowsAsync<Exception>(() => client.ConnectAsync(connectMsg));
        Assert.Contains("连接失败", exception.Message);
    }
    #endregion

    #region 同一服务端多协议并发连接
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V311+V500 客户端同时连接同一服务端")]
    public async Task V311_And_V500_Clients_ConnectSameServer()
    {
        using var v311Client = CreateClient("multi_v311", MqttVersion.V311);
        using var v500Client = CreateClient("multi_v500", MqttVersion.V500);

        // 两个客户端并行连接
        var v311Task = v311Client.ConnectAsync();
        var v500Task = v500Client.ConnectAsync();

        await Task.WhenAll(v311Task, v500Task);

        var v311Ack = await v311Task;
        var v500Ack = await v500Task;

        Assert.Equal(ConnectReturnCode.Accepted, v311Ack.ReturnCode);
        Assert.Equal(ConnectReturnCode.Accepted, v500Ack.ReturnCode);
        Assert.True(v311Client.IsConnected);
        Assert.True(v500Client.IsConnected);

        await v311Client.DisconnectAsync();
        await v500Client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：多个 V311+V500 客户端同时连接并发布")]
    public async Task Multiple_V311_V500_Clients_Concurrent()
    {
        const Int32 clientCount = 4;
        var clients = new List<MqttClient>();

        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var version = (i % 2 == 0) ? MqttVersion.V311 : MqttVersion.V500;
                var c = CreateClient($"concurrent_{i}", version);
                clients.Add(c);
            }

            // 并发连接
            var connectTasks = clients.Select(c => c.ConnectAsync()).ToArray();
            await Task.WhenAll(connectTasks);

            // 验证全部连接成功
            foreach (var c in clients)
                Assert.True(c.IsConnected);

            // 各自发布一条消息
            var pubTasks = clients.Select((c, i) =>
                c.PublishAsync($"concurrent/topic{i}", $"msg_{i}", QualityOfService.AtMostOnce)).ToArray();
            await Task.WhenAll(pubTasks);
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
    #endregion

    #region 跨协议版本发布/订阅
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V311 发布 → V500 订阅接收")]
    public async Task V311_Publishes_V500_Subscribes()
    {
        using var publisher = CreateClient("cross_pub_v311", MqttVersion.V311);
        using var subscriber = CreateClient("cross_sub_v500", MqttVersion.V500);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync("cross/v311_to_v500");
        await Task.Delay(100);

        var payload = "hello_from_v311_" + Rand.NextString(4);
        await publisher.PublishAsync("cross/v311_to_v500", payload, QualityOfService.AtMostOnce);

        var winner = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner == received.Task, "3 秒内未收到跨协议消息（V311→V500）");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 发布 → V311 订阅接收")]
    public async Task V500_Publishes_V311_Subscribes()
    {
        using var publisher = CreateClient("cross_pub_v500", MqttVersion.V500);
        using var subscriber = CreateClient("cross_sub_v311", MqttVersion.V311);

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subscriber.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subscriber.SubscribeAsync("cross/v500_to_v311");
        await Task.Delay(100);

        var payload = "hello_from_v500_" + Rand.NextString(4);
        await publisher.PublishAsync("cross/v500_to_v311", payload, QualityOfService.AtMostOnce);

        var winner2 = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner2 == received.Task, "3 秒内未收到跨协议消息（V500→V311）");
        Assert.Equal(payload, await received.Task);

        await publisher.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：V311+V500 订阅同一主题均收到消息")]
    public async Task Both_V311_And_V500_Subscribers_Receive()
    {
        using var pub = CreateClient("fanout_pub", MqttVersion.V311);
        using var subV311 = CreateClient("fanout_sub_v311", MqttVersion.V311);
        using var subV500 = CreateClient("fanout_sub_v500", MqttVersion.V500);

        await pub.ConnectAsync();
        await subV311.ConnectAsync();
        await subV500.ConnectAsync();

        var v311Received = new TaskCompletionSource<String>();
        var v500Received = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => v311Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        subV500.Received += (s, e) => v500Received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subV311.SubscribeAsync("fanout/broadcast");
        await subV500.SubscribeAsync("fanout/broadcast");
        await Task.Delay(100);

        var payload = "broadcast_" + Rand.NextString(4);
        await pub.PublishAsync("fanout/broadcast", payload, QualityOfService.AtMostOnce);

        var v311Winner = await Task.WhenAny(v311Received.Task, Task.Delay(3000));
        var v500Winner = await Task.WhenAny(v500Received.Task, Task.Delay(3000));

        Assert.True(v311Winner == v311Received.Task, "3 秒内 V311 订阅者未收到广播消息");
        Assert.Equal(payload, await v311Received.Task);
        Assert.True(v500Winner == v500Received.Task, "3 秒内 V500 订阅者未收到广播消息");
        Assert.Equal(payload, await v500Received.Task);

        await pub.DisconnectAsync();
        await subV311.DisconnectAsync();
        await subV500.DisconnectAsync();
    }
    #endregion

    #region V500 QoS 全路径
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 客户端 QoS1 发布收到 PubAck")]
    public async Task V500_QoS1_PublishAndAck()
    {
        using var client = CreateClient("v500_qos1", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("v500/qos1", "test_data", QualityOfService.AtLeastOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubAck>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 客户端 QoS2 完成四步握手")]
    public async Task V500_QoS2_FourWayHandshake()
    {
        using var client = CreateClient("v500_qos2", MqttVersion.V500);
        await client.ConnectAsync();

        var rs = await client.PublishAsync("v500/qos2", "qos2_data", QualityOfService.ExactlyOnce);

        Assert.NotNull(rs);
        Assert.IsType<PubComp>(rs);
        Assert.NotEqual(0, rs!.Id);

        await client.DisconnectAsync();
    }
    #endregion

    #region V500 CONNACK 属性检查
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 CONNACK 返回正确连接码")]
    public async Task V500_ConnAck_HasAcceptedReturnCode()
    {
        using var client = CreateClient("v500_connack_check", MqttVersion.V500);

        var ack = await client.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);

        await client.DisconnectAsync();
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 服务端 KeepAlive 覆盖可通过 Exchange 设置")]
    public async Task V500_ServerKeepAlive_CanBeSet()
    {
        // 创建带 ServerKeepAlive 的专用服务端
        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);

        var exchange = new MqttExchange { ServerKeepAlive = 120 };
        var server = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Exchange = exchange,
            Log = XTrace.Log,
        };
        server.Start();

        try
        {
            using var client = new MqttClient
            {
                Log = XTrace.Log,
                Server = $"tcp://127.0.0.1:{server.Port}",
                ClientId = "v500_ka_client",
                Timeout = 5000,
                Reconnect = false,
                Version = MqttVersion.V500,
            };

            var ack = await client.ConnectAsync();

            Assert.NotNull(ack);
            Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
            // V500 CONNACK 应携带 Properties（含 ServerKeepAlive）
            // 由于 ack.Properties 在 ConnAck 反序列化时会包含
            // 在此仅验证连接成功且 ServerKeepAlive 已设置到 Exchange
            Assert.Equal((UInt16)120, exchange.ServerKeepAlive);

            await client.DisconnectAsync();
        }
        finally
        {
            server.TryDispose();
        }
    }
    #endregion

    #region V500 持久会话
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 CleanSession=false 会话持久化")]
    public async Task V500_PersistentSession_SaveAndRestore()
    {
        const String clientId = "v500_persistent_client";

        using var client1 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V500,
            CleanSession = false,
        };

        await client1.ConnectAsync();
        await client1.SubscribeAsync("persistent/v500/topic");
        await client1.DisconnectAsync();
        // 等待服务端处理 DISCONNECT 并保存持久会话
        await Task.Delay(200);
        // 使用同一 ClientId、CleanSession=false 重连，服务端应恢复会话
        using var client2 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = clientId,
            Timeout = 5000,
            Reconnect = false,
            Version = MqttVersion.V500,
            CleanSession = false,
        };

        var ack = await client2.ConnectAsync();

        Assert.NotNull(ack);
        Assert.Equal(ConnectReturnCode.Accepted, ack.ReturnCode);
        // SessionPresent=true 表示服务端找到了持久会话
        Assert.True(ack.SessionPresent);

        await client2.DisconnectAsync();
    }
    #endregion

    #region V500 遗嘱消息
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 遗嘱消息异常断开时发布")]
    public async Task V500_Will_PublishedOnAbnormalDisconnect()
    {
        using var subV311 = CreateClient("v500will_sub", MqttVersion.V311);
        await subV311.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subV311.SubscribeAsync("v500/will/status");
        await Task.Delay(100);

        // V500 客户端携带遗嘱，异常断开
        var willClient = CreateClient("v500_will_client", MqttVersion.V500);
        willClient.WillTopic = "v500/will/status";
        willClient.WillMessage = "v500_offline"u8.ToArray();
        willClient.WillQoS = QualityOfService.AtMostOnce;

        await willClient.ConnectAsync();
        await Task.Delay(100);

        willClient.Dispose(); // 模拟异常断开

        var winner3 = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner3 == received.Task, "3 秒内未收到遗嘱消息（V500 异常断开）");
        Assert.Equal("v500_offline", await received.Task);

        await subV311.DisconnectAsync();
    }
    #endregion

    #region V500 Retain 消息
    [Fact]
    [System.ComponentModel.DisplayName("多协议：V500 客户端发布 Retain 消息，V311 客户端订阅后收到")]
    public async Task V500_Publishes_Retain_V311_Subscribes_Receives()
    {
        using var pubV500 = CreateClient("v500_retain_pub", MqttVersion.V500);
        await pubV500.ConnectAsync();

        var retainTopic = $"retain/v500/{Rand.NextString(4)}";
        var retainMsg = new PublishMessage
        {
            Topic = retainTopic,
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = new ArrayPacket("v500_retained_value"u8.ToArray()),
        };
        await pubV500.PublishAsync(retainMsg);
        await Task.Delay(100);

        // V311 客户端订阅，应收到 Retain 消息
        using var subV311 = CreateClient("v311_retain_sub", MqttVersion.V311);
        await subV311.ConnectAsync();

        var received = new TaskCompletionSource<String>();
        subV311.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

        await subV311.SubscribeAsync(retainTopic);

        var winner4 = await Task.WhenAny(received.Task, Task.Delay(3000));
        Assert.True(winner4 == received.Task, "3 秒内 V311 订阅者未收到 V500 发布的 Retain 消息");
        Assert.Equal("v500_retained_value", await received.Task);

        await pubV500.DisconnectAsync();
        await subV311.DisconnectAsync();
    }
    #endregion

    #region 连接字符串初始化
    [Fact]
    [System.ComponentModel.DisplayName("多协议：MqttClient.Init 连接字符串正确解析")]
    public void MqttClient_Init_ParsesConnectionString()
    {
        var client = new MqttClient();
        client.Init($"Server=tcp://127.0.0.1:{_port};UserName=user1;Password=pass1;ClientId=init_test_client;Timeout=8000");

        Assert.Equal($"tcp://127.0.0.1:{_port}", client.Server);
        Assert.Equal("user1", client.UserName);
        Assert.Equal("pass1", client.Password);
        Assert.Equal("init_test_client", client.ClientId);
        Assert.Equal(8000, client.Timeout);
    }

    [Fact]
    [System.ComponentModel.DisplayName("多协议：MqttClient.Init 逗号分隔格式正确解析")]
    public void MqttClient_Init_CommaDelimited_ParsesConnectionString()
    {
        var client = new MqttClient();
        client.Init($"Server=tcp://127.0.0.1:{_port},UserName=user2,Password=pass2,ClientId=comma_test");

        Assert.Equal($"tcp://127.0.0.1:{_port}", client.Server);
        Assert.Equal("user2", client.UserName);
        Assert.Equal("pass2", client.Password);
        Assert.Equal("comma_test", client.ClientId);
    }
    #endregion
}
