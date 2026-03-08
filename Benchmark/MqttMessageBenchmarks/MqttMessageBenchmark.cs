using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 消息编解码性能基准测试（单线程）。测量服务端单次消息处理吞吐量和内存分配</summary>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class MqttMessageBenchmark
{
    private readonly MqttFactory _factory = new();

    private static Byte[] GetBytes(MqttMessage message)
    {
        using var pk = message.ToPacket();
        return pk.GetSpan().ToArray();
    }

    #region 预构建消息与二进制数据
    private PublishMessage _publishQoS0 = null!;
    private PublishMessage _publishQoS1 = null!;
    private ConnectMessage _connectMsg = null!;
    private SubscribeMessage _subscribeMsg = null!;
    private PingRequest _pingReq = null!;

    private Byte[] _publishQoS0Bytes = null!;
    private Byte[] _publishQoS1Bytes = null!;
    private Byte[] _connectBytes = null!;
    private Byte[] _subscribeBytes = null!;
    private Byte[] _pingReqBytes = null!;
    private Byte[] _publishLargeBytes = null!;
    #endregion

    [GlobalSetup]
    public void Setup()
    {
        // QoS0 Publish（服务端最常见消息）
        _publishQoS0 = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket("{\"t\":25.6,\"h\":60,\"ts\":1700000000}"u8.ToArray()),
        };
        _publishQoS0Bytes = GetBytes(_publishQoS0);

        // QoS1 Publish
        _publishQoS1 = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtLeastOnce,
            Id = 1,
            Payload = new ArrayPacket("{\"t\":25.6,\"h\":60,\"ts\":1700000000}"u8.ToArray()),
        };
        _publishQoS1Bytes = GetBytes(_publishQoS1);

        // Connect（客户端连接时服务端解析）
        _connectMsg = new ConnectMessage
        {
            ClientId = "device_sensor_001",
            Username = "admin",
            Password = "password123",
            CleanSession = true,
            KeepAliveInSeconds = 60,
            ProtocolLevel = MqttVersion.V311,
        };
        _connectBytes = GetBytes(_connectMsg);

        // Subscribe（订阅请求）
        _subscribeMsg = new SubscribeMessage
        {
            Id = 1,
            Requests =
            [
                new Subscription("sensor/+/temperature", QualityOfService.AtLeastOnce),
                new Subscription("actuator/#", QualityOfService.ExactlyOnce),
            ],
        };
        _subscribeBytes = GetBytes(_subscribeMsg);

        // Ping
        _pingReq = new PingRequest();
        _pingReqBytes = GetBytes(_pingReq);

        // 大负载 Publish（1KB 载荷）
        var largePub = new PublishMessage
        {
            Topic = "data/bulk/upload",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(new Byte[1024]),
        };
        _publishLargeBytes = GetBytes(largePub);
    }

    #region 反序列化（服务端收到消息后解码）
    [Benchmark(Description = "反序列化 Publish QoS0")]
    public MqttMessage? Deserialize_Publish_QoS0()
    {
        return _factory.ReadMessage(new ArrayPacket(_publishQoS0Bytes));
    }

    [Benchmark(Description = "反序列化 Publish QoS1")]
    public MqttMessage? Deserialize_Publish_QoS1()
    {
        return _factory.ReadMessage(new ArrayPacket(_publishQoS1Bytes));
    }

    [Benchmark(Description = "反序列化 Connect")]
    public MqttMessage? Deserialize_Connect()
    {
        return _factory.ReadMessage(new ArrayPacket(_connectBytes));
    }

    [Benchmark(Description = "反序列化 Subscribe")]
    public MqttMessage? Deserialize_Subscribe()
    {
        return _factory.ReadMessage(new ArrayPacket(_subscribeBytes));
    }

    [Benchmark(Description = "反序列化 Ping")]
    public MqttMessage? Deserialize_Ping()
    {
        return _factory.ReadMessage(new ArrayPacket(_pingReqBytes));
    }

    [Benchmark(Description = "反序列化 Publish 1KB")]
    public MqttMessage? Deserialize_Publish_Large()
    {
        return _factory.ReadMessage(new ArrayPacket(_publishLargeBytes));
    }
    #endregion

    #region 序列化（服务端发送消息前编码）
    [Benchmark(Description = "序列化 Publish QoS0")]
    public Int32 Serialize_Publish_QoS0()
    {
        using var pk = _publishQoS0.ToPacket();
        return pk.Total;
    }

    [Benchmark(Description = "序列化 Publish QoS1")]
    public Int32 Serialize_Publish_QoS1()
    {
        using var pk = _publishQoS1.ToPacket();
        return pk.Total;
    }

    [Benchmark(Description = "序列化 Connect")]
    public Int32 Serialize_Connect()
    {
        using var pk = _connectMsg.ToPacket();
        return pk.Total;
    }

    [Benchmark(Description = "序列化 Subscribe")]
    public Int32 Serialize_Subscribe()
    {
        using var pk = _subscribeMsg.ToPacket();
        return pk.Total;
    }

    [Benchmark(Description = "序列化 Ping")]
    public Int32 Serialize_Ping()
    {
        using var pk = _pingReq.ToPacket();
        return pk.Total;
    }
    #endregion
}
