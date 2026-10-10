#nullable enable
using System;
using System.Collections.Generic;
using NewLife.Data;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>帧体多段链解码测试。v12 消息泵零拷贝切帧：帧体跨多次管道追加时由多段共享切片组成（<see cref="IPacket.Next"/> 非空），
/// 此时 SpanReader 退化为“流式分块缓冲”，其 Available 只反映已缓冲分块——
/// 按它取负载会静默截断（大载荷发布丢尾部）、按它判断“是否读完”会提前结束（订阅列表少项）、按它校验长度会误报数据不足。
/// 解码入口须先把链式帧体聚合为连续包。</summary>
public class FrameBodyChainTests
{
    private static readonly MqttFactory _factory = new();
    private static readonly MqttCodec _codec = new();

    /// <summary>把整帧按固定头 + 变长长度切出帧体，并把帧体切成两段链包</summary>
    /// <param name="frame">整帧字节</param>
    /// <param name="splitAt">帧体内第一段长度</param>
    /// <returns>固定头首字节与链式帧体</returns>
    private static (Byte Flag, IPacket Body) SplitBody(Byte[] frame, Int32 splitAt)
    {
        var pos = 1;
        while ((frame[pos++] & 0x80) != 0) { }

        var flag = frame[0];
        var bodyLength = frame.Length - pos;
        Assert.InRange(splitAt, 0, bodyLength);

        var first = new ArrayPacket(frame, pos, splitAt);
        var second = new ArrayPacket(frame, pos + splitAt, bodyLength - splitAt);

        return (flag, first.Append(second));
    }

    /// <summary>构建 MQTT 整帧字节</summary>
    private static Byte[] BuildFrame(MqttMessage message, MqttType type, MqttVersion version = MqttVersion.V311)
    {
        var frame = new MqttFrame
        {
            Mqtt = message,
            Version = version,
            FirstByte = (Byte)((Byte)type << 4),
        };

        using var pk = _codec.Build(frame);
        Assert.NotNull(pk);

        return pk!.ToArray();
    }

    [Fact(DisplayName = "解码_发布帧体为多段链_负载完整不截断")]
    public void Publish_ChainedBody_PayloadIntact()
    {
        var payload = new Byte[8192];
        for (var i = 0; i < payload.Length; i++) payload[i] = (Byte)(i * 7 + 3);

        var publish = new PublishMessage
        {
            Topic = "chain/topic",
            Payload = new ArrayPacket(payload),
            QoS = QualityOfService.AtMostOnce,
        };
        var (flag, body) = SplitBody(BuildFrame(publish, MqttType.Publish), 4096);

        var msg = _factory.ReadMessage(flag, body, MqttVersion.V311) as PublishMessage;

        Assert.NotNull(msg);
        Assert.Equal("chain/topic", msg!.Topic);
        Assert.Equal(payload.Length, msg.Payload!.Total);
        Assert.Equal(payload, msg.Payload.ToArray());
    }

    [Fact(DisplayName = "解码_整帧为多段链_负载完整不截断")]
    public void ReadMessage_ChainedPacket_PayloadIntact()
    {
        var payload = new Byte[8192];
        for (var i = 0; i < payload.Length; i++) payload[i] = (Byte)(i * 11 + 5);

        var publish = new PublishMessage
        {
            Topic = "chain/whole",
            Payload = new ArrayPacket(payload),
            QoS = QualityOfService.AtLeastOnce,
            Id = 1234,
        };
        var bytes = BuildFrame(publish, MqttType.Publish);

        // 整帧切成两段链包（等价于原始字节分两次进入解码器）
        var split = bytes.Length / 2;
        var chain = new ArrayPacket(bytes, 0, split).Append(new ArrayPacket(bytes, split, bytes.Length - split));

        var msg = _factory.ReadMessage(chain, MqttVersion.V311) as PublishMessage;

        Assert.NotNull(msg);
        Assert.Equal("chain/whole", msg!.Topic);
        Assert.Equal(1234, msg.Id);
        Assert.Equal(payload.Length, msg.Payload!.Total);
        Assert.Equal(payload, msg.Payload.ToArray());
    }

    [Fact(DisplayName = "解码_订阅帧体为多段链_主题列表完整")]
    public void Subscribe_ChainedBody_AllTopicsParsed()
    {
        var topics = new List<Subscription>();
        for (var i = 0; i < 600; i++)
        {
            topics.Add(new Subscription($"chain/topic/{i:D4}", QualityOfService.AtLeastOnce));
        }

        var subscribe = new SubscribeMessage { Id = 7, Requests = topics };
        var frame = BuildFrame(subscribe, MqttType.Subscribe);
        Assert.True(frame.Length > 8192, "帧体需超过单次缓冲分块（8KB）才能暴露链式读取截断");

        var (flag, body) = SplitBody(frame, 3000);
        var msg = _factory.ReadMessage(flag, body, MqttVersion.V311) as SubscribeMessage;

        Assert.NotNull(msg);
        Assert.Equal(7, msg!.Id);
        Assert.Equal(topics.Count, msg.Requests.Count);
        Assert.Equal(topics[0].TopicFilter, msg.Requests[0].TopicFilter);
        Assert.Equal(topics[topics.Count - 1].TopicFilter, msg.Requests[topics.Count - 1].TopicFilter);
    }
}
