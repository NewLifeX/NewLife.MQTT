using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Messaging;

namespace NewLife.MQTT.EventBus;

/// <summary>MQTT事件总线。基于 MQTT 主题的强类型事件发布/订阅</summary>
/// <remarks>
/// <para>发布：把事件（默认包裹 <see cref="MqttEventEnvelope{TEvent}"/>）编码为 JSON 后发布到主题；订阅：由工厂统一订阅主题，
/// 收到消息后在本机解码并分发给全部处理器。</para>
/// <para>回环排除：信封携带发布方 <see cref="ClientId"/>，分发时写入 <see cref="EventContext.ClientId"/>，
/// 相同标识的处理器会被跳过。约定订阅时 <see cref="IEventBus{TEvent}.SubscribeAsync"/> 的 clientId 使用与本节点 <see cref="ClientId"/> 相同的值。</para>
/// <para>可靠性：默认 QoS1（至少一次）。MQTT 不提供服务端业务重试，需要离线补偿时应配合持久会话（CleanSession=false）或离线消息存储。</para>
/// </remarks>
/// <typeparam name="TEvent">事件类型</typeparam>
public class MqttEventBus<TEvent> : EventBus<TEvent>, ITracerFeature
{
    #region 属性
    /// <summary>发布方标识字段名。保留用于与 Redis 事件总线的标识约定保持一致</summary>
    public const String ClientIdKey = "__clientId";

    /// <summary>事件总线工厂</summary>
    public MqttEventBusFactory Factory { get; }

    /// <summary>事件主题（不含前缀）</summary>
    public String Topic { get; }

    /// <summary>实际发布主题（含工厂前缀）</summary>
    public String FullTopic { get; }

    /// <summary>订阅过滤器。广播模式等于 <see cref="FullTopic"/>，共享订阅模式带 <c>$share/{group}/</c> 前缀</summary>
    public String TopicFilter { get; }

    /// <summary>发布方客户标识。默认取 <see cref="Runtime.ClientId"/>，需与本节点订阅时使用的 clientId 保持一致才能排除自身事件</summary>
    public String ClientId { get; set; } = Runtime.ClientId;

    /// <summary>是否在消息中附加发布方标识，默认true。关闭后直接发布原始事件体</summary>
    public Boolean AttachClientId { get; set; } = true;

    /// <summary>服务质量。默认至少一次</summary>
    public QualityOfService QoS { get; set; } = QualityOfService.AtLeastOnce;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    private readonly Action<PublishMessage> _sink;
    #endregion

    #region 构造
    internal MqttEventBus(MqttEventBusFactory factory, String topic, String clientId)
    {
        Factory = factory;
        Topic = topic;
        FullTopic = factory.BuildTopic(topic);
        TopicFilter = factory.BuildTopicFilter(topic, clientId);
        AttachClientId = factory.AttachClientId;
        QoS = factory.QoS;
        Tracer = factory.Tracer;
        Log = factory.Log;

        _sink = OnMessage;
    }
    #endregion

    #region 发布
    /// <summary>发布事件</summary>
    /// <param name="event">事件</param>
    /// <param name="context">事件上下文。MQTT 主题由总线自身决定，上下文仅用于兼容统一契约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>固定返回1，表示消息已交给客户端发送；不等待远端处理器数量</returns>
    public override Task<Int32> PublishAsync(TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        // 待发布消息增加追踪标识
        if (@event is ITraceMessage tm && tm.TraceId.IsNullOrEmpty()) tm.TraceId = DefaultSpan.Current?.ToString();

        var data = AttachClientId
            ? (Object)new MqttEventEnvelope<TEvent> { ClientId = ClientId, Topic = FullTopic, Data = @event }
            : @event;

        return PublishCoreAsync(data, cancellationToken);
    }

    private async Task<Int32> PublishCoreAsync(Object data, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan($"mqtt:EventBus:{Topic}", FullTopic);

        await Factory.Client.PublishAsync(FullTopic, data, QoS).ConfigureAwait(false);

        return 1;
    }
    #endregion

    #region 订阅
    /// <summary>订阅事件。首次订阅时向服务端订阅主题</summary>
    /// <param name="handler">事件处理器</param>
    /// <param name="clientId">客户标识。同一标识重复订阅时覆盖前一次</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否订阅成功</returns>
    public override async Task<Boolean> SubscribeAsync(IEventHandler<TEvent> handler, String clientId = "", CancellationToken cancellationToken = default)
    {
        var rs = await base.SubscribeAsync(handler, clientId, cancellationToken).ConfigureAwait(false);

        await Factory.EnsureSubscribedAsync(TopicFilter, _sink, cancellationToken).ConfigureAwait(false);

        return rs;
    }

    /// <summary>收到消息。由工厂分发出线程调用，立即返回，实际处理在后台任务中完成</summary>
    /// <param name="message">MQTT消息</param>
    private void OnMessage(PublishMessage message) => _ = Task.Run(() => ProcessAsync(message));

    private async Task ProcessAsync(PublishMessage message)
    {
        try
        {
            await DispatchMessageAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log?.Error("MQTT event bus [{0}] failed to process message: {1}", FullTopic, ex.Message);
        }
    }

    private async Task DispatchMessageAsync(PublishMessage message)
    {
        var payload = message.Payload;
        if (payload == null) return;

        var context = new MqttEventContext
        {
            EventBus = this,
            Message = message,
            Topic = Topic,
        };

        TEvent? @event;
        if (AttachClientId)
        {
            var envelope = Factory.Client.Encoder.Decode<MqttEventEnvelope<TEvent>>(payload);
            if (envelope == null)
            {
                Log?.Error("MQTT event bus [{0}] failed to decode the event envelope", FullTopic);
                return;
            }

            context.ClientId = envelope.ClientId;
            @event = envelope.Data;
        }
        else
        {
            @event = Factory.Client.Encoder.Decode<TEvent>(payload);
        }

        if (@event == null)
        {
            Log?.Error("MQTT event bus [{0}] failed to decode the event", FullTopic);
            return;
        }

        using var span = Tracer?.NewSpan($"mqtt:EventBus:{Topic}:Dispatch", FullTopic);

        await DispatchAsync(@event, context, CancellationToken.None).ConfigureAwait(false);
    }
    #endregion

    #region 释放
    /// <summary>释放事件总线，注销工厂中的订阅者</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing) Factory.Remove(TopicFilter, _sink);
    }
    #endregion
}
