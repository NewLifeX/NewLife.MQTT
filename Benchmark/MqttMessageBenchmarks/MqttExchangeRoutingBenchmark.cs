using BenchmarkDotNet.Attributes;
using NewLife;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 交换机路由性能基准测试。对比旧版全表扫描与新版首层段前缀索引在大规模过滤器下的路由开销</summary>
/// <remarks>
/// 场景：单会话订阅 N 个不同前缀的过滤器（device{i}/data/#），发布到其中一个前缀的主题。
/// 旧实现需要遍历全部 N 个过滤器逐一匹配；新实现按首层主题段定位，仅遍历同前缀过滤器。
/// 两侧均包含单条投递的消息对象创建，差异集中在扫描与匹配路径。
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class MqttExchangeRoutingBenchmark
{
    private MqttExchange _exchange = null!;
    private FakeHandler _handler = null!;
    private PublishMessage _message = null!;
    private String[] _filters = null!;

    // 防止 JIT 消除基准逻辑的落点
    private PublishMessage? _sink;
    private Int32 _legacyDelivered;

    /// <summary>注册的过滤器数量，模拟不同规模的订阅表</summary>
    [Params(1, 100, 10000)]
    public Int32 FilterCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // 过滤器分散在不同首层段，发布主题只命中其中一个
        _filters = new String[FilterCount];
        for (var i = 0; i < FilterCount; i++)
        {
            _filters[i] = $"device{i}/data/#";
        }

        _exchange = new MqttExchange { EnableSysTopic = false };
        _handler = new FakeHandler();
        _exchange.Add(1, _handler);
        foreach (var filter in _filters)
        {
            _exchange.Subscribe(1, filter, QualityOfService.AtMostOnce);
        }

        _message = new PublishMessage
        {
            Topic = $"device{FilterCount / 2}/data/temperature",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket("{\"t\":25.6}"u8.ToArray()),
        };
    }

    [GlobalCleanup]
    public void Cleanup() => _exchange.TryDispose();

    [Benchmark(Baseline = true, OperationsPerInvoke = 100, Description = "旧实现：全表扫描匹配")]
    public void Legacy_FullScan()
    {
        var topic = _message.Topic;
        for (var n = 0; n < 100; n++)
        {
            for (var i = 0; i < _filters.Length; i++)
            {
                if (!LegacyTopicMatcher.IsMatch(topic, _filters[i])) continue;

                // 模拟投递（等价于旧实现每消息创建投递对象）
                _sink = new PublishMessage
                {
                    Topic = topic,
                    Payload = _message.Payload,
                    QoS = QualityOfService.AtMostOnce,
                };
                _legacyDelivered++;
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 100, Description = "新实现：前缀索引路由")]
    public void Exchange_Route()
    {
        for (var i = 0; i < 100; i++)
        {
            _exchange.Publish(_message);
        }
    }

    /// <summary>基准用会话处理器。仅统计投递数，避免网络开销干扰</summary>
    private sealed class FakeHandler : IMqttHandler
    {
        private Int64 _count;

        /// <summary>已投递消息数</summary>
        public Int64 Count => Interlocked.Read(ref _count);

        public MqttMessage? Process(MqttMessage message) => null;

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce) => Task.FromResult((MqttIdMessage?)null);

        public Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce) => Task.FromResult((MqttIdMessage?)null);

        public Task<MqttIdMessage?> PublishAsync(PublishMessage message)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult((MqttIdMessage?)null);
        }

        public void Close(String reason) { }
    }
}
