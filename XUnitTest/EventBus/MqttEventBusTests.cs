using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.EventBus;
using NewLife.MQTT.Messaging;
using NewLife.Messaging;
using Xunit;
using XUnitTest.Integration;

namespace XUnitTest.EventBus;

/// <summary>MQTT事件总线测试</summary>
public class MqttEventBusTests : IClassFixture<MqttServerFixture>
{
    private readonly Int32 _port;

    public MqttEventBusTests(MqttServerFixture fixture) => _port = fixture.Port;

    private MqttClient CreateClient(String clientId) => new()
    {
        Log = XTrace.Log,
        Server = $"tcp://127.0.0.1:{_port}",
        ClientId = clientId,
        Timeout = 5_000,
        Reconnect = false,
    };

    [Fact(DisplayName = "默认广播订阅，主题前缀与共享订阅过滤器构建")]
    public void BuildTopicFilter()
    {
        using var client = CreateClient("eb_filter");
        using var factory = new MqttEventBusFactory(client);

        using var bus = (MqttEventBus<TestEvent>)factory.CreateEventBus<TestEvent>("evt/a", "g1")!;
        Assert.Equal("evt/a", bus.FullTopic);
        Assert.Equal("evt/a", bus.TopicFilter);

        factory.TopicPrefix = "event";
        Assert.Equal("event/evt/a", factory.BuildTopic("evt/a"));

        factory.UseSharedSubscription = true;
        Assert.Equal("$share/g1/event/evt/a", factory.BuildTopicFilter("evt/a", "g1"));

        factory.UseSharedSubscription = false;
        Assert.Equal("event/evt/a", factory.BuildTopicFilter("evt/a", "g1"));
    }

    [Fact(DisplayName = "事件信封经默认编码器 JSON 往返")]
    public void EnvelopeRoundtrip()
    {
        var encoder = new DefaultPacketEncoder();
        var envelope = new MqttEventEnvelope<TestEvent>
        {
            ClientId = "node1",
            Topic = "evt/a",
            Data = new TestEvent { Name = "n", Value = 42 },
        };

        var packet = encoder.Encode(envelope);
        Assert.NotNull(packet);

        var back = encoder.Decode<MqttEventEnvelope<TestEvent>>(packet!);
        Assert.NotNull(back);
        Assert.Equal("node1", back!.ClientId);
        Assert.Equal("evt/a", back.Topic);
        Assert.NotNull(back.Data);
        Assert.Equal("n", back.Data!.Name);
        Assert.Equal(42, back.Data.Value);
    }

    [Fact(DisplayName = "广播订阅：所有节点都收到，发布方自身事件被排除")]
    public async Task Broadcast_DeliversToAllNodes()
    {
        using var pubClient = CreateClient("eb_pub");
        using var subClient = CreateClient("eb_sub");

        await pubClient.ConnectAsync();
        await subClient.ConnectAsync();

        using var pubFactory = new MqttEventBusFactory(pubClient) { Log = XTrace.Log, TopicPrefix = "event" };
        using var subFactory = new MqttEventBusFactory(subClient) { Log = XTrace.Log, TopicPrefix = "event" };
        using var pubBus = (MqttEventBus<TestEvent>)pubFactory.CreateEventBus<TestEvent>("evt/broadcast", "node_pub")!;
        using var subBus = (MqttEventBus<TestEvent>)subFactory.CreateEventBus<TestEvent>("evt/broadcast", "node_sub")!;

        pubBus.ClientId = "node_pub";
        subBus.ClientId = "node_sub";

        var selfReceived = 0;
        var remoteTcs = new TaskCompletionSource<TestEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        await pubBus.SubscribeAsync(new DelegateHandler<TestEvent>((e, c, t) =>
        {
            Interlocked.Increment(ref selfReceived);
            return Task.CompletedTask;
        }), "node_pub");
        await subBus.SubscribeAsync(new DelegateHandler<TestEvent>((e, c, t) =>
        {
            remoteTcs.TrySetResult(e);
            return Task.CompletedTask;
        }), "node_sub");

        await Task.Delay(200);

        await pubBus.PublishAsync(new TestEvent { Name = "hello", Value = 7 });

        var received = await remoteTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("hello", received.Name);
        Assert.Equal(7, received.Value);

        // 发布方自身收到回环消息，但处理器标识与信封 ClientId 相同，应被排除
        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref selfReceived));
    }

    [Fact(DisplayName = "共享订阅：同组内只有一个节点收到事件")]
    public async Task SharedSubscription_OnlyOneNodeReceives()
    {
        using var pubClient = CreateClient("eb_share_pub");
        using var subClient1 = CreateClient("eb_share_1");
        using var subClient2 = CreateClient("eb_share_2");

        await pubClient.ConnectAsync();
        await subClient1.ConnectAsync();
        await subClient2.ConnectAsync();

        using var pubFactory = new MqttEventBusFactory(pubClient) { Log = XTrace.Log };
        using var factory1 = new MqttEventBusFactory(subClient1) { Log = XTrace.Log, UseSharedSubscription = true };
        using var factory2 = new MqttEventBusFactory(subClient2) { Log = XTrace.Log, UseSharedSubscription = true };

        using var pubBus = (MqttEventBus<TestEvent>)pubFactory.CreateEventBus<TestEvent>("evt/share", "pub")!;
        using var bus1 = (MqttEventBus<TestEvent>)factory1.CreateEventBus<TestEvent>("evt/share", "g_share")!;
        using var bus2 = (MqttEventBus<TestEvent>)factory2.CreateEventBus<TestEvent>("evt/share", "g_share")!;

        Assert.Equal("$share/g_share/evt/share", bus1.TopicFilter);

        var count = 0;
        await bus1.SubscribeAsync(new DelegateHandler<TestEvent>((e, c, t) =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }), "n1");
        await bus2.SubscribeAsync(new DelegateHandler<TestEvent>((e, c, t) =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }), "n2");

        await Task.Delay(200);

        await pubBus.PublishAsync(new TestEvent { Name = "share", Value = 1 });

        await Task.Delay(800);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact(DisplayName = "关闭发布方标识后直接传输原始事件体")]
    public async Task WithoutClientId_RawPayload()
    {
        using var pubClient = CreateClient("eb_raw_pub");
        using var subClient = CreateClient("eb_raw_sub");

        await pubClient.ConnectAsync();
        await subClient.ConnectAsync();

        using var pubFactory = new MqttEventBusFactory(pubClient) { Log = XTrace.Log, AttachClientId = false };
        using var subFactory = new MqttEventBusFactory(subClient) { Log = XTrace.Log, AttachClientId = false };
        using var pubBus = (MqttEventBus<TestEvent>)pubFactory.CreateEventBus<TestEvent>("evt/raw", "pub")!;
        using var subBus = (MqttEventBus<TestEvent>)subFactory.CreateEventBus<TestEvent>("evt/raw", "sub")!;

        var tcs = new TaskCompletionSource<TestEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subBus.SubscribeAsync(new DelegateHandler<TestEvent>((e, c, t) =>
        {
            tcs.TrySetResult(e);
            return Task.CompletedTask;
        }), "sub");

        await Task.Delay(200);

        await pubBus.PublishAsync(new TestEvent { Name = "raw", Value = 9 });

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("raw", received.Name);
        Assert.Equal(9, received.Value);
    }

    private sealed class DelegateHandler<TEvent> : IEventHandler<TEvent>
    {
        private readonly Func<TEvent, IEventContext?, CancellationToken, Task> _func;

        public DelegateHandler(Func<TEvent, IEventContext?, CancellationToken, Task> func) => _func = func;

        public Task HandleAsync(TEvent @event, IEventContext? context, CancellationToken cancellationToken) => _func(@event, context, cancellationToken);
    }
}

/// <summary>测试事件</summary>
public class TestEvent
{
    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>数值</summary>
    public Int32 Value { get; set; }
}
