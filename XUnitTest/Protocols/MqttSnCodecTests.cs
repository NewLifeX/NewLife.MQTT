using System;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.MqttSn;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>MQTT-SN codec unit tests</summary>
public class MqttSnCodecTests
{
    #region Codec
    [System.ComponentModel.DisplayName("MqttSnCodec: round-trip encode then decode")]
    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        var original = new MqttSnConnAckMessage { ReturnCode = MqttSnReturnCode.Accepted };
        var data = original.Encode();
        var msg = MqttSnCodec.Decode(data, 0, data.Length);

        Assert.NotNull(msg);
        Assert.IsType<MqttSnConnAckMessage>(msg);
        Assert.Equal(MqttSnReturnCode.Accepted, ((MqttSnConnAckMessage)msg).ReturnCode);
    }

    [System.ComponentModel.DisplayName("MqttSnCodec: short buffer returns null")]
    [Fact]
    public void Decode_ShortBuffer_ReturnsNull()
    {
        var msg = MqttSnCodec.Decode(new Byte[1], 0, 1);
        Assert.Null(msg);
    }

    [System.ComponentModel.DisplayName("MqttSnCodec: unknown type returns null")]
    [Fact]
    public void Decode_UnknownType_ReturnsNull()
    {
        var data = new Byte[] { 2, 0xFF };
        var msg = MqttSnCodec.Decode(data, 0, 2);
        Assert.Null(msg);
    }

    [System.ComponentModel.DisplayName("MqttSnCodec: 3-byte length format")]
    [Fact]
    public void Decode_ThreeByteLength()
    {
        var data = new Byte[] { 0x01, 0x00, 0x05, (Byte)MqttSnMessageType.ConnAck, 0x00 };
        var msg = MqttSnCodec.Decode(data, 0, 5);

        Assert.NotNull(msg);
        Assert.IsType<MqttSnConnAckMessage>(msg);
    }
    #endregion

    #region Message Types
    [System.ComponentModel.DisplayName("Advertise: encode-decode round trip")]
    [Fact]
    public void Advertise_RoundTrip()
    {
        var original = new AdvertiseMessage { GatewayId = 5, Duration = 300 };
        var data = original.Encode();
        var msg = (AdvertiseMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(5, msg.GatewayId);
        Assert.Equal(300, msg.Duration);
    }

    [System.ComponentModel.DisplayName("SearchGw: encode-decode round trip")]
    [Fact]
    public void SearchGw_RoundTrip()
    {
        var original = new SearchGwMessage { Radius = 2 };
        var data = original.Encode();
        var msg = (SearchGwMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(2, msg.Radius);
    }

    [System.ComponentModel.DisplayName("Connect: encode-decode round trip")]
    [Fact]
    public void Connect_RoundTrip()
    {
        var original = new MqttSnConnectMessage
        {
            Flags = 0x02,
            Duration = 600,
            ClientId = "sensor001",
        };
        var data = original.Encode();
        var msg = (MqttSnConnectMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.True(msg.CleanSession);
        Assert.Equal(600, msg.Duration);
        Assert.Equal("sensor001", msg.ClientId);
    }

    [System.ComponentModel.DisplayName("ConnAck: accepted return code")]
    [Fact]
    public void ConnAck_Accepted()
    {
        var data = new Byte[] { 3, (Byte)MqttSnMessageType.ConnAck, 0x00 };
        var msg = (MqttSnConnAckMessage)MqttSnCodec.Decode(data, 0, 3);

        Assert.Equal(MqttSnReturnCode.Accepted, msg.ReturnCode);
    }

    [System.ComponentModel.DisplayName("ConnAck: rejected")]
    [Fact]
    public void ConnAck_Rejected()
    {
        var data = new Byte[] { 3, (Byte)MqttSnMessageType.ConnAck, 0x01 };
        var msg = (MqttSnConnAckMessage)MqttSnCodec.Decode(data, 0, 3);

        Assert.Equal(MqttSnReturnCode.RejectedCongestion, msg.ReturnCode);
    }

    [System.ComponentModel.DisplayName("Register: encode-decode round trip")]
    [Fact]
    public void Register_RoundTrip()
    {
        var original = new RegisterMessage { TopicId = 0, MessageId = 10, TopicName = "sensor/temp" };
        var data = original.Encode();
        var msg = (RegisterMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(0, msg.TopicId);
        Assert.Equal(10, msg.MessageId);
        Assert.Equal("sensor/temp", msg.TopicName);
    }

    [System.ComponentModel.DisplayName("RegAck: encode-decode round trip")]
    [Fact]
    public void RegAck_RoundTrip()
    {
        var data = new Byte[] { 7, (Byte)MqttSnMessageType.RegAck, 0x00, 0x05, 0x00, 0x0A, 0x00 };
        var msg = (RegAckMessage)MqttSnCodec.Decode(data, 0, 7);

        Assert.Equal(5, msg.TopicId);
        Assert.Equal(10, msg.MessageId);
        Assert.Equal(MqttSnReturnCode.Accepted, msg.ReturnCode);
    }

    [System.ComponentModel.DisplayName("Publish: QoS 0 normal topic ID")]
    [Fact]
    public void Publish_Qos0_NormalTopic()
    {
        var original = new MqttSnPublishMessage
        {
            Flags = 0x00,
            TopicId = 100,
            Data = new Byte[] { 0x01, 0x02, 0x03 },
        };
        var data = original.Encode();
        var msg = (MqttSnPublishMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(QualityOfService.AtMostOnce, msg.QoS);
        Assert.Equal(100, msg.TopicId);
        Assert.Equal(3, msg.Data.Length);
    }

    [System.ComponentModel.DisplayName("Publish: QoS 1 with message ID")]
    [Fact]
    public void Publish_Qos1_WithMessageId()
    {
        var original = new MqttSnPublishMessage
        {
            Flags = (Byte)((1 << 5) | 0x00),
            TopicId = 200,
            MessageId = 42,
            Data = new Byte[] { 0xFF },
        };
        var data = original.Encode();
        var msg = (MqttSnPublishMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(QualityOfService.AtLeastOnce, msg.QoS);
        Assert.Equal(200, msg.TopicId);
        Assert.Equal(42, msg.MessageId);
        Assert.Single(msg.Data);
    }

    [System.ComponentModel.DisplayName("Publish: Dup and Retain flags")]
    [Fact]
    public void Publish_DupRetainFlags()
    {
        var original = new MqttSnPublishMessage
        {
            Flags = 0x90,
            TopicId = 1,
            Data = [],
        };
        var data = original.Encode();
        var msg = (MqttSnPublishMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.True(msg.Dup);
        Assert.True(msg.Retain);
        Assert.Equal(QualityOfService.AtMostOnce, msg.QoS);
    }

    [System.ComponentModel.DisplayName("PubAck: encode-decode round trip")]
    [Fact]
    public void PubAck_RoundTrip()
    {
        var data = new Byte[] { 7, (Byte)MqttSnMessageType.PubAck, 0x00, 0x64, 0x00, 0x2A, 0x00 };
        var msg = (MqttSnPubAckMessage)MqttSnCodec.Decode(data, 0, 7);

        Assert.Equal(100, msg.TopicId);
        Assert.Equal(42, msg.MessageId);
        Assert.Equal(MqttSnReturnCode.Accepted, msg.ReturnCode);
    }

    [System.ComponentModel.DisplayName("Subscribe: encode-decode round trip")]
    [Fact]
    public void Subscribe_RoundTrip()
    {
        var original = new MqttSnSubscribeMessage
        {
            Flags = 0x00,
            MessageId = 5,
            TopicName = "device/status",
        };
        var data = original.Encode();
        var msg = (MqttSnSubscribeMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal(5, msg.MessageId);
        Assert.Equal("device/status", msg.TopicName);
    }

    [System.ComponentModel.DisplayName("SubAck: encode-decode round trip")]
    [Fact]
    public void SubAck_RoundTrip()
    {
        var data = new Byte[] { 8, (Byte)MqttSnMessageType.SubAck, 0x00, 0x00, 0x0A, 0x00, 0x05, 0x00 };
        var msg = (MqttSnSubAckMessage)MqttSnCodec.Decode(data, 0, 8);

        Assert.Equal(10, msg.TopicId);
        Assert.Equal(5, msg.MessageId);
        Assert.Equal(MqttSnReturnCode.Accepted, msg.ReturnCode);
    }

    [System.ComponentModel.DisplayName("Unsubscribe: encode-decode round trip")]
    [Fact]
    public void Unsubscribe_RoundTrip()
    {
        var data = new Byte[] { 7, (Byte)MqttSnMessageType.Unsubscribe, 0x00, 0x00, 0x07, 0x00, 0x14 };
        var msg = (MqttSnUnsubscribeMessage)MqttSnCodec.Decode(data, 0, 7);

        Assert.Equal(7, msg.MessageId);
        Assert.Equal(20, msg.TopicId);
    }

    [System.ComponentModel.DisplayName("PingReq/PingResp: encode-decode round trip")]
    [Fact]
    public void PingReqPingResp_RoundTrip()
    {
        var pingReq = new MqttSnPingReqMessage { ClientId = "dev1" };
        var data = pingReq.Encode();
        var msg = (MqttSnPingReqMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Equal("dev1", msg.ClientId);

        var pingResp = new MqttSnPingRespMessage();
        data = pingResp.Encode();
        var msg2 = MqttSnCodec.Decode(data, 0, data.Length);
        Assert.IsType<MqttSnPingRespMessage>(msg2);
    }

    [System.ComponentModel.DisplayName("Disconnect: normal and sleep mode")]
    [Fact]
    public void Disconnect_NormalAndSleep()
    {
        var normal = new MqttSnDisconnectMessage();
        var data = normal.Encode();
        var msg = (MqttSnDisconnectMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Null(msg.Duration);

        var sleep = new MqttSnDisconnectMessage { Duration = 300 };
        data = sleep.Encode();
        msg = (MqttSnDisconnectMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Equal((UInt16)300, msg.Duration);
    }

    [System.ComponentModel.DisplayName("WillTopic: encode-decode round trip")]
    [Fact]
    public void WillTopic_RoundTrip()
    {
        var original = new WillTopicMessage { Flags = 0x20, TopicName = "device/alarm" };
        var data = original.Encode();
        var msg = (WillTopicMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Equal("device/alarm", msg.TopicName);
        Assert.Equal(QualityOfService.AtLeastOnce, msg.QoS);
    }

    [System.ComponentModel.DisplayName("WillMsg: encode-decode round trip")]
    [Fact]
    public void WillMsg_RoundTrip()
    {
        var original = new WillMsgMessage { Data = new Byte[] { 0x01 } };
        var data = original.Encode();
        var msg = (WillMsgMessage)MqttSnCodec.Decode(data, 0, data.Length);

        Assert.Single(msg.Data);
        Assert.Equal(0x01, msg.Data[0]);
    }

    [System.ComponentModel.DisplayName("PubRec/PubRel/PubComp: QoS 2 flow")]
    [Fact]
    public void Qos2Flow_RoundTrip()
    {
        var pubRec = new PubRecMessage { MessageId = 99 };
        var data = pubRec.Encode();
        var msg = (PubRecMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Equal(99, msg.MessageId);

        var pubRel = new PubRelMessage { MessageId = 99 };
        data = pubRel.Encode();
        var msg2 = (PubRelMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Equal(99, msg2.MessageId);

        var pubComp = new PubCompMessage { MessageId = 99 };
        data = pubComp.Encode();
        var msg3 = (PubCompMessage)MqttSnCodec.Decode(data, 0, data.Length);
        Assert.Equal(99, msg3.MessageId);
    }

    [System.ComponentModel.DisplayName("Topic registry: register and lookup")]
    [Fact]
    public void TopicRegistry_RegisterAndLookup()
    {
        var registry = new MqttSnTopicRegistry();
        var id = registry.Register("sensor/temp");

        Assert.True(id >= 2);
        Assert.Equal("sensor/temp", registry.Lookup(id));
        Assert.Equal(id, registry.GetId("sensor/temp"));
    }

    [System.ComponentModel.DisplayName("Topic registry: same topic returns same ID")]
    [Fact]
    public void TopicRegistry_SameTopicSameId()
    {
        var registry = new MqttSnTopicRegistry();
        var id1 = registry.Register("sensor/temp");
        var id2 = registry.Register("sensor/temp");

        Assert.Equal(id1, id2);
    }

    [System.ComponentModel.DisplayName("Topic registry: remove and clear")]
    [Fact]
    public void TopicRegistry_RemoveAndClear()
    {
        var registry = new MqttSnTopicRegistry();
        var id = registry.Register("sensor/temp");
        registry.Remove(id);

        Assert.Null(registry.Lookup(id));
        Assert.Equal(0, registry.GetId("sensor/temp"));
    }
    #endregion
}
