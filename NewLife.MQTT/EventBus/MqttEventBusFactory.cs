using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Messaging;

namespace NewLife.MQTT.EventBus;

/// <summary>MQTT事件总线工厂</summary>
/// <remarks>
/// <para>基于一个已连接的 <see cref="MqttClient"/> 创建强类型事件总线，使业务代码可以在 MQTT、Redis 与进程内三种通道之间自由切换（统一 <see cref="IEventBusFactory"/> 契约）。</para>
/// <para>默认广播订阅：同一主题的所有订阅节点都会收到事件。开启 <see cref="UseSharedSubscription"/> 后改为 MQTT5 共享订阅
/// <c>$share/{group}/{topic}</c>，由服务端在组内轮询一个订阅者，语义对齐 Redis Stream 消费组。</para>
/// <para>同一主题只向客户端订阅一次，收到消息后再分发到本工厂创建的全部事件总线，避免 <see cref="MqttClient"/> 按主题过滤器去重导致后续订阅拿不到消息。</para>
/// </remarks>
public class MqttEventBusFactory : IEventBusFactory, IDisposable
{
    #region 属性
    /// <summary>MQTT客户端。连接、重连与断开由调用方负责，工厂不接管其生命周期</summary>
    public MqttClient Client { get; }

    /// <summary>主题前缀。用于给事件主题划分命名空间，如 <c>event/</c></summary>
    public String? TopicPrefix { get; set; }

    /// <summary>是否使用共享订阅（<c>$share/{group}/{topic}</c>）。默认false即广播，所有订阅节点都收到</summary>
    public Boolean UseSharedSubscription { get; set; }

    /// <summary>是否在消息中附加发布方标识，默认true</summary>
    public Boolean AttachClientId { get; set; } = true;

    /// <summary>服务质量。默认至少一次</summary>
    public QualityOfService QoS { get; set; } = QualityOfService.AtLeastOnce;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private readonly ConcurrentDictionary<String, List<Action<PublishMessage>>> _sinks = [];
    private readonly Object _lock = new();
    private Boolean _hooked;
    #endregion

    #region 构造
    /// <summary>实例化事件总线工厂</summary>
    /// <param name="client">已连接的MQTT客户端</param>
    public MqttEventBusFactory(MqttClient client) => Client = client ?? throw new ArgumentNullException(nameof(client));
    #endregion

    #region 创建
    /// <summary>创建事件总线，可发布消息或订阅消息</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="topic">事件主题</param>
    /// <param name="clientId">客户标识/消息分组。共享订阅模式下作为消费组名</param>
    /// <returns>事件总线实例</returns>
    public IEventBus<TEvent>? CreateEventBus<TEvent>(String topic, String clientId = "") => new MqttEventBus<TEvent>(this, topic, clientId);

    /// <summary>构建事件发布主题（附加 <see cref="TopicPrefix"/>）</summary>
    /// <param name="topic">事件主题</param>
    /// <returns>实际发布主题</returns>
    public String BuildTopic(String topic)
    {
        if (TopicPrefix.IsNullOrEmpty()) return topic;

        return TopicPrefix!.EnsureEnd("/") + topic;
    }

    /// <summary>构建事件订阅过滤器</summary>
    /// <param name="topic">事件主题</param>
    /// <param name="clientId">客户标识/消息分组，为空时取 <see cref="Runtime.ClientId"/></param>
    /// <returns>广播模式下等于发布主题，共享订阅模式下带 <c>$share/{group}/</c> 前缀</returns>
    public String BuildTopicFilter(String topic, String clientId)
    {
        var actual = BuildTopic(topic);
        if (!UseSharedSubscription) return actual;

        var group = clientId.IsNullOrEmpty() ? Runtime.ClientId : clientId;

        return $"$share/{group}/{actual}";
    }
    #endregion

    #region 订阅管理
    /// <summary>登记订阅者，首次登记时向服务端订阅主题</summary>
    /// <param name="topicFilter">订阅过滤器</param>
    /// <param name="sink">消息回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    internal async Task EnsureSubscribedAsync(String topicFilter, Action<PublishMessage> sink, CancellationToken cancellationToken)
    {
        var needSubscribe = false;
        lock (_lock)
        {
            if (!_sinks.TryGetValue(topicFilter, out var list))
            {
                list = [];
                _sinks[topicFilter] = list;
            }

            if (!list.Contains(sink))
            {
                needSubscribe = list.Count == 0;
                list.Add(sink);
            }
        }

        if (!needSubscribe) return;

        Hook();

        // 订阅只需告知服务端，消息统一由 Received 事件分发，避免客户端按过滤器去重后回调被挤掉
        await Client.SubscribeAsync(topicFilter, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>移除订阅者，最后一个订阅者移除时取消服务端订阅</summary>
    /// <param name="topicFilter">订阅过滤器</param>
    /// <param name="sink">消息回调</param>
    internal void Remove(String topicFilter, Action<PublishMessage> sink)
    {
        var needUnsubscribe = false;
        lock (_lock)
        {
            if (_sinks.TryGetValue(topicFilter, out var list))
            {
                list.Remove(sink);
                if (list.Count == 0)
                {
                    _sinks.TryRemove(topicFilter, out _);
                    needUnsubscribe = true;
                }
            }
        }

        if (needUnsubscribe) _ = Task.Run(() => UnsubscribeAsync(topicFilter));
    }

    private async Task UnsubscribeAsync(String topicFilter)
    {
        try
        {
            await Client.UnsubscribeAsync([topicFilter]).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log?.Error("MQTT event bus failed to unsubscribe: {0}", ex.Message);
        }
    }

    private void Hook()
    {
        if (_hooked) return;

        lock (_lock)
        {
            if (_hooked) return;

            Client.Received += OnReceived;
            _hooked = true;
        }
    }

    private void OnReceived(Object? sender, EventArgs<PublishMessage> e)
    {
        var message = e.Arg;
        if (message?.Topic == null || _sinks.IsEmpty) return;

        foreach (var item in _sinks)
        {
            if (!MqttTopicFilter.IsMatch(message.Topic, item.Key)) continue;

            Action<PublishMessage>[] sinks;
            lock (_lock) sinks = item.Value.ToArray();

            foreach (var sink in sinks)
            {
                try
                {
                    sink(message);
                }
                catch (Exception ex)
                {
                    Log?.Error("MQTT event bus dispatch failed: {0}", ex.Message);
                }
            }
        }
    }
    #endregion

    #region 释放
    /// <summary>释放工厂。只解除客户端事件挂载，不关闭调用方提供的客户端</summary>
    public void Dispose()
    {
        if (_hooked)
        {
            Client.Received -= OnReceived;
            _hooked = false;
        }

        _sinks.Clear();
    }
    #endregion
}
