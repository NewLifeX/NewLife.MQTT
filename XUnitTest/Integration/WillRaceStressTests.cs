#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>诊断压测：正常 DISCONNECT 不应触发遗嘱</summary>
/// <remarks>
/// 默认跳过，仅在排查“正常断开被误判为异常断开”类问题时手动运行。
/// 用例会制造 CPU 饱和以放大竞态窗口，耗时较长，不适合纳入常规测试套件。
/// 曾用它复现 Core v12 消息泵在连接关闭时丢弃已到达 DISCONNECT 报文的问题（修复前 200 次中误发 192~199 次）；客户端加入 WaitForServerClose 延迟关闭缓解后该路径已被屏蔽，复现与复验须改用裸 TCP 第三方客户端直连。
/// </remarks>
[Collection("E2EScenarioTests")]
public class WillRaceStressTests : IDisposable
{
    private readonly MqttServer _server;
    private readonly Int32 _port;

    public WillRaceStressTests()
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

    private MqttClient CreateClient(String? clientId = null) => new()
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId ?? $"stress_{Rand.NextString(6)}",
        Timeout = 5000,
        Reconnect = false,
    };

    [Fact(Skip = "诊断压测，需手动运行（CPU 饱和 60 秒）")]
    [DisplayName("压测：正常 DISCONNECT 重复多次均不触发遗嘱")]
    public async Task NormalDisconnect_NeverPublishesWill_Stress()
    {
        var willCount = 0;

        using var watcher = CreateClient("stress_watcher");
        await watcher.ConnectAsync();
        watcher.Received += (s, e) => Interlocked.Increment(ref willCount);
        await watcher.SubscribeAsync("stress/will/#");
        await Task.Delay(100);

        // 制造线程池压力，放大“关闭先于泵交付”的竞态窗口
        var load = new List<Task>();
        var end = DateTime.Now.AddSeconds(60);
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            load.Add(Task.Run(() =>
            {
                while (DateTime.Now < end) { }
            }));
        }

        const Int32 n = 200;
        for (var i = 0; i < n; i++)
        {
            using var c = CreateClient($"stress_{i}");
            c.WillTopic = "stress/will/t";
            c.WillMessage = "boom"u8.ToArray();
            await c.ConnectAsync();
            await c.DisconnectAsync();
        }

        await Task.Delay(2000);

        XTrace.WriteLine("压测完成：{0} 次正常断开，误发遗嘱 {1} 次，服务端在线 {2}", n, willCount, _server.Exchange?.Stats.ConnectedClients);

        Assert.Equal(0, willCount);
    }
}
