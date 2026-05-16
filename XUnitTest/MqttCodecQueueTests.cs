#nullable enable

using System;
using System.Collections.Generic;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Net.Handlers;
using Xunit;

namespace XUnitTestClient;

/// <summary>MQTT 编解码器匹配队列测试</summary>
public class MqttCodecQueueTests
{
    [Fact]
    public void Read_PingRequest_DoesNotMatchQueue()
    {
        var codec = new MqttCodec();
        var queue = new TestMatchQueue();
        var context = new TestHandlerContext { Owner = new TestExtendOwner() };
        codec.Queue = queue;

        using var pk = new PingRequest().ToPacket();
        codec.Read(context, pk);

        Assert.Equal(0, queue.MatchCount);
        Assert.Single(context.Messages);
        Assert.IsType<PingRequest>(context.Messages[0]);
    }

    [Fact]
    public void Read_PingResponse_MatchesQueue()
    {
        var codec = new MqttCodec();
        var queue = new TestMatchQueue();
        var context = new TestHandlerContext { Owner = new TestExtendOwner() };
        codec.Queue = queue;

        using var pk = new PingResponse().ToPacket();
        codec.Read(context, pk);

        Assert.Equal(1, queue.MatchCount);
        Assert.IsType<PingResponse>(queue.LastResponse);
        Assert.IsType<PingResponse>(queue.LastResult);
        Assert.Single(context.Messages);
        Assert.IsType<PingResponse>(context.Messages[0]);
    }

    private sealed class TestHandlerContext : HandlerContext
    {
        public IList<Object> Messages { get; } = [];

        public override void FireRead(Object message) => Messages.Add(message);
    }

    private sealed class TestExtendOwner : IExtend
    {
        public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

        public Object? this[String key]
        {
            get => Items.TryGetValue(key, out var value) ? value : null;
            set => Items[key] = value;
        }
    }

    private sealed class TestMatchQueue : IMatchQueue
    {
        public Int32 MatchCount { get; private set; }

        public Object? LastResponse { get; private set; }

        public Object? LastResult { get; private set; }

        public void Add(Object? owner, Object request, Int32 msTimeout, Object source) { }

        public Boolean Match(Object? owner, Object response, Object result, Func<Object?, Object?, Boolean> callback)
        {
            MatchCount++;
            LastResponse = response;
            LastResult = result;

            return true;
        }

        public void Clear() { }
    }
}