using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>
/// IoT 设备模拟集成测试。演示在没有任何真实硬件的情况下如何做完善的集成测试。
///
/// 核心策略：
///   1. 嵌入式服务端（Port=0）— 进程内启动真实 MQTT Broker，无需外部依赖
///   2. MqttClient 模拟设备 — 每个 MqttClient 实例即为一台虚拟 IoT 设备
///   3. 端到端消息流验证 — 完整模拟设备上报、平台下发、报警联动等真实场景
///   4. 并发压力验证 — 多设备同时上报，验证服务端吞吐稳定性
/// </summary>
[Collection("DeviceSimulationTests")]
public class DeviceSimulationTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public DeviceSimulationTests()
    {
        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        _server.Start();
        _port = _server.Port;
    }

    public void Dispose() => _server.TryDispose();

    private MqttClient CreateDevice(String deviceId, MqttVersion version = MqttVersion.V311) => new MqttClient
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = deviceId,
        Timeout = 5000,
        Reconnect = false,
        Version = version,
    };

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 A：单设备遥测上报（温湿度传感器）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟A：温湿度传感器上报 QoS1 遥测数据，平台侧收到并解析")]
    public async Task SimA_Sensor_Reports_Telemetry_To_Platform()
    {
        // 平台订阅端（模拟云端数据接入）
        using var platform = CreateDevice("platform_hub");
        await platform.ConnectAsync();

        var receivedTelemetry = new ConcurrentBag<String>();
        platform.Received += (s, e) => receivedTelemetry.Add(e.Arg.Payload?.ToStr() ?? "");
        await platform.SubscribeAsync(new[] { "devices/+/telemetry" }, QualityOfService.AtLeastOnce);
        await Task.Delay(100);

        // 模拟温湿度传感器
        using var sensor = CreateDevice("sensor_th_001");
        await sensor.ConnectAsync();

        // 发送 5 次遥测数据（JSON 格式）
        var sentCount = 5;
        for (var i = 0; i < sentCount; i++)
        {
            var temp = 20.0 + Rand.Next(-5, 15) / 10.0;
            var humi = 50 + Rand.Next(-10, 20);
            var payload = $"{{\"temp\":{temp:F1},\"humi\":{humi},\"seq\":{i}}}";
            await sensor.PublishAsync("devices/sensor_th_001/telemetry", payload, QualityOfService.AtLeastOnce);
            await Task.Delay(50);
        }

        // 等待平台侧全部接收
        await Task.Delay(1000);

        Assert.Equal(sentCount, receivedTelemetry.Count);
        // 验证 JSON 结构（每条都包含 temp 字段）
        foreach (var msg in receivedTelemetry)
        {
            Assert.Contains("\"temp\"", msg);
            Assert.Contains("\"humi\"", msg);
        }

        await platform.DisconnectAsync();
        await sensor.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 B：平台下发指令，设备执行并上报结果（命令/响应模式）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟B：平台下发控制指令，设备执行并回传响应（命令-响应模式）")]
    public async Task SimB_Platform_Command_Device_Response()
    {
        using var platform = CreateDevice("platform_cmd");
        using var device = CreateDevice("actuator_relay_001");

        await platform.ConnectAsync();
        await device.ConnectAsync();

        // 设备监听命令主题
        var commandTcs = new TaskCompletionSource<String>();
        device.Received += async (s, e) =>
        {
            var cmd = e.Arg.Payload?.ToStr() ?? "";
            commandTcs.TrySetResult(cmd);

            // 模拟执行命令并回传响应
            await device.PublishAsync("devices/actuator_relay_001/response",
                $"{{\"status\":\"ok\",\"cmd\":{cmd}}}",
                QualityOfService.AtLeastOnce);
        };
        await device.SubscribeAsync(new[] { "devices/actuator_relay_001/cmd" }, QualityOfService.AtLeastOnce);

        // 平台监听响应主题
        var responseTcs = new TaskCompletionSource<String>();
        platform.Received += (s, e) => responseTcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await platform.SubscribeAsync(new[] { "devices/actuator_relay_001/response" }, QualityOfService.AtLeastOnce);
        await Task.Delay(200);

        // 平台下发开灯指令
        await platform.PublishAsync("devices/actuator_relay_001/cmd", "\"open\"", QualityOfService.AtLeastOnce);

        // 验证设备收到命令
        var cmdWin = await Task.WhenAny(commandTcs.Task, Task.Delay(3000));
        Assert.True(cmdWin == commandTcs.Task, "设备 3s 内未收到命令");
        Assert.Contains("open", await commandTcs.Task);

        // 验证平台收到响应
        var respWin = await Task.WhenAny(responseTcs.Task, Task.Delay(3000));
        Assert.True(respWin == responseTcs.Task, "平台 3s 内未收到设备响应");
        Assert.Contains("\"status\":\"ok\"", await responseTcs.Task);

        await platform.DisconnectAsync();
        await device.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 C：多设备并发上报（模拟工厂 10 个设备同时上线）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟C：10台设备并发上线并发布遥测，平台全量接收不丢消息")]
    public async Task SimC_MultiDevice_Concurrent_Telemetry()
    {
        const Int32 deviceCount = 10;
        const Int32 msgsPerDevice = 3;
        var expected = deviceCount * msgsPerDevice;

        // 平台聚合订阅
        using var aggregator = CreateDevice("platform_agg");
        await aggregator.ConnectAsync();

        var receivedMsgs = 0;
        aggregator.Received += (s, e) => Interlocked.Increment(ref receivedMsgs);
        await aggregator.SubscribeAsync(new[] { "factory/+/data" }, QualityOfService.AtLeastOnce);
        await Task.Delay(200);

        // 并发创建、连接、发布
        var devices = new MqttClient[deviceCount];
        for (var i = 0; i < deviceCount; i++)
            devices[i] = CreateDevice($"factory_device_{i:D3}");

        await Task.WhenAll(devices.Select(d => d.ConnectAsync()));

        // 每台设备发 msgsPerDevice 条
        var publishTasks = devices.SelectMany((d, idx) =>
            Enumerable.Range(0, msgsPerDevice).Select(j =>
                d.PublishAsync($"factory/{d.ClientId}/data",
                    $"{{\"device\":\"{d.ClientId}\",\"val\":{j}}}",
                    QualityOfService.AtLeastOnce)));
        await Task.WhenAll(publishTasks);

        // 等待全部送达
        var deadline = DateTime.Now.AddSeconds(5);
        while (receivedMsgs < expected && DateTime.Now < deadline)
            await Task.Delay(100);

        Assert.Equal(expected, receivedMsgs);

        // 清理
        await Task.WhenAll(devices.Select(d => d.DisconnectAsync()));
        foreach (var d in devices)
            d.Dispose();
        await aggregator.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 D：设备异常离线 + 遗嘱告警（设备心跳监控）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟D：设备异常断电触发遗嘱告警，告警平台及时收到离线通知")]
    public async Task SimD_Device_PowerCut_Triggers_Will_Alert()
    {
        using var alertPlatform = CreateDevice("alert_platform");
        await alertPlatform.ConnectAsync();

        var alertTcs = new TaskCompletionSource<String>();
        alertPlatform.Received += (s, e) => alertTcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
        await alertPlatform.SubscribeAsync(new[] { "alerts/device/offline" }, QualityOfService.AtLeastOnce);
        await Task.Delay(200);

        // 设备携带遗嘱消息（模拟心跳监控 LWT）
        var device = CreateDevice("critical_device_001");
        device.WillTopic = "alerts/device/offline";
        device.WillMessage = "{\"deviceId\":\"critical_device_001\",\"reason\":\"unexpected_disconnect\"}"u8.ToArray();
        device.WillQoS = QualityOfService.AtLeastOnce;

        await device.ConnectAsync();
        await Task.Delay(200);

        // 模拟突然断电（直接 Dispose 不发 DISCONNECT）
        device.Dispose();

        // 验证告警平台收到离线通知
        var win = await Task.WhenAny(alertTcs.Task, Task.Delay(5000));
        Assert.True(win == alertTcs.Task, "5s 内告警平台未收到离线遗嘱");
        var alertPayload = await alertTcs.Task;
        Assert.Contains("critical_device_001", alertPayload);
        Assert.Contains("unexpected_disconnect", alertPayload);

        await alertPlatform.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 E：设备离线消息缓存 + 重连补投（QoS1 持久会话）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟E：弱网设备断线重连后服务端补投离线期间的控制指令")]
    public async Task SimE_WeakNetwork_Device_Reconnect_Receives_Queued_Commands()
    {
        const String deviceId = "weak_network_device_001";
        const String cmdTopic = $"devices/{deviceId}/cmd";

        // 设备初次连接（持久会话），订阅指令主题后断线
        var device1 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = deviceId,
            Timeout = 5000,
            CleanSession = false,
        };
        await device1.ConnectAsync();
        await device1.SubscribeAsync(new[] { cmdTopic }, QualityOfService.AtLeastOnce);
        await device1.DisconnectAsync();
        device1.Dispose();
        await Task.Delay(300);

        // 平台在设备离线期间下发 3 条指令
        using var platform = CreateDevice("cmd_platform_e");
        await platform.ConnectAsync();
        await platform.PublishAsync(cmdTopic, "{\"cmd\":\"reboot\"}", QualityOfService.AtLeastOnce);
        await platform.PublishAsync(cmdTopic, "{\"cmd\":\"update_config\"}", QualityOfService.AtLeastOnce);
        await platform.PublishAsync(cmdTopic, "{\"cmd\":\"check_status\"}", QualityOfService.AtLeastOnce);
        await Task.Delay(300);

        // 设备重连（相同 ClientId，CleanSession=false）
        var receivedCmds = new ConcurrentBag<String>();
        var device2 = new MqttClient
        {
            Log = XTrace.Log,
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = deviceId,
            Timeout = 5000,
            CleanSession = false,
        };
        device2.Received += (s, e) => receivedCmds.Add(e.Arg.Payload?.ToStr() ?? "");
        var ack = await device2.ConnectAsync();

        // 服务端应找到持久会话并补投离线消息
        Assert.True(ack.SessionPresent, "服务端未找到持久会话");

        // 等待补投完成
        var deadline = DateTime.Now.AddSeconds(3);
        while (receivedCmds.Count < 3 && DateTime.Now < deadline)
            await Task.Delay(100);

        Assert.Equal(3, receivedCmds.Count);
        Assert.Contains(receivedCmds, m => m.Contains("reboot"));
        Assert.Contains(receivedCmds, m => m.Contains("update_config"));
        Assert.Contains(receivedCmds, m => m.Contains("check_status"));

        await device2.DisconnectAsync();
        device2.Dispose();
        await platform.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 F：设备配置下发（Retain 消息，设备随时上线均能获取最新配置）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟F：平台预发布 Retain 配置，任意时刻上线的设备均获取最新配置")]
    public async Task SimF_Platform_Retain_Config_Device_Gets_On_Subscribe()
    {
        const String configTopic = "config/device_type_A";
        const String configPayload = "{\"interval\":30,\"threshold\":85.0,\"version\":\"2.1.0\"}";

        // 平台预先发布设备配置（Retain=true）
        using var platform = CreateDevice("config_platform_f");
        await platform.ConnectAsync();
        await platform.PublishAsync(new PublishMessage
        {
            Topic = configTopic,
            Payload = new ArrayPacket(configPayload.GetBytes()),
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        });
        await platform.DisconnectAsync();
        await Task.Delay(200);

        // 模拟 3 台同型号设备在不同时刻上线，均能获取到保留配置
        for (var i = 1; i <= 3; i++)
        {
            using var device = CreateDevice($"device_type_A_{i:D3}");
            await device.ConnectAsync();

            var configTcs = new TaskCompletionSource<String>();
            device.Received += (s, e) => configTcs.TrySetResult(e.Arg.Payload?.ToStr() ?? "");
            await device.SubscribeAsync(configTopic);

            // 设备应立即收到保留配置（无需等待平台再次发布）
            var win = await Task.WhenAny(configTcs.Task, Task.Delay(2000));
            Assert.True(win == configTcs.Task, $"设备{i} 未在 2s 内收到保留配置");

            var received = await configTcs.Task;
            Assert.Contains("\"interval\":30", received);
            Assert.Contains("\"version\":\"2.1.0\"", received);

            await device.DisconnectAsync();
            await Task.Delay(100);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 G：QoS 降级投递验证
    //   发布者 QoS2，订阅者 QoS1 → 投递 QoS 为 min(2,1)=1
    //   发布者 QoS1，订阅者 QoS0 → 投递 QoS 为 min(1,0)=0
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟G1：发布者 QoS2，订阅者 QoS1 → 实际投递 QoS1")]
    public async Task SimG1_QoS_Downgrade_Pub2_Sub1()
    {
        var topic = $"sim/qos_downgrade/{Rand.NextString(4)}";
        using var pub = CreateDevice("sim_g1_pub");
        using var sub = CreateDevice("sim_g1_sub");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedMsg = new TaskCompletionSource<PublishMessage>();
        sub.Received += (s, e) => receivedMsg.TrySetResult(e.Arg);
        // 订阅者以 QoS1 订阅
        await sub.SubscribeAsync(new[] { topic }, QualityOfService.AtLeastOnce);
        await Task.Delay(100);

        // 发布者以 QoS2 发布
        await pub.PublishAsync(topic, "downgrade_test", QualityOfService.ExactlyOnce);

        var win = await Task.WhenAny(receivedMsg.Task, Task.Delay(4000));
        Assert.True(win == receivedMsg.Task, "消息未到达");

        var msg = await receivedMsg.Task;
        // 投递 QoS 应为订阅者指定的 QoS1（min(publish=2, subscribe=1) = 1）
        Assert.Equal(QualityOfService.AtLeastOnce, msg.QoS);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    [Fact]
    [DisplayName("设备模拟G2：发布者 QoS1，订阅者 QoS0 → 实际投递 QoS0")]
    public async Task SimG2_QoS_Downgrade_Pub1_Sub0()
    {
        var topic = $"sim/qos_downgrade0/{Rand.NextString(4)}";
        using var pub = CreateDevice("sim_g2_pub");
        using var sub = CreateDevice("sim_g2_sub");

        await pub.ConnectAsync();
        await sub.ConnectAsync();

        var receivedMsg = new TaskCompletionSource<PublishMessage>();
        sub.Received += (s, e) => receivedMsg.TrySetResult(e.Arg);
        // 订阅者以 QoS0 订阅
        await sub.SubscribeAsync(new[] { topic }, QualityOfService.AtMostOnce);
        await Task.Delay(100);

        // 发布者以 QoS1 发布
        await pub.PublishAsync(topic, "downgrade_test_q0", QualityOfService.AtLeastOnce);

        var win = await Task.WhenAny(receivedMsg.Task, Task.Delay(4000));
        Assert.True(win == receivedMsg.Task, "消息未到达");

        var msg = await receivedMsg.Task;
        // 投递 QoS 应为 QoS0（min(publish=1, subscribe=0) = 0）
        Assert.Equal(QualityOfService.AtMostOnce, msg.QoS);

        await pub.DisconnectAsync();
        await sub.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 H：MQTT 5.0 请求/响应模式（Response Topic + Correlation Data）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟H：MQTT 5.0 Request-Response 模式（ResponseTopic + CorrelationData）")]
    public async Task SimH_Mqtt5_RequestResponse_Pattern()
    {
        var requestTopic = $"sim/rpc/request/{Rand.NextString(4)}";
        var responseTopic = $"sim/rpc/response/{Rand.NextString(4)}";
        var correlationId = Rand.NextString(8);

        using var requester = CreateDevice("sim_h_requester", MqttVersion.V500);
        using var responder = CreateDevice("sim_h_responder", MqttVersion.V500);

        await requester.ConnectAsync();
        await responder.ConnectAsync();

        // 响应者监听请求主题
        var requestTcs = new TaskCompletionSource<PublishMessage>();
        responder.Received += async (s, e) =>
        {
            requestTcs.TrySetResult(e.Arg);
            // 模拟处理并回应（回应时携带 CorrelationData）
            var reply = new PublishMessage
            {
                Topic = responseTopic,
                Payload = new ArrayPacket("{\"result\":\"success\",\"value\":42}".GetBytes()),
                QoS = QualityOfService.AtMostOnce,
                Properties = new MqttProperties(),
            };
            if (e.Arg.Properties != null)
            {
                var corrData = e.Arg.Properties.GetBinary(MqttPropertyId.CorrelationData);
                if (corrData != null)
                    reply.Properties!.SetBinary(MqttPropertyId.CorrelationData, corrData);
            }
            await responder.PublishAsync(reply);
        };
        await responder.SubscribeAsync(requestTopic);

        // 请求者监听响应主题
        var responseTcs = new TaskCompletionSource<PublishMessage>();
        requester.Received += (s, e) => responseTcs.TrySetResult(e.Arg);
        await requester.SubscribeAsync(responseTopic);
        await Task.Delay(200);

        // 发送请求（携带 ResponseTopic 和 CorrelationData）
        var request = new PublishMessage
        {
            Topic = requestTopic,
            Payload = new ArrayPacket("{\"method\":\"getStatus\"}".GetBytes()),
            QoS = QualityOfService.AtMostOnce,
            Properties = new MqttProperties(),
        };
        request.Properties.SetString(MqttPropertyId.ResponseTopic, responseTopic);
        request.Properties.SetBinary(MqttPropertyId.CorrelationData, correlationId.GetBytes());
        await requester.PublishAsync(request);

        // 验证响应者收到请求
        var reqWin = await Task.WhenAny(requestTcs.Task, Task.Delay(3000));
        Assert.True(reqWin == requestTcs.Task, "响应者未收到请求");
        var reqMsg = await requestTcs.Task;
        Assert.Contains("getStatus", reqMsg.Payload?.ToStr() ?? "");

        // 验证请求者收到响应
        var respWin = await Task.WhenAny(responseTcs.Task, Task.Delay(3000));
        Assert.True(respWin == responseTcs.Task, "请求者未收到响应");
        var respMsg = await responseTcs.Task;
        Assert.Contains("\"result\":\"success\"", respMsg.Payload?.ToStr() ?? "");
        // 验证 CorrelationData 回传
        var corrBack = respMsg.Properties?.GetBinary(MqttPropertyId.CorrelationData);
        Assert.NotNull(corrBack);
        Assert.Equal(correlationId, corrBack.ToStr());

        await requester.DisconnectAsync();
        await responder.DisconnectAsync();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 场景 I：网关聚合（多子设备通过网关发布，平台按网关 ID 路由）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Fact]
    [DisplayName("设备模拟I：网关代理 3 台子设备上报数据（网关聚合模式）")]
    public async Task SimI_Gateway_Proxies_Multiple_Sub_Devices()
    {
        const String gatewayId = "gateway_001";
        var subDevices = new[] { "sub_001", "sub_002", "sub_003" };

        using var platform = CreateDevice("platform_gw_i");
        await platform.ConnectAsync();

        var receivedBySubDevice = new ConcurrentDictionary<String, Int32>();
        foreach (var subId in subDevices)
            receivedBySubDevice[subId] = 0;

        platform.Received += (s, e) =>
        {
            // 主题格式：gateways/{gatewayId}/devices/{subDeviceId}/telemetry
            var parts = e.Arg.Topic.Split('/');
            if (parts.Length >= 4)
                receivedBySubDevice.AddOrUpdate(parts[3], 1, (_, v) => v + 1);
        };
        await platform.SubscribeAsync(new[] { $"gateways/{gatewayId}/devices/+/telemetry" }, QualityOfService.AtLeastOnce);
        await Task.Delay(100);

        // 网关以单一连接代理所有子设备上报
        using var gateway = CreateDevice(gatewayId);
        await gateway.ConnectAsync();

        foreach (var subId in subDevices)
        {
            var payload = $"{{\"gateway\":\"{gatewayId}\",\"device\":\"{subId}\",\"val\":{Rand.Next(0, 100)}}}";
            await gateway.PublishAsync($"gateways/{gatewayId}/devices/{subId}/telemetry",
                payload, QualityOfService.AtLeastOnce);
        }

        await Task.Delay(1000);

        // 平台收到每个子设备各 1 条数据
        Assert.All(subDevices, subId =>
            Assert.Equal(1, receivedBySubDevice.GetValueOrDefault(subId, 0)));

        await platform.DisconnectAsync();
        await gateway.DisconnectAsync();
    }
}
