#nullable enable

using System;
using System.Buffers;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MQTT 帧编解码器测试。覆盖 v12 消息泵定界语义与请求响应配对</summary>
public class MqttCodecTests
{
    [Fact]
    [System.ComponentModel.DisplayName("帧定界：PINGREQ 固定头解析")]
    public void TryParse_PingRequest_DelimitsFrame()
    {
        var codec = new MqttCodec();
        var bytes = new PingRequest().ToPacket().ToArray();

        var rs = codec.TryParse(new ReadOnlySequence<Byte>(bytes));

        Assert.NotNull(rs);
        var result = rs!.Value;
        Assert.False(result.Invalid);
        Assert.Equal(2, result.HeaderSize);
        Assert.Equal(0, result.BodyLength);

        var frame = Assert.IsType<MqttFrame>(result.Message);
        Assert.Equal(MqttType.PingReq, frame.Type);
        Assert.Equal(MessageKinds.Request, frame.Kind);
    }

    [Fact]
    [System.ComponentModel.DisplayName("帧定界：头部不足返回null等待追加")]
    public void TryParse_PartialHeader_ReturnsNull()
    {
        var codec = new MqttCodec();

        Assert.Null(codec.TryParse(new ReadOnlySequence<Byte>([0xC0])));
    }

    [Fact]
    [System.ComponentModel.DisplayName("帧定界：变长长度超4字节判为损坏")]
    public void TryParse_InvalidVarint_ReturnsInvalid()
    {
        var codec = new MqttCodec();
        var bytes = new Byte[] { 0x30, 0xFF, 0xFF, 0xFF, 0xFF };

        var rs = codec.TryParse(new ReadOnlySequence<Byte>(bytes));

        Assert.NotNull(rs);
        Assert.True(rs!.Value.Invalid);
    }

    [Fact]
    [System.ComponentModel.DisplayName("帧解码：固定头与帧体分离后还原 PINGREQ")]
    public void ReadMessage_FlagAndBody_RoundTrips()
    {
        var factory = new MqttFactory();
        var bytes = new PingRequest().ToPacket().ToArray();
        var body = new ArrayPacket(bytes, 2, bytes.Length - 2);

        var msg = factory.ReadMessage(bytes[0], body, MqttVersion.V311);

        Assert.IsType<PingRequest>(msg);
    }

    [Fact]
    [System.ComponentModel.DisplayName("配对：PINGREQ 与 PINGRESP 匹配")]
    public void Match_PingRequest_PingResponse()
    {
        var codec = new MqttCodec();
        var request = ParseFrame(codec, new PingRequest().ToPacket().ToArray());
        var response = ParseFrame(codec, new PingResponse().ToPacket().ToArray());

        Assert.True(codec.Match(request, response));
    }

    [Fact]
    [System.ComponentModel.DisplayName("配对：响应与请求方向不符不匹配")]
    public void Match_SameDirection_ReturnsFalse()
    {
        var codec = new MqttCodec();
        var request = ParseFrame(codec, new PingRequest().ToPacket().ToArray());
        var another = ParseFrame(codec, new PingRequest().ToPacket().ToArray());

        Assert.False(codec.Match(request, another));
    }

    [Fact]
    [System.ComponentModel.DisplayName("配对：发布消息与 PUBACK 按标识符匹配")]
    public void Match_Publish_PubAck_ById()
    {
        var codec = new MqttCodec();
        var request = CreateFrame(new PublishMessage { Topic = "a/b", QoS = QualityOfService.AtLeastOnce, Id = 7 });
        var ack = CreateFrame(new PubAck { Id = 7 });
        var other = CreateFrame(new PubAck { Id = 8 });

        Assert.True(codec.Match(request, ack));
        Assert.False(codec.Match(request, other));
    }

    private static MqttFrame ParseFrame(MqttCodec codec, Byte[] bytes)
    {
        var rs = codec.TryParse(new ReadOnlySequence<Byte>(bytes));
        Assert.NotNull(rs);

        return Assert.IsType<MqttFrame>(rs!.Value.Message);
    }

    private static MqttFrame CreateFrame(MqttMessage msg)
    {
        var flag = (Byte)((Byte)msg.Type << 4);

        return new MqttFrame
        {
            Mqtt = msg,
            Version = MqttVersion.V311,
            FirstByte = flag,
            Kind = MqttFrame.GetKind(flag),
        };
    }
}
