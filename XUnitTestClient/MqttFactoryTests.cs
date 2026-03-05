using System;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttFactory 消息工厂单元测试</summary>
public class MqttFactoryTests
{
    [Theory]
    [InlineData(MqttType.Connect, typeof(ConnectMessage))]
    [InlineData(MqttType.ConnAck, typeof(ConnAck))]
    [InlineData(MqttType.Publish, typeof(PublishMessage))]
    [InlineData(MqttType.PubAck, typeof(PubAck))]
    [InlineData(MqttType.PubRec, typeof(PubRec))]
    [InlineData(MqttType.PubRel, typeof(PubRel))]
    [InlineData(MqttType.PubComp, typeof(PubComp))]
    [InlineData(MqttType.Subscribe, typeof(SubscribeMessage))]
    [InlineData(MqttType.SubAck, typeof(SubAck))]
    [InlineData(MqttType.UnSubscribe, typeof(UnsubscribeMessage))]
    [InlineData(MqttType.UnSubAck, typeof(UnsubAck))]
    [InlineData(MqttType.PingReq, typeof(PingRequest))]
    [InlineData(MqttType.PingResp, typeof(PingResponse))]
    [InlineData(MqttType.Disconnect, typeof(DisconnectMessage))]
    [InlineData(MqttType.Auth, typeof(AuthMessage))]
    public void CreateMessage_ReturnsCorrectType(MqttType type, Type expectedType)
    {
        var factory = new MqttFactory();

        var msg = factory.CreateMessage(type);

        Assert.NotNull(msg);
        Assert.IsType(expectedType, msg);
    }

    [Fact]
    public void CreateMessage_UnsupportedType_Throws()
    {
        var factory = new MqttFactory();

        Assert.Throws<NotSupportedException>(() => factory.CreateMessage((MqttType)0));
    }

    [Fact]
    public void ReadMessage_PingRequest()
    {
        var factory = new MqttFactory();

        // PingRequest 报文：0xC0 0x00
        var data = new Byte[] { 0xC0, 0x00 };
        var pk = new ArrayPacket(data);

        var msg = factory.ReadMessage(pk);

        Assert.NotNull(msg);
        Assert.IsType<PingRequest>(msg);
    }

    [Fact]
    public void ReadMessage_PingResponse()
    {
        var factory = new MqttFactory();

        // PingResponse 报文：0xD0 0x00
        var data = new Byte[] { 0xD0, 0x00 };
        var pk = new ArrayPacket(data);

        var msg = factory.ReadMessage(pk);

        Assert.NotNull(msg);
        Assert.IsType<PingResponse>(msg);
    }

    [Fact]
    public void ReadMessage_WithProtocolLevel5()
    {
        var factory = new MqttFactory();

        // PingRequest 报文
        var data = new Byte[] { 0xC0, 0x00 };
        var pk = new ArrayPacket(data);

        var msg = factory.ReadMessage(pk, 5);

        Assert.NotNull(msg);
        Assert.IsType<PingRequest>(msg);
    }

    [Fact]
    public void ReadMessage_ConnAck()
    {
        var factory = new MqttFactory();

        // ConnAck 报文：0x20 0x02 0x00 0x00 (无会话存在，返回码 Accepted)
        var data = new Byte[] { 0x20, 0x02, 0x00, 0x00 };
        var pk = new ArrayPacket(data);

        var msg = factory.ReadMessage(pk);

        Assert.NotNull(msg);
        Assert.IsType<ConnAck>(msg);
        var connAck = (ConnAck)msg;
        Assert.Equal(ConnectReturnCode.Accepted, connAck.ReturnCode);
    }
}
