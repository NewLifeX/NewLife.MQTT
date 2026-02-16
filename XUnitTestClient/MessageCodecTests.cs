using System;
using System.Collections.Generic;
using System.IO;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>报文编解码单元测试。覆盖所有15种报文类型的序列化/反序列化往返</summary>
public class MessageCodecTests
{
    private static readonly MqttFactory _factory = new();

    /// <summary>辅助方法：序列化再反序列化</summary>
    private static T RoundTrip<T>(T msg) where T : MqttMessage
    {
        var buf = msg.ToArray();
        Assert.True(buf.Length > 0);

        var pk = new ArrayPacket(buf);
        var result = _factory.ReadMessage(pk);
        Assert.NotNull(result);
        Assert.IsType<T>(result);

        return (T)result;
    }

    #region CONNECT / CONNACK
    [Fact]
    public void ConnectMessage_RoundTrip_Basic()
    {
        var msg = new ConnectMessage
        {
            ClientId = "test_client_1",
            Username = "admin",
            Password = "pass123",
            CleanSession = true,
            KeepAliveInSeconds = 60,
            ProtocolLevel = 0x04,
        };

        var result = RoundTrip(msg);

        Assert.Equal("test_client_1", result.ClientId);
        Assert.Equal("admin", result.Username);
        Assert.Equal("pass123", result.Password);
        Assert.True(result.CleanSession);
        Assert.Equal(60, result.KeepAliveInSeconds);
        Assert.Equal(0x04, result.ProtocolLevel);
        Assert.Equal(MqttType.Connect, result.Type);
    }

    [Fact]
    public void ConnectMessage_RoundTrip_WithWill()
    {
        var msg = new ConnectMessage
        {
            ClientId = "will_client",
            HasWill = true,
            WillTopicName = "last/will/topic",
            WillMessage = "offline"u8.ToArray(),
            WillQualityOfService = QualityOfService.AtLeastOnce,
            WillRetain = true,
            KeepAliveInSeconds = 120,
        };

        var result = RoundTrip(msg);

        Assert.True(result.HasWill);
        Assert.Equal("last/will/topic", result.WillTopicName);
        Assert.Equal("offline", System.Text.Encoding.UTF8.GetString(result.WillMessage!));
        Assert.Equal(QualityOfService.AtLeastOnce, result.WillQualityOfService);
        Assert.True(result.WillRetain);
    }

    [Fact]
    public void ConnectMessage_RoundTrip_V500()
    {
        var msg = new ConnectMessage
        {
            ClientId = "v5_client",
            ProtocolLevel = 0x05,
            CleanSession = true,
            KeepAliveInSeconds = 30,
        };

        // MQTT 5.0 需要写入空属性长度
        var result = RoundTrip(msg);

        Assert.Equal("v5_client", result.ClientId);
        Assert.Equal(0x05, result.ProtocolLevel);
    }

    [Fact]
    public void ConnAck_RoundTrip()
    {
        var msg = new ConnAck
        {
            SessionPresent = true,
            ReturnCode = ConnectReturnCode.Accepted,
        };

        var result = RoundTrip(msg);

        Assert.True(result.SessionPresent);
        Assert.Equal(ConnectReturnCode.Accepted, result.ReturnCode);
        Assert.Equal(MqttType.ConnAck, result.Type);
    }

    [Fact]
    public void ConnAck_RoundTrip_Refused()
    {
        var msg = new ConnAck
        {
            SessionPresent = false,
            ReturnCode = ConnectReturnCode.RefusedBadUsernameOrPassword,
        };

        var result = RoundTrip(msg);

        Assert.False(result.SessionPresent);
        Assert.Equal(ConnectReturnCode.RefusedBadUsernameOrPassword, result.ReturnCode);
    }
    #endregion

    #region PUBLISH
    [Fact]
    public void PublishMessage_RoundTrip_QoS0()
    {
        var payload = "Hello MQTT"u8.ToArray();
        var msg = new PublishMessage
        {
            Topic = "test/topic",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(payload),
        };

        var result = RoundTrip(msg);

        Assert.Equal("test/topic", result.Topic);
        Assert.Equal(QualityOfService.AtMostOnce, result.QoS);
        Assert.Equal("Hello MQTT", result.Payload?.ToStr());
        Assert.Equal(MqttType.Publish, result.Type);
    }

    [Fact]
    public void PublishMessage_RoundTrip_QoS1()
    {
        var msg = new PublishMessage
        {
            Topic = "qos1/topic",
            QoS = QualityOfService.AtLeastOnce,
            Id = 42,
            Payload = new ArrayPacket("test data"u8.ToArray()),
        };

        var result = RoundTrip(msg);

        Assert.Equal("qos1/topic", result.Topic);
        Assert.Equal(QualityOfService.AtLeastOnce, result.QoS);
        Assert.Equal(42, result.Id);
        Assert.Equal("test data", result.Payload?.ToStr());
    }

    [Fact]
    public void PublishMessage_RoundTrip_QoS2()
    {
        var msg = new PublishMessage
        {
            Topic = "qos2/topic",
            QoS = QualityOfService.ExactlyOnce,
            Id = 100,
            Payload = new ArrayPacket("exactly once"u8.ToArray()),
        };

        var result = RoundTrip(msg);

        Assert.Equal(QualityOfService.ExactlyOnce, result.QoS);
        Assert.Equal(100, result.Id);
    }

    [Fact]
    public void PublishMessage_RoundTrip_RetainFlag()
    {
        var msg = new PublishMessage
        {
            Topic = "retain/topic",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = new ArrayPacket("retained msg"u8.ToArray()),
        };

        var result = RoundTrip(msg);

        Assert.True(result.Retain);
        Assert.Equal("retained msg", result.Payload?.ToStr());
    }

    [Fact]
    public void PublishMessage_RoundTrip_DuplicateFlag()
    {
        var msg = new PublishMessage
        {
            Topic = "dup/topic",
            QoS = QualityOfService.AtLeastOnce,
            Id = 7,
            Duplicate = true,
            Payload = new ArrayPacket("dup msg"u8.ToArray()),
        };

        var result = RoundTrip(msg);

        Assert.True(result.Duplicate);
    }

    [Fact]
    public void PublishMessage_RoundTrip_EmptyPayload()
    {
        var msg = new PublishMessage
        {
            Topic = "empty/payload",
            QoS = QualityOfService.AtMostOnce,
        };

        var result = RoundTrip(msg);

        Assert.Equal("empty/payload", result.Topic);
        Assert.True(result.Payload == null || result.Payload.Total == 0);
    }
    #endregion

    #region PUBACK / PUBREC / PUBREL / PUBCOMP
    [Fact]
    public void PubAck_RoundTrip()
    {
        var msg = new PubAck { Id = 123 };
        var result = RoundTrip(msg);

        Assert.Equal(123, result.Id);
        Assert.Equal(MqttType.PubAck, result.Type);
    }

    [Fact]
    public void PubRec_RoundTrip()
    {
        var msg = new PubRec { Id = 456 };
        var result = RoundTrip(msg);

        Assert.Equal(456, result.Id);
        Assert.Equal(MqttType.PubRec, result.Type);
    }

    [Fact]
    public void PubRel_RoundTrip()
    {
        var msg = new PubRel { Id = 789 };
        var result = RoundTrip(msg);

        Assert.Equal(789, result.Id);
        Assert.Equal(MqttType.PubRel, result.Type);
    }

    [Fact]
    public void PubComp_RoundTrip()
    {
        var msg = new PubComp { Id = 321 };
        var result = RoundTrip(msg);

        Assert.Equal(321, result.Id);
        Assert.Equal(MqttType.PubComp, result.Type);
    }
    #endregion

    #region SUBSCRIBE / SUBACK
    [Fact]
    public void SubscribeMessage_RoundTrip_SingleTopic()
    {
        var msg = new SubscribeMessage
        {
            Id = 10,
            Requests = [new Subscription("test/topic", QualityOfService.AtLeastOnce)],
        };

        var result = RoundTrip(msg);

        Assert.Equal(10, result.Id);
        Assert.Single(result.Requests);
        Assert.Equal("test/topic", result.Requests[0].TopicFilter);
        Assert.Equal(QualityOfService.AtLeastOnce, result.Requests[0].QualityOfService);
    }

    [Fact]
    public void SubscribeMessage_RoundTrip_MultipleTopics()
    {
        var msg = new SubscribeMessage
        {
            Id = 20,
            Requests =
            [
                new Subscription("topic/a", QualityOfService.AtMostOnce),
                new Subscription("topic/b", QualityOfService.AtLeastOnce),
                new Subscription("topic/c/#", QualityOfService.ExactlyOnce),
            ],
        };

        var result = RoundTrip(msg);

        Assert.Equal(3, result.Requests.Count);
        Assert.Equal("topic/a", result.Requests[0].TopicFilter);
        Assert.Equal("topic/c/#", result.Requests[2].TopicFilter);
        Assert.Equal(QualityOfService.ExactlyOnce, result.Requests[2].QualityOfService);
    }

    [Fact]
    public void SubAck_RoundTrip()
    {
        var msg = new SubAck
        {
            Id = 10,
            GrantedQos = [QualityOfService.AtMostOnce, QualityOfService.AtLeastOnce, QualityOfService.ExactlyOnce],
        };

        var result = RoundTrip(msg);

        Assert.Equal(10, result.Id);
        Assert.Equal(3, result.GrantedQos.Count);
        Assert.Equal(QualityOfService.AtMostOnce, result.GrantedQos[0]);
        Assert.Equal(QualityOfService.AtLeastOnce, result.GrantedQos[1]);
        Assert.Equal(QualityOfService.ExactlyOnce, result.GrantedQos[2]);
    }
    #endregion

    #region UNSUBSCRIBE / UNSUBACK
    [Fact]
    public void UnsubscribeMessage_RoundTrip()
    {
        var msg = new UnsubscribeMessage
        {
            Id = 30,
            TopicFilters = ["topic/a", "topic/b/#"],
        };

        var result = RoundTrip(msg);

        Assert.Equal(30, result.Id);
        Assert.Equal(2, result.TopicFilters.Count);
        Assert.Equal("topic/a", result.TopicFilters[0]);
        Assert.Equal("topic/b/#", result.TopicFilters[1]);
    }

    [Fact]
    public void UnsubAck_RoundTrip()
    {
        var msg = new UnsubAck { Id = 30 };
        var result = RoundTrip(msg);

        Assert.Equal(30, result.Id);
        Assert.Equal(MqttType.UnSubAck, result.Type);
    }
    #endregion

    #region PINGREQ / PINGRESP
    [Fact]
    public void PingRequest_RoundTrip()
    {
        var msg = new PingRequest();
        var result = RoundTrip(msg);

        Assert.Equal(MqttType.PingReq, result.Type);
    }

    [Fact]
    public void PingResponse_RoundTrip()
    {
        var msg = new PingResponse();
        var result = RoundTrip(msg);

        Assert.Equal(MqttType.PingResp, result.Type);
    }
    #endregion

    #region DISCONNECT
    [Fact]
    public void DisconnectMessage_RoundTrip()
    {
        var msg = new DisconnectMessage();
        var result = RoundTrip(msg);

        Assert.Equal(MqttType.Disconnect, result.Type);
    }
    #endregion

    #region AUTH (MQTT 5.0)
    [Fact]
    public void AuthMessage_RoundTrip_Empty()
    {
        var msg = new AuthMessage();
        var result = RoundTrip(msg);

        Assert.Equal(MqttType.Auth, result.Type);
        Assert.Equal(0x00, result.ReasonCode);
    }

    [Fact]
    public void AuthMessage_RoundTrip_WithReasonCode()
    {
        var msg = new AuthMessage
        {
            ReasonCode = 0x18, // 继续认证
        };

        var result = RoundTrip(msg);

        Assert.Equal(0x18, result.ReasonCode);
    }
    #endregion

    #region MQTT 5.0 属性系统
    [Fact]
    public void MqttProperties_RoundTrip_ByteProperty()
    {
        var props = new MqttProperties();
        props.SetByte(MqttPropertyId.PayloadFormatIndicator, 1);

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal((Byte)1, result.GetByte(MqttPropertyId.PayloadFormatIndicator));
    }

    [Fact]
    public void MqttProperties_RoundTrip_UInt16Property()
    {
        var props = new MqttProperties();
        props.SetUInt16(MqttPropertyId.ReceiveMaximum, 1024);

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal((UInt16)1024, result.GetUInt16(MqttPropertyId.ReceiveMaximum));
    }

    [Fact]
    public void MqttProperties_RoundTrip_UInt32Property()
    {
        var props = new MqttProperties();
        props.SetUInt32(MqttPropertyId.SessionExpiryInterval, 3600);

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal((UInt32)3600, result.GetUInt32(MqttPropertyId.SessionExpiryInterval));
    }

    [Fact]
    public void MqttProperties_RoundTrip_StringProperty()
    {
        var props = new MqttProperties();
        props.SetString(MqttPropertyId.ContentType, "application/json");

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal("application/json", result.GetString(MqttPropertyId.ContentType));
    }

    [Fact]
    public void MqttProperties_RoundTrip_BinaryProperty()
    {
        var data = new Byte[] { 0x01, 0x02, 0x03, 0x04 };
        var props = new MqttProperties();
        props.SetBinary(MqttPropertyId.CorrelationData, data);

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal(data, result.GetBinary(MqttPropertyId.CorrelationData));
    }

    [Fact]
    public void MqttProperties_RoundTrip_UserProperties()
    {
        var props = new MqttProperties();
        props.UserProperties.Add(new KeyValuePair<String, String>("key1", "value1"));
        props.UserProperties.Add(new KeyValuePair<String, String>("key2", "value2"));

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal(2, result.UserProperties.Count);
        Assert.Equal("key1", result.UserProperties[0].Key);
        Assert.Equal("value1", result.UserProperties[0].Value);
        Assert.Equal("key2", result.UserProperties[1].Key);
        Assert.Equal("value2", result.UserProperties[1].Value);
    }

    [Fact]
    public void MqttProperties_RoundTrip_MultipleProperties()
    {
        var props = new MqttProperties();
        props.SetByte(MqttPropertyId.PayloadFormatIndicator, 1);
        props.SetUInt32(MqttPropertyId.MessageExpiryInterval, 600);
        props.SetString(MqttPropertyId.ResponseTopic, "reply/topic");
        props.SetUInt16(MqttPropertyId.TopicAlias, 5);
        props.UserProperties.Add(new KeyValuePair<String, String>("app", "mqtt-test"));

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal((Byte)1, result.GetByte(MqttPropertyId.PayloadFormatIndicator));
        Assert.Equal((UInt32)600, result.GetUInt32(MqttPropertyId.MessageExpiryInterval));
        Assert.Equal("reply/topic", result.GetString(MqttPropertyId.ResponseTopic));
        Assert.Equal((UInt16)5, result.GetUInt16(MqttPropertyId.TopicAlias));
        Assert.Single(result.UserProperties);
        Assert.Equal("app", result.UserProperties[0].Key);
    }

    [Fact]
    public void MqttProperties_EmptyProperties_RoundTrip()
    {
        var props = new MqttProperties();

        var ms = new MemoryStream();
        props.Write(ms);

        // 空属性只写入一个字节（长度0）
        Assert.Equal(1, ms.Length);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void MqttProperties_VariableInt_RoundTrip()
    {
        var props = new MqttProperties();
        props.SetVariableInt(MqttPropertyId.SubscriptionIdentifier, 268435455); // 最大变长整数

        var ms = new MemoryStream();
        props.Write(ms);

        ms.Position = 0;
        var result = new MqttProperties();
        result.Read(ms);

        Assert.Equal(268435455, result.GetVariableInt(MqttPropertyId.SubscriptionIdentifier));
    }
    #endregion

    #region QoS 标志位正确性
    [Theory]
    [InlineData(QualityOfService.AtMostOnce)]
    [InlineData(QualityOfService.AtLeastOnce)]
    [InlineData(QualityOfService.ExactlyOnce)]
    public void PublishMessage_QoSFlagBits(QualityOfService qos)
    {
        var msg = new PublishMessage
        {
            Topic = "test",
            QoS = qos,
            Id = qos > 0 ? (UInt16)1 : (UInt16)0,
            Payload = new ArrayPacket("data"u8.ToArray()),
        };

        var buf = msg.ToArray();

        // 检查固定头字节中的 QoS 位
        var firstByte = buf[0];
        var actualQoS = (QualityOfService)((firstByte >> 1) & 0x03);
        Assert.Equal(qos, actualQoS);
    }

    [Fact]
    public void PublishMessage_RetainFlagBit()
    {
        var msg = new PublishMessage
        {
            Topic = "test",
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
            Payload = new ArrayPacket("data"u8.ToArray()),
        };

        var buf = msg.ToArray();
        var firstByte = buf[0];

        // Retain 在最低位
        Assert.True((firstByte & 0x01) == 1);
    }

    [Fact]
    public void PublishMessage_DuplicateFlagBit()
    {
        var msg = new PublishMessage
        {
            Topic = "test",
            QoS = QualityOfService.AtLeastOnce,
            Id = 1,
            Duplicate = true,
            Payload = new ArrayPacket("data"u8.ToArray()),
        };

        var buf = msg.ToArray();
        var firstByte = buf[0];

        // DUP 在第3位
        Assert.True((firstByte & 0x08) == 0x08);
    }
    #endregion

    #region MqttFactory
    [Fact]
    public void MqttFactory_CreateMessage_AllTypes()
    {
        var types = new[]
        {
            MqttType.Connect, MqttType.ConnAck, MqttType.Publish,
            MqttType.PubAck, MqttType.PubRec, MqttType.PubRel, MqttType.PubComp,
            MqttType.Subscribe, MqttType.SubAck,
            MqttType.UnSubscribe, MqttType.UnSubAck,
            MqttType.PingReq, MqttType.PingResp,
            MqttType.Disconnect, MqttType.Auth,
        };

        foreach (var t in types)
        {
            var msg = _factory.CreateMessage(t);
            Assert.NotNull(msg);
            Assert.Equal(t, msg.Type);
        }
    }
    #endregion
}
