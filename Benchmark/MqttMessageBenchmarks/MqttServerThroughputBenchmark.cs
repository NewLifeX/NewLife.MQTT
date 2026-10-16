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
    private Int64 _targetCount;
    private SemaphoreSlim? _receivedEvent;

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
        _subscriber.Received += (s, e) =>
        {
            // 达到目标数立即唤醒等待方，避免轮询等待引入毫秒级误差
            if (Interlocked.Increment(ref _receivedCount) >= Volatile.Read(ref _targetCount))
                _receivedEvent?.Release();
        };
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
        var signal = BeginMeasure();

        try
        {
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

            await WaitDeliveryAsync(signal);
        }
        finally
        {
            _receivedEvent = null;
        }
    }
    #endregion

    #region QoS1 吞吐（需要 PubAck 确认）
    [Benchmark(Description = "QoS1 端到端吞吐")]
    public async Task QoS1_Throughput()
    {
        var signal = BeginMeasure();

        try
        {
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

            await WaitDeliveryAsync(signal);
        }
        finally
        {
            _receivedEvent = null;
        }
    }
    #endregion

    #region 辅助
    /// <summary>开始一次测量：重置接收计数并登记唤醒信号</summary>
    /// <returns></returns>
    private SemaphoreSlim BeginMeasure()
    {
        var signal = new SemaphoreSlim(0);
        Volatile.Write(ref _targetCount, MessageCount);
        _receivedEvent = signal;
        Interlocked.Exchange(ref _receivedCount, 0);

        return signal;
    }

    /// <summary>等待全部消息投递完成（最长 5 秒）</summary>
    /// <param name="signal">接收完成信号</param>
    /// <returns></returns>
    private static async Task WaitDeliveryAsync(SemaphoreSlim signal)
    {
        using var cts = new CancellationTokenSource(5000);
        try
        {
            await signal.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }
    #endregion
}
