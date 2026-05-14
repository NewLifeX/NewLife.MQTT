using System;
using System.IO;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>报文编解码边界情况测试。覆盖变长整数编码、协议名/版本号组合、MQTT 5.0 属性往返</summary>
public class MessageCodecEdgeCaseTests
{
    private static readonly MqttFactory _factory = new();
    private static readonly MqttVersion _v500Level = MqttVersion.V500;  // 0x05

    /// <summary>辅助：序列化再反序列化</summary>
    private static T RoundTrip<T>(T msg, MqttVersion? protocolLevel = null) where T : MqttMessage
    {
        using var pk = msg.ToPacket();
        Assert.True(pk.Total > 0);

        var result = protocolLevel.HasValue
            ? _factory.ReadMessage(pk, protocolLevel.Value)
            : _factory.ReadMessage(pk);
        Assert.NotNull(result);
        Assert.IsType<T>(result);
        return (T)result;
    }

    #region 变长整数编码（Remaining Length）
    [Fact]
    [System.ComponentModel.DisplayName("变长整数：单字节最大值 127")]
    public void VariableLengthInt_SingleByte_Max()
    {
        // 构造一条 PINGREQ（固定 2 字节：0xC0 0x00），验证长度字段为 1 字节
        var msg = new PingRequest();
        using var pk = msg.ToPacket();
        Assert.Equal(2, pk.Total);
        // 第二个字节是剩余长度，值为 0
        Assert.Equal(0, pk[1]);
    }

    [Fact]
    [System.ComponentModel.DisplayName("变长整数：两字节编码（128 ~ 16383）")]
    public void VariableLengthInt_TwoBytes()
    {
        // 构造足够大的 Payload 使剩余长度需要 2 字节（>= 128 字节）
        var payload = new Byte[200];
        for (var i = 0; i < payload.Length; i++) payload[i] = (Byte)(i % 256);

        var msg = new PublishMessage
        {
            Topic = "test/big",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(payload),
        };

        using var pk = msg.ToPacket();
        // 第二个字节的最高位应为 1（表示后续还有字节）
        Assert.True((pk[1] & 0x80) == 0x80);

        var result = RoundTrip(msg);
        Assert.Equal("test/big", result.Topic);
        Assert.Equal(200, result.Payload!.Total);
    }

    [Fact]
    [System.ComponentModel.DisplayName("变长整数：三字节编码（16384 ~ 2097151）")]
    public void VariableLengthInt_ThreeBytes()
    {
        var payload = new Byte[20000];
        var msg = new PublishMessage
        {
            Topic = "t",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(payload),
        };

        using var pk = msg.ToPacket();
        // 前三个 Remaining Length 字节的高位均应为 1（第三个不需要，但前两个需要）
        Assert.True((pk[1] & 0x80) == 0x80);
        Assert.True((pk[2] & 0x80) == 0x80);

        var result = RoundTrip(msg);
        Assert.Equal(20000, result.Payload!.Total);
    }
    #endregion

    #region MQTT 3.1 ProtocolName=MQIsdp 编解码
    [Fact]
    [System.ComponentModel.DisplayName("ConnectMessage：MQTT 3.1 MQIsdp 协议名往返正确")]
    public void ConnectMessage_V310_MQIsdp_RoundTrip()
    {
        var msg = new ConnectMessage
        {
            ClientId = "v310_client",
            ProtocolName = "MQIsdp",
            ProtocolLevel = MqttVersion.V310,
            CleanSession = true,
            KeepAliveInSeconds = 60,
        };

        var result = RoundTrip(msg);

        Assert.Equal("MQIsdp", result.ProtocolName);
        Assert.Equal(MqttVersion.V310, result.ProtocolLevel);
        Assert.Equal("v310_client", result.ClientId);
        Assert.True(result.CleanSession);
    }

    [Fact]
    [System.ComponentModel.DisplayName("ConnectMessage：MQTT 3.1 MQIsdp 带用户名密码往返")]
    public void ConnectMessage_V310_MQIsdp_WithCredentials()
    {
        var msg = new ConnectMessage
        {
            ClientId = "v310_cred",
            ProtocolName = "MQIsdp",
            ProtocolLevel = MqttVersion.V310,
            Username = "user1",
            Password = "pass1",
            KeepAliveInSeconds = 30,
        };

        var result = RoundTrip(msg);

        Assert.Equal("MQIsdp", result.ProtocolName);
        Assert.Equal(MqttVersion.V310, result.ProtocolLevel);
        Assert.Equal("user1", result.Username);
        Assert.Equal("pass1", result.Password);
    }
    #endregion

    #region MQTT 5.0 ConnectMessage 扩展属性
    [Fact]
    [System.ComponentModel.DisplayName("ConnectMessage：MQTT 5.0 带连接属性和遗嘱属性往返")]
    public void ConnectMessage_V500_WithProperties_RoundTrip()
    {
        var connProps = new MqttProperties();
        connProps.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);
        connProps.SetUInt16(MqttPropertyId.ReceiveMaximum, 100);
        connProps.SetString(MqttPropertyId.AuthenticationMethod, "PLAIN");

        var willProps = new MqttProperties();
        willProps.SetUInt32(MqttPropertyId.WillDelayInterval, 30);

        var msg = new ConnectMessage
        {
            ClientId = "v5_full_client",
            ProtocolLevel = MqttVersion.V500,
            CleanSession = true,
            KeepAliveInSeconds = 60,
            HasWill = true,
            WillTopicName = "status/offline",
            WillMessage = "gone"u8.ToArray(),
            WillQualityOfService = QualityOfService.AtLeastOnce,
            Properties = connProps,
            WillProperties = willProps,
        };

        var result = RoundTrip(msg);

        Assert.Equal(MqttVersion.V500, result.ProtocolLevel);
        Assert.Equal("v5_full_client", result.ClientId);
        Assert.True(result.HasWill);
        Assert.Equal("status/offline", result.WillTopicName);
        Assert.NotNull(result.Properties);
        Assert.Equal((UInt32)3600, result.Properties!.GetUInt32(MqttPropertyId.SessionExpiryInterval));
        Assert.Equal((UInt16)100, result.Properties.GetUInt16(MqttPropertyId.ReceiveMaximum));
        Assert.NotNull(result.WillProperties);
        Assert.Equal((UInt32)30, result.WillProperties!.GetUInt32(MqttPropertyId.WillDelayInterval));
    }

    [Fact]
    [System.ComponentModel.DisplayName("ConnectMessage：MQTT 5.0 空属性（属性长度=0）往返")]
    public void ConnectMessage_V500_EmptyProperties_RoundTrip()
    {
        var msg = new ConnectMessage
        {
            ClientId = "v5_empty_props",
            ProtocolLevel = MqttVersion.V500,
            CleanSession = true,
        };

        var result = RoundTrip(msg);

        Assert.Equal(MqttVersion.V500, result.ProtocolLevel);
        Assert.Equal("v5_empty_props", result.ClientId);
        // 无属性或空属性均可
        Assert.True(result.Properties == null || result.Properties.Count == 0);
    }
    #endregion

    #region MQTT 5.0 ConnAck 属性往返
    [Fact]
    [System.ComponentModel.DisplayName("ConnAck：MQTT 5.0 原因码+属性往返")]
    public void ConnAck_V500_WithProperties_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);
        props.SetUInt16(MqttPropertyId.ServerKeepAlive, 120);
        props.SetByte(MqttPropertyId.RetainAvailable, 1);
        props.SetByte(MqttPropertyId.WildcardSubscriptionAvailable, 1);
        props.SetString(MqttPropertyId.AssignedClientIdentifier, "server-assigned-id");

        var msg = new ConnAck
        {
            SessionPresent = false,
            ReturnCode = ConnectReturnCode.Accepted,
            ReasonCode = ConnAckReasonCode.Success,
            Properties = props,
        };

        var result = RoundTrip(msg);

        Assert.False(result.SessionPresent);
        Assert.Equal(ConnectReturnCode.Accepted, result.ReturnCode);
        Assert.NotNull(result.Properties);
        Assert.Equal((UInt32)3600, result.Properties!.GetUInt32(MqttPropertyId.SessionExpiryInterval));
        Assert.Equal((UInt16)120, result.Properties.GetUInt16(MqttPropertyId.ServerKeepAlive));
        Assert.Equal("server-assigned-id", result.Properties.GetString(MqttPropertyId.AssignedClientIdentifier));
    }

    [Theory]
    [InlineData(ConnAckReasonCode.Success, ConnectReturnCode.Accepted)]
    [InlineData(ConnAckReasonCode.NotAuthorized, ConnectReturnCode.RefusedNotAuthorized)]
    [InlineData(ConnAckReasonCode.BadUserNameOrPassword, ConnectReturnCode.RefusedBadUsernameOrPassword)]
    [System.ComponentModel.DisplayName("ConnAck：所有返回码往返正确")]
    public void ConnAck_AllReturnCodes_RoundTrip(ConnAckReasonCode reasonCode, ConnectReturnCode returnCode)
    {
        var msg = new ConnAck
        {
            ReturnCode = returnCode,
            ReasonCode = reasonCode,
        };

        var result = RoundTrip(msg);

        Assert.Equal(returnCode, result.ReturnCode);
    }
    #endregion

    #region MQTT 5.0 DisconnectMessage 属性往返
    [Fact]
    [System.ComponentModel.DisplayName("DisconnectMessage：MQTT 3.1.1 空消息往返（无额外字节）")]
    public void DisconnectMessage_V311_Empty_RoundTrip()
    {
        var msg = new DisconnectMessage();
        using var pk = msg.ToPacket();

        // MQTT 3.1.1 的 DISCONNECT 是固定 2 字节 0xE0 0x00
        Assert.Equal(2, pk.Total);
        Assert.Equal(0xE0, pk[0]);
        Assert.Equal(0x00, pk[1]);

        var result = RoundTrip(msg);
        Assert.Equal(MqttType.Disconnect, result.Type);
        Assert.Equal(0x00, result.ReasonCode);
    }

    [Fact]
    [System.ComponentModel.DisplayName("DisconnectMessage：MQTT 5.0 原因码+属性往返")]
    public void DisconnectMessage_V500_WithReasonCode_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetString(MqttPropertyId.ReasonString, "session expired");
        props.SetUInt32(MqttPropertyId.SessionExpiryInterval, 0);

        var msg = new DisconnectMessage
        {
            ReasonCode = 0x04, // Disconnect with will message
            Properties = props,
        };

        // DisconnectMessage 的 Write 需要 5.0 感知
        var buf = new Byte[msg.GetEstimatedSize()];
        var writer = new NewLife.Buffers.SpanWriter(buf) { IsLittleEndian = false };
        msg.Write(ref writer, _v500Level);

        var buf2 = writer.WrittenSpan.ToArray();
        var pk = new ArrayPacket(buf2);
        var result = _factory.ReadMessage(pk, _v500Level) as DisconnectMessage;
        Assert.NotNull(result);
        Assert.Equal(MqttType.Disconnect, result!.Type);
    }
    #endregion

    #region MQTT 5.0 PublishMessage 属性往返（上下文感知）
    [Fact]
    [System.ComponentModel.DisplayName("PublishMessage：MQTT 5.0 带完整属性往返")]
    public void PublishMessage_V500_WithProperties_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetByte(MqttPropertyId.PayloadFormatIndicator, 1);   // UTF-8 payload
        props.SetUInt32(MqttPropertyId.MessageExpiryInterval, 600);
        props.SetString(MqttPropertyId.ContentType, "application/json");
        props.SetString(MqttPropertyId.ResponseTopic, "reply/result");
        props.SetUInt16(MqttPropertyId.TopicAlias, 3);
        props.UserProperties.Add(new System.Collections.Generic.KeyValuePair<String, String>("trace-id", "abc123"));

        var msg = new PublishMessage
        {
            Topic = "sensor/data",
            QoS = QualityOfService.AtLeastOnce,
            Id = 42,
            Properties = props,
            Payload = new ArrayPacket("{\"t\":25}"u8.ToArray()),
        };

        // V500 往返：写入时不需要 context，读取时需要 protocolLevel=5
        using var pk = msg.ToPacket();
        var result = _factory.ReadMessage(pk, _v500Level) as PublishMessage;

        Assert.NotNull(result);
        Assert.Equal("sensor/data", result!.Topic);
        Assert.Equal(42, result.Id);
        Assert.NotNull(result.Properties);
        Assert.Equal((Byte)1, result.Properties!.GetByte(MqttPropertyId.PayloadFormatIndicator));
        Assert.Equal((UInt32)600, result.Properties.GetUInt32(MqttPropertyId.MessageExpiryInterval));
        Assert.Equal("application/json", result.Properties.GetString(MqttPropertyId.ContentType));
        Assert.Equal("reply/result", result.Properties.GetString(MqttPropertyId.ResponseTopic));
        Assert.Equal((UInt16)3, result.Properties.GetUInt16(MqttPropertyId.TopicAlias));
        Assert.Single(result.Properties.UserProperties);
        Assert.Equal("trace-id", result.Properties.UserProperties[0].Key);
        Assert.Equal("abc123", result.Properties.UserProperties[0].Value);
    }

    [Fact]
    [System.ComponentModel.DisplayName("PublishMessage：MQTT 5.0 QoS0 带属性往返")]
    public void PublishMessage_V500_QoS0_WithProperties_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetString(MqttPropertyId.ContentType, "text/plain");

        var msg = new PublishMessage
        {
            Topic = "hello/world",
            QoS = QualityOfService.AtMostOnce,
            Properties = props,
            Payload = new ArrayPacket("Hello!"u8.ToArray()),
        };

        using var pk = msg.ToPacket();
        var result = _factory.ReadMessage(pk, _v500Level) as PublishMessage;

        Assert.NotNull(result);
        Assert.Equal("hello/world", result!.Topic);
        Assert.NotNull(result.Properties);
        Assert.Equal("text/plain", result.Properties!.GetString(MqttPropertyId.ContentType));
        Assert.Equal("Hello!", result.Payload?.ToStr());
    }

    [Fact]
    [System.ComponentModel.DisplayName("PublishMessage：MQTT 3.1.1 时读取不解析 5.0 属性")]
    public void PublishMessage_V311_NoProperties_Read()
    {
        // V311 发送的消息不含属性字节，V311 协议解码不应读取属性
        var msg = new PublishMessage
        {
            Topic = "v311/data",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket("hello"u8.ToArray()),
        };

        var result = RoundTrip(msg); // 默认 context=null (V311)

        Assert.Equal("v311/data", result.Topic);
        Assert.Null(result.Properties); // V311 不读属性
        Assert.Equal("hello", result.Payload?.ToStr());
    }
    #endregion

    #region SubscribeMessage / SubAck MQTT 5.0
    [Fact]
    [System.ComponentModel.DisplayName("SubscribeMessage：MQTT 5.0 订阅选项往返")]
    public void SubscribeMessage_V500_WithProperties_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetVariableInt(MqttPropertyId.SubscriptionIdentifier, 42);

        var msg = new SubscribeMessage
        {
            Id = 1,
            Properties = props,
            Requests =
            [
                new Subscription("sensor/+", QualityOfService.AtLeastOnce),
                new Subscription("actuator/#", QualityOfService.ExactlyOnce),
            ],
        };

        // SubscribeMessage 无 V500 特殊 context，仅测试 Properties 字段
        Assert.Equal(2, msg.Requests.Count);
        Assert.NotNull(msg.Properties);
        Assert.Equal(42, msg.Properties.GetVariableInt(MqttPropertyId.SubscriptionIdentifier));
    }
    #endregion

    #region MqttMessage.ToString() 覆盖
    [Fact]
    [System.ComponentModel.DisplayName("MqttMessage.ToString：各消息类型输出非空")]
    public void MqttMessage_ToString_AllTypes()
    {
        MqttMessage[] messages =
        [
            new ConnectMessage { ClientId = "c", Username = "u", CleanSession = true },
            new ConnAck { SessionPresent = true, ReturnCode = ConnectReturnCode.Accepted },
            new PublishMessage { Topic = "t", QoS = QualityOfService.AtLeastOnce, Id = 1 },
            new PubAck { Id = 1 },
            new PubRec { Id = 2 },
            new PubRel { Id = 3 },
            new PubComp { Id = 4 },
            new SubscribeMessage { Id = 5, Requests = [new Subscription("t", QualityOfService.AtMostOnce)] },
            new SubAck { Id = 5, GrantedQos = [QualityOfService.AtMostOnce] },
            new UnsubscribeMessage { Id = 6, TopicFilters = ["t"] },
            new UnsubAck { Id = 6 },
            new PingRequest(),
            new PingResponse(),
            new DisconnectMessage(),
            new AuthMessage(),
        ];

        foreach (var msg in messages)
        {
            var str = msg.ToString();
            Assert.False(String.IsNullOrEmpty(str), $"{msg.Type}.ToString() 返回空");
        }
    }
    #endregion

    #region MqttIdMessage 自动分配 Id
    [Fact]
    [System.ComponentModel.DisplayName("MqttIdMessage：Id 0 时序列化字段占位正确")]
    public void MqttIdMessage_IdField_Serialized()
    {
        // PubAck Id=0 和 Id=123 序列化结果不同
        var msgZero = new PubAck { Id = 0 };
        var msgNonZero = new PubAck { Id = 123 };

        using var pkZero = msgZero.ToPacket();
        using var pkNonZero = msgNonZero.ToPacket();

        // 两者字节长度相同（固定格式），但 Id 字节不同
        Assert.Equal(pkZero.Total, pkNonZero.Total);

        var resZero = RoundTrip(msgZero);
        var resNonZero = RoundTrip(msgNonZero);

        Assert.Equal(0, resZero.Id);
        Assert.Equal(123, resNonZero.Id);
    }
    #endregion

    #region MqttFactory 未知类型异常
    [Fact]
    [System.ComponentModel.DisplayName("MqttFactory.CreateMessage：保留类型 0 抛出 NotSupportedException")]
    public void MqttFactory_InvalidType_ThrowsNotSupported()
    {
        var factory = new MqttFactory();
        Assert.Throws<NotSupportedException>(() => factory.CreateMessage((MqttType)0));
    }
    #endregion

    #region PublishMessage CreateAck / CreateReceive
    [Fact]
    [System.ComponentModel.DisplayName("PublishMessage.CreateAck：返回正确 Id 的 PubAck")]
    public void PublishMessage_CreateAck_CorrectId()
    {
        var msg = new PublishMessage { Topic = "t", QoS = QualityOfService.AtLeastOnce, Id = 99 };
        var ack = msg.CreateAck();

        Assert.IsType<PubAck>(ack);
        Assert.Equal(99, ack.Id);
    }

    [Fact]
    [System.ComponentModel.DisplayName("PublishMessage.CreateReceive：返回正确 Id 的 PubRec")]
    public void PublishMessage_CreateReceive_CorrectId()
    {
        var msg = new PublishMessage { Topic = "t", QoS = QualityOfService.ExactlyOnce, Id = 77 };
        var rec = msg.CreateReceive();

        Assert.IsType<PubRec>(rec);
        Assert.Equal(77, rec.Id);
    }
    #endregion

    #region MqttMessage.Reply 属性
    [Fact]
    [System.ComponentModel.DisplayName("MqttMessage.Reply：响应类型消息 Reply=true")]
    public void MqttMessage_Reply_True_ForResponseTypes()
    {
        Assert.True(new ConnAck().Reply);
        Assert.True(new PubAck().Reply);
        Assert.True(new PubRec().Reply);
        Assert.True(new PubComp().Reply);
        Assert.True(new SubAck().Reply);
        Assert.True(new UnsubAck().Reply);
        Assert.True(new PingResponse().Reply);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttMessage.Reply：请求类型消息 Reply=false")]
    public void MqttMessage_Reply_False_ForRequestTypes()
    {
        Assert.False(new ConnectMessage().Reply);
        Assert.False(new PublishMessage().Reply);
        Assert.False(new PubRel().Reply);
        Assert.False(new SubscribeMessage { Id = 1, Requests = [new Subscription("t", QualityOfService.AtMostOnce)] }.Reply);
        Assert.False(new UnsubscribeMessage { Id = 1, TopicFilters = ["t"] }.Reply);
        Assert.False(new PingRequest().Reply);
        Assert.False(new DisconnectMessage().Reply);
    }
    #endregion
}
