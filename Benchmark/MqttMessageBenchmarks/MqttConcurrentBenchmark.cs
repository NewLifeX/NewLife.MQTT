using BenchmarkDotNet.Attributes;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 消息编解码并发吞吐基准测试。测量服务端多线程处理能力</summary>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class MqttConcurrentBenchmark
{
    private readonly MqttFactory _factory = new();
    private PublishMessage _publishQoS0 = null!;
    private Byte[] _publishQoS0Bytes = null!;

    private static Byte[] GetBytes(MqttMessage message)
    {
        using var pk = message.ToPacket();
        return pk.GetSpan().ToArray();
    }

    /// <summary>动态线程数，包含 CPU 核心数</summary>
    public static IEnumerable<Int32> ThreadCounts
    {
        get
        {
            var cores = Environment.ProcessorCount;
            var set = new SortedSet<Int32> { 1, 4, 8, 32 };
            set.Add(cores);
            return set;
        }
    }

    [ParamsSource(nameof(ThreadCounts))]
    public Int32 ThreadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _publishQoS0 = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket("{\"t\":25.6,\"h\":60,\"ts\":1700000000}"u8.ToArray()),
        };
        _publishQoS0Bytes = GetBytes(_publishQoS0);
    }

    #region 多线程并发吞吐
    [Benchmark(Description = "并发反序列化 Publish QoS0")]
    public void Concurrent_Deserialize_Publish()
    {
        var tasks = new Task[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 1000; j++)
                {
                    _factory.ReadMessage(new ArrayPacket(_publishQoS0Bytes));
                }
            });
        }
        Task.WaitAll(tasks);
    }

    [Benchmark(Description = "并发序列化 Publish QoS0")]
    public void Concurrent_Serialize_Publish()
    {
        var tasks = new Task[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 1000; j++)
                {
                    using var pk = _publishQoS0.ToPacket();
                }
            });
        }
        Task.WaitAll(tasks);
    }

    [Benchmark(Description = "并发往返 Publish QoS0")]
    public void Concurrent_RoundTrip_Publish()
    {
        var tasks = new Task[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 1000; j++)
                {
                    using var pk = _publishQoS0.ToPacket();
                    _factory.ReadMessage(pk);
                }
            });
        }
        Task.WaitAll(tasks);
    }
    #endregion
}
