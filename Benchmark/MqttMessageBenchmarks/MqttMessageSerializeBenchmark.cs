using BenchmarkDotNet.Attributes;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 消息编解码性能基准测试。重点细化 Publish 消息按载荷大小和是否携带 Properties 的性能差异，同时覆盖其余消息类型作为基线</summary>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MqttMessageSerializeBenchmark
{
    private readonly MqttFactory _factory = new();

    #region 辅助方法
    private static Byte[] GetBytes(MqttMessage message)
    {
        using var pk = message.ToPacket();
        return pk.GetSpan().ToArray();
    }

    private static MqttProperties CreatePublishProperties()
    {
        var props = new MqttProperties();
        props.SetByte(MqttPropertyId.PayloadFormatIndicator, 1);
        props.SetUInt32(MqttPropertyId.MessageExpiryInterval, 300);
        props.SetString(MqttPropertyId.ContentType, "application/json");
        props.SetString(MqttPropertyId.ResponseTopic, "reply/device001");
        props.SetBinary(MqttPropertyId.CorrelationData, "req-001"u8.ToArray());
        props.UserProperties.Add(new KeyValuePair<String, String>("trace-id", "abc123"));
        return props;
    }

    private static PublishMessage CreatePublish(Int32 payloadSize, QualityOfService qos, MqttProperties? props = null)
    {
        var msg = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = qos,
            Payload = payloadSize > 0 ? new ArrayPacket(new Byte[payloadSize]) : new ArrayPacket([]),
            Properties = props,
        };
        if (qos > 0) msg.Id = 1;
        return msg;
    }
    #endregion

    #region 预构建消息对象

    // Publish 消息矩阵：载荷大小 × QoS × 是否携带 Properties
    // 载荷大小：0B（空通知）、34B（传感器JSON）、256B（中等）、1KB（大负载）、4KB（超大负载）
    private static readonly Byte[] _payload34 = "{\"t\":25.6,\"h\":60,\"ts\":1700000000}"u8.ToArray();

    // V3.1.1 Publish（无 Properties）
    private PublishMessage _pub311_0B = null!;
    private PublishMessage _pub311_34B = null!;
    private PublishMessage _pub311_256B = null!;
    private PublishMessage _pub311_1KB = null!;
    private PublishMessage _pub311_4KB = null!;

    // V3.1.1 Publish QoS1（测 Id 字段影响）
    private PublishMessage _pub311_34B_Q1 = null!;
    private PublishMessage _pub311_1KB_Q1 = null!;

    // V5.0 Publish（携带 Properties）
    private PublishMessage _pub500_0B = null!;
    private PublishMessage _pub500_34B = null!;
    private PublishMessage _pub500_256B = null!;
    private PublishMessage _pub500_1KB = null!;
    private PublishMessage _pub500_4KB = null!;

    // V5.0 Publish QoS1（测 Id + Properties）
    private PublishMessage _pub500_34B_Q1 = null!;
    private PublishMessage _pub500_1KB_Q1 = null!;

    // 反序列化用预编码字节
    private Byte[] _pub311_0B_Bytes = null!;
    private Byte[] _pub311_34B_Bytes = null!;
    private Byte[] _pub311_256B_Bytes = null!;
    private Byte[] _pub311_1KB_Bytes = null!;
    private Byte[] _pub311_4KB_Bytes = null!;
    private Byte[] _pub311_34B_Q1_Bytes = null!;
    private Byte[] _pub311_1KB_Q1_Bytes = null!;

    private Byte[] _pub500_0B_Bytes = null!;
    private Byte[] _pub500_34B_Bytes = null!;
    private Byte[] _pub500_256B_Bytes = null!;
    private Byte[] _pub500_1KB_Bytes = null!;
    private Byte[] _pub500_4KB_Bytes = null!;
    private Byte[] _pub500_34B_Q1_Bytes = null!;
    private Byte[] _pub500_1KB_Q1_Bytes = null!;

    // 其余消息类型（基线参考）
    private ConnectMessage _connect311 = null!;
    private ConnAck _connAck311 = null!;
    private PubAck _pubAck311 = null!;
    private SubscribeMessage _subscribe311 = null!;
    private PingRequest _pingReq = null!;
    private DisconnectMessage _disconnect311 = null!;
    private ConnectMessage _connect500 = null!;
    private AuthMessage _auth500 = null!;

    private Byte[] _connect311Bytes = null!;
    private Byte[] _connAck311Bytes = null!;
    private Byte[] _pubAck311Bytes = null!;
    private Byte[] _subscribe311Bytes = null!;
    private Byte[] _pingReqBytes = null!;
    private Byte[] _disconnect311Bytes = null!;
    private Byte[] _connect500Bytes = null!;
    private Byte[] _auth500Bytes = null!;
    #endregion

    [GlobalSetup]
    public void Setup()
    {
        #region Publish 消息矩阵构建
        // V3.1.1 QoS0
        _pub311_0B = CreatePublish(0, QualityOfService.AtMostOnce);
        _pub311_34B = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(_payload34),
        };
        _pub311_256B = CreatePublish(256, QualityOfService.AtMostOnce);
        _pub311_1KB = CreatePublish(1024, QualityOfService.AtMostOnce);
        _pub311_4KB = CreatePublish(4096, QualityOfService.AtMostOnce);

        // V3.1.1 QoS1
        _pub311_34B_Q1 = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtLeastOnce,
            Id = 1,
            Payload = new ArrayPacket(_payload34),
        };
        _pub311_1KB_Q1 = CreatePublish(1024, QualityOfService.AtLeastOnce);

        // V5.0 QoS0（携带 Properties）
        _pub500_0B = CreatePublish(0, QualityOfService.AtMostOnce, CreatePublishProperties());
        _pub500_34B = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(_payload34),
            Properties = CreatePublishProperties(),
        };
        _pub500_256B = CreatePublish(256, QualityOfService.AtMostOnce, CreatePublishProperties());
        _pub500_1KB = CreatePublish(1024, QualityOfService.AtMostOnce, CreatePublishProperties());
        _pub500_4KB = CreatePublish(4096, QualityOfService.AtMostOnce, CreatePublishProperties());

        // V5.0 QoS1（携带 Properties + Id）
        _pub500_34B_Q1 = new PublishMessage
        {
            Topic = "sensor/temperature/device001",
            QoS = QualityOfService.AtLeastOnce,
            Id = 1,
            Payload = new ArrayPacket(_payload34),
            Properties = CreatePublishProperties(),
        };
        _pub500_1KB_Q1 = CreatePublish(1024, QualityOfService.AtLeastOnce, CreatePublishProperties());
        #endregion

        #region 其余消息类型构建
        _connect311 = new ConnectMessage
        {
            ClientId = "device_sensor_001",
            Username = "admin",
            Password = "password123",
            CleanSession = true,
            KeepAliveInSeconds = 60,
            ProtocolLevel = MqttVersion.V311,
        };

        _connAck311 = new ConnAck
        {
            SessionPresent = false,
            ReturnCode = ConnectReturnCode.Accepted,
        };

        _pubAck311 = new PubAck { Id = 1 };

        _subscribe311 = new SubscribeMessage
        {
            Id = 1,
            Requests =
            [
                new Subscription("sensor/+/temperature", QualityOfService.AtLeastOnce),
                new Subscription("actuator/#", QualityOfService.ExactlyOnce),
            ],
        };

        _pingReq = new PingRequest();
        _disconnect311 = new DisconnectMessage();

        _connect500 = new ConnectMessage
        {
            ClientId = "device_sensor_001",
            Username = "admin",
            Password = "password123",
            CleanSession = true,
            KeepAliveInSeconds = 60,
            ProtocolLevel = MqttVersion.V500,
            Properties = new MqttProperties(),
        };
        _connect500.Properties.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);
        _connect500.Properties.SetUInt16(MqttPropertyId.ReceiveMaximum, 100);
        _connect500.Properties.SetUInt32(MqttPropertyId.MaximumPacketSize, 65535);
        _connect500.Properties.SetUInt16(MqttPropertyId.TopicAliasMaximum, 10);
        _connect500.Properties.SetString(MqttPropertyId.AuthenticationMethod, "SCRAM-SHA-256");
        _connect500.Properties.UserProperties.Add(new KeyValuePair<String, String>("client-version", "2.0"));

        _auth500 = new AuthMessage
        {
            ReasonCode = 0x18,
            Properties = new MqttProperties(),
        };
        _auth500.Properties.SetString(MqttPropertyId.AuthenticationMethod, "SCRAM-SHA-256");
        _auth500.Properties.SetBinary(MqttPropertyId.AuthenticationData, new Byte[32]);
        _auth500.Properties.SetString(MqttPropertyId.ReasonString, "Continue");
        #endregion

        #region 预序列化二进制数据
        _pub311_0B_Bytes = GetBytes(_pub311_0B);
        _pub311_34B_Bytes = GetBytes(_pub311_34B);
        _pub311_256B_Bytes = GetBytes(_pub311_256B);
        _pub311_1KB_Bytes = GetBytes(_pub311_1KB);
        _pub311_4KB_Bytes = GetBytes(_pub311_4KB);
        _pub311_34B_Q1_Bytes = GetBytes(_pub311_34B_Q1);
        _pub311_1KB_Q1_Bytes = GetBytes(_pub311_1KB_Q1);

        _pub500_0B_Bytes = GetBytes(_pub500_0B);
        _pub500_34B_Bytes = GetBytes(_pub500_34B);
        _pub500_256B_Bytes = GetBytes(_pub500_256B);
        _pub500_1KB_Bytes = GetBytes(_pub500_1KB);
        _pub500_4KB_Bytes = GetBytes(_pub500_4KB);
        _pub500_34B_Q1_Bytes = GetBytes(_pub500_34B_Q1);
        _pub500_1KB_Q1_Bytes = GetBytes(_pub500_1KB_Q1);

        _connect311Bytes = GetBytes(_connect311);
        _connAck311Bytes = GetBytes(_connAck311);
        _pubAck311Bytes = GetBytes(_pubAck311);
        _subscribe311Bytes = GetBytes(_subscribe311);
        _pingReqBytes = GetBytes(_pingReq);
        _disconnect311Bytes = GetBytes(_disconnect311);
        _connect500Bytes = GetBytes(_connect500);
        _auth500Bytes = GetBytes(_auth500);
        #endregion
    }

    #region 序列化 Publish V3.1.1（按载荷大小）
    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS0 0B V3.1.1")]
    public Int32 Serialize_Pub311_0B()
    {
        using var pk = _pub311_0B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS0 34B V3.1.1")]
    public Int32 Serialize_Pub311_34B()
    {
        using var pk = _pub311_34B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS0 256B V3.1.1")]
    public Int32 Serialize_Pub311_256B()
    {
        using var pk = _pub311_256B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS0 1KB V3.1.1")]
    public Int32 Serialize_Pub311_1KB()
    {
        using var pk = _pub311_1KB.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS0 4KB V3.1.1")]
    public Int32 Serialize_Pub311_4KB()
    {
        using var pk = _pub311_4KB.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS1 34B V3.1.1")]
    public Int32 Serialize_Pub311_34B_Q1()
    {
        using var pk = _pub311_34B_Q1.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V311")]
    [Benchmark(Description = "序列化 Pub QoS1 1KB V3.1.1")]
    public Int32 Serialize_Pub311_1KB_Q1()
    {
        using var pk = _pub311_1KB_Q1.ToPacket();
        return pk.Total;
    }
    #endregion

    #region 序列化 Publish V5.0（携带 Properties，按载荷大小）
    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS0 0B V5.0")]
    public Int32 Serialize_Pub500_0B()
    {
        using var pk = _pub500_0B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS0 34B V5.0")]
    public Int32 Serialize_Pub500_34B()
    {
        using var pk = _pub500_34B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS0 256B V5.0")]
    public Int32 Serialize_Pub500_256B()
    {
        using var pk = _pub500_256B.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS0 1KB V5.0")]
    public Int32 Serialize_Pub500_1KB()
    {
        using var pk = _pub500_1KB.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS0 4KB V5.0")]
    public Int32 Serialize_Pub500_4KB()
    {
        using var pk = _pub500_4KB.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS1 34B V5.0")]
    public Int32 Serialize_Pub500_34B_Q1()
    {
        using var pk = _pub500_34B_Q1.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Pub_V500")]
    [Benchmark(Description = "序列化 Pub QoS1 1KB V5.0")]
    public Int32 Serialize_Pub500_1KB_Q1()
    {
        using var pk = _pub500_1KB_Q1.ToPacket();
        return pk.Total;
    }
    #endregion

    #region 反序列化 Publish V3.1.1（按载荷大小）
    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS0 0B V3.1.1")]
    public MqttMessage? Deserialize_Pub311_0B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_0B_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS0 34B V3.1.1")]
    public MqttMessage? Deserialize_Pub311_34B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_34B_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS0 256B V3.1.1")]
    public MqttMessage? Deserialize_Pub311_256B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_256B_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS0 1KB V3.1.1")]
    public MqttMessage? Deserialize_Pub311_1KB()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_1KB_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS0 4KB V3.1.1")]
    public MqttMessage? Deserialize_Pub311_4KB()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_4KB_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS1 34B V3.1.1")]
    public MqttMessage? Deserialize_Pub311_34B_Q1()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_34B_Q1_Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Pub_V311")]
    [Benchmark(Description = "反序列化 Pub QoS1 1KB V3.1.1")]
    public MqttMessage? Deserialize_Pub311_1KB_Q1()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub311_1KB_Q1_Bytes), MqttVersion.V311);
    }
    #endregion

    #region 反序列化 Publish V5.0（携带 Properties，按载荷大小）
    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS0 0B V5.0")]
    public MqttMessage? Deserialize_Pub500_0B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_0B_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS0 34B V5.0")]
    public MqttMessage? Deserialize_Pub500_34B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_34B_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS0 256B V5.0")]
    public MqttMessage? Deserialize_Pub500_256B()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_256B_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS0 1KB V5.0")]
    public MqttMessage? Deserialize_Pub500_1KB()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_1KB_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS0 4KB V5.0")]
    public MqttMessage? Deserialize_Pub500_4KB()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_4KB_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS1 34B V5.0")]
    public MqttMessage? Deserialize_Pub500_34B_Q1()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_34B_Q1_Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Pub_V500")]
    [Benchmark(Description = "反序列化 Pub QoS1 1KB V5.0")]
    public MqttMessage? Deserialize_Pub500_1KB_Q1()
    {
        return _factory.ReadMessage(new ArrayPacket(_pub500_1KB_Q1_Bytes), MqttVersion.V500);
    }
    #endregion

    #region 序列化 其余消息基线
    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 Connect V3.1.1")]
    public Int32 Serialize_Connect_V311()
    {
        using var pk = _connect311.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 ConnAck V3.1.1")]
    public Int32 Serialize_ConnAck_V311()
    {
        using var pk = _connAck311.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 PubAck V3.1.1")]
    public Int32 Serialize_PubAck_V311()
    {
        using var pk = _pubAck311.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 Subscribe V3.1.1")]
    public Int32 Serialize_Subscribe_V311()
    {
        using var pk = _subscribe311.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 PingReq")]
    public Int32 Serialize_PingReq()
    {
        using var pk = _pingReq.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 Disconnect V3.1.1")]
    public Int32 Serialize_Disconnect_V311()
    {
        using var pk = _disconnect311.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 Connect V5.0")]
    public Int32 Serialize_Connect_V500()
    {
        using var pk = _connect500.ToPacket();
        return pk.Total;
    }

    [BenchmarkCategory("Serialize_Baseline")]
    [Benchmark(Description = "序列化 Auth V5.0")]
    public Int32 Serialize_Auth_V500()
    {
        using var pk = _auth500.ToPacket();
        return pk.Total;
    }
    #endregion

    #region 反序列化 其余消息基线
    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 Connect V3.1.1")]
    public MqttMessage? Deserialize_Connect_V311()
    {
        return _factory.ReadMessage(new ArrayPacket(_connect311Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 ConnAck V3.1.1")]
    public MqttMessage? Deserialize_ConnAck_V311()
    {
        return _factory.ReadMessage(new ArrayPacket(_connAck311Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 PubAck V3.1.1")]
    public MqttMessage? Deserialize_PubAck_V311()
    {
        return _factory.ReadMessage(new ArrayPacket(_pubAck311Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 Subscribe V3.1.1")]
    public MqttMessage? Deserialize_Subscribe_V311()
    {
        return _factory.ReadMessage(new ArrayPacket(_subscribe311Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 PingReq")]
    public MqttMessage? Deserialize_PingReq()
    {
        return _factory.ReadMessage(new ArrayPacket(_pingReqBytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 Disconnect V3.1.1")]
    public MqttMessage? Deserialize_Disconnect_V311()
    {
        return _factory.ReadMessage(new ArrayPacket(_disconnect311Bytes), MqttVersion.V311);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 Connect V5.0")]
    public MqttMessage? Deserialize_Connect_V500()
    {
        return _factory.ReadMessage(new ArrayPacket(_connect500Bytes), MqttVersion.V500);
    }

    [BenchmarkCategory("Deserialize_Baseline")]
    [Benchmark(Description = "反序列化 Auth V5.0")]
    public MqttMessage? Deserialize_Auth_V500()
    {
        return _factory.ReadMessage(new ArrayPacket(_auth500Bytes), MqttVersion.V500);
    }
    #endregion
}
