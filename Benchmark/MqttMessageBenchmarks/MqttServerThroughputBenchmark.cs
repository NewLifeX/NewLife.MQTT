using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 服务端吞吐压力测试。测量端到端消息处理能力（连接→发布→路由→订阅者接收）和内存分配</summary>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class MqttServerThroughputBenchmark
{
    private MqttServer _server = null!;
    private Int32 _port;
    private MqttClient[] _publishers = null!;
    private MqttClient _subscriber = null!;
    private Int64 _receivedCount;

    /// <summary>每批次发布的消息数</summary>
    [Params(1000, 5000)]
    public Int32 MessageCount { get; set; }

    /// <summary>并发发布者数量</summary>
    public static IEnumerable<Int32> PublisherCounts
    {
        get
        {
            var cores = Environment.ProcessorCount;
            var set = new SortedSet<Int32> { 1, 4 };
            if (cores > 4) set.Add(Math.Min(cores, 8));
            return set;
        }
    }

    [ParamsSource(nameof(PublisherCounts))]
    public Int32 PublisherCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = ObjectContainer.Current;
        services.AddSingleton<ILog>(XTrace.Log);

        _server = new MqttServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = Logger.Null,
            SessionLog = Logger.Null,
        };
        _server.Start();
        _port = _server.Port;

        // 创建订阅者
        _subscriber = new MqttClient
        {
            Server = $"tcp://127.0.0.1:{_port}",
            ClientId = "bench_sub",
            Timeout = 30000,
            Reconnect = false,
            Log = Logger.Null,
        };
        _subscriber.ConnectAsync().GetAwaiter().GetResult();
        _subscriber.Received += (s, e) => Interlocked.Increment(ref _receivedCount);
        _subscriber.SubscribeAsync("bench/#").GetAwaiter().GetResult();

        // 创建发布者
        _publishers = new MqttClient[PublisherCount];
        for (var i = 0; i < PublisherCount; i++)
        {
            var pub = new MqttClient
            {
                Server = $"tcp://127.0.0.1:{_port}",
                ClientId = $"bench_pub_{i}",
                Timeout = 30000,
                Reconnect = false,
                Log = Logger.Null,
            };
            pub.ConnectAsync().GetAwaiter().GetResult();
            _publishers[i] = pub;
        }

        // 等待所有连接稳定
        Thread.Sleep(200);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var pub in _publishers)
            pub.TryDispose();

        _subscriber.TryDispose();
        _server.TryDispose();
    }

    #region QoS0 吞吐（Fire and Forget，最高吞吐场景）
    [Benchmark(Description = "QoS0 端到端吞吐")]
    public async Task QoS0_Throughput()
    {
        Interlocked.Exchange(ref _receivedCount, 0);
        var messagesPerPublisher = MessageCount / PublisherCount;

        var tasks = new Task[PublisherCount];
        for (var i = 0; i < PublisherCount; i++)
        {
            var pub = _publishers[i];
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                for (var j = 0; j < messagesPerPublisher; j++)
                {
                    await pub.PublishAsync($"bench/qos0/{idx}", $"{{\"v\":{j}}}", QualityOfService.AtMostOnce);
                }
            });
        }
        await Task.WhenAll(tasks);

        // 等待投递完成（最长 5 秒）
        var deadline = Environment.TickCount64 + 5000;
        while (Interlocked.Read(ref _receivedCount) < MessageCount && Environment.TickCount64 < deadline)
            await Task.Delay(10);
    }
    #endregion

    #region QoS1 吞吐（需要 PubAck 确认）
    [Benchmark(Description = "QoS1 端到端吞吐")]
    public async Task QoS1_Throughput()
    {
        Interlocked.Exchange(ref _receivedCount, 0);
        var messagesPerPublisher = MessageCount / PublisherCount;

        var tasks = new Task[PublisherCount];
        for (var i = 0; i < PublisherCount; i++)
        {
            var pub = _publishers[i];
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                for (var j = 0; j < messagesPerPublisher; j++)
                {
                    await pub.PublishAsync($"bench/qos1/{idx}", $"{{\"v\":{j}}}", QualityOfService.AtLeastOnce);
                }
            });
        }
        await Task.WhenAll(tasks);

        var deadline = Environment.TickCount64 + 5000;
        while (Interlocked.Read(ref _receivedCount) < MessageCount && Environment.TickCount64 < deadline)
            await Task.Delay(10);
    }
    #endregion
}
