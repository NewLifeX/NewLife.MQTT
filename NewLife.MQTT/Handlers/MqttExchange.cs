using System.Collections.Concurrent;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Threading;

namespace NewLife.MQTT.Handlers;

/// <summary>消息交换机。提供会话管理与消息转发</summary>
public class MqttExchange : DisposeBase, IMqttExchange, ITracerFeature
{
    #region 属性
    /// <summary>会话过期时间。默认10分钟</summary>
    public TimeSpan Expire { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>运行统计数据</summary>
    public MqttStats Stats { get; } = new();

    /// <summary>是否启用 $SYS 系统主题。默认true</summary>
    public Boolean EnableSysTopic { get; set; } = true;

    /// <summary>$SYS 主题发布间隔（秒）。默认60秒</summary>
    public Int32 SysTopicInterval { get; set; } = 60;

    /// <summary>本地缓存，保存设备的对象引用，具备定时清理能力</summary>
    private readonly ConcurrentDictionary<Int32, IMqttHandler> _sessions = new();
    private TimerX? _timer;
    private TimerX? _sysTimer;
    private TimerX? _statsTimer;

    /// <summary>保留消息存储。key=主题，value=保留消息</summary>
    private readonly ConcurrentDictionary<String, PublishMessage> _retainMessages = new();

    /// <summary>持久化会话存储。key=ClientId，value=会话状态</summary>
    private readonly ConcurrentDictionary<String, PersistentSession> _persistentSessions = new();

    /// <summary>客户端标识映射。key=sessionId，value=clientId</summary>
    private readonly ConcurrentDictionary<Int32, String> _clientIdMap = new();
    #endregion

    #region 构造
    /// <summary>实例化交换机</summary>
    public MqttExchange() { }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
        _sysTimer.TryDispose();
        _statsTimer.TryDispose();
    }
    #endregion

    #region 会话管理
    /// <summary>添加会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public Boolean Add(Int32 sessionId, IMqttHandler session)
    {
        _timer ??= new TimerX(RemoveNotAlive, null, 30_000, 30_000);

        // 启动统计和 $SYS 定时器
        _statsTimer ??= new TimerX(s => Stats.CalculateRates(), null, 1000, 1000);
        if (EnableSysTopic)
            _sysTimer ??= new TimerX(PublishSysTopics, null, SysTopicInterval * 1000, SysTopicInterval * 1000);

        var ok = _sessions.TryAdd(sessionId, session);
        if (ok)
        {
            Interlocked.Increment(ref Stats.ConnectedClients);
            Interlocked.Increment(ref Stats.TotalConnections);

            if (Stats.ConnectedClients > Stats.MaxConnectedClients)
                Stats.MaxConnectedClients = Stats.ConnectedClients;
        }

        return ok;
    }

    /// <summary>注册客户端标识映射</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="clientId">客户端标识</param>
    public void RegisterClientId(Int32 sessionId, String clientId)
    {
        if (!clientId.IsNullOrEmpty())
            _clientIdMap[sessionId] = clientId;
    }

    /// <summary>获取会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public Boolean TryGetValue(Int32 sessionId, out IMqttHandler session) => _sessions.TryGetValue(sessionId, out session);

    /// <summary>删除会话</summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public Boolean Remove(Int32 sessionId)
    {
        _clientIdMap.TryRemove(sessionId, out _);

        if (!_sessions.TryRemove(sessionId, out var session)) return false;

        Interlocked.Decrement(ref Stats.ConnectedClients);

        session.TryDispose();

        return true;
    }

    private void RemoveNotAlive(Object state)
    {
        using var span = Tracer?.NewSpan("mqtt:SessionManager:RemoveNotAlive");

        // 找到不活跃会话，并销毁它
        var exp = DateTime.Now.Subtract(Expire);
        var dic = new Dictionary<Int32, IMqttHandler>();
        foreach (var item in _sessions)
        {
            if (item.Value is MqttHandler handler)
            {
                if (handler.Session == null)
                    dic.Add(item.Key, item.Value);
                else if (handler.Session is not NetSession session || session.Disposed || session.Session == null)
                    dic.Add(item.Key, item.Value);
                else if (session.Session.LastTime < exp)
                    dic.Add(item.Key, item.Value);
            }
        }

        foreach (var item in dic)
        {
            _sessions.TryRemove(item.Key, out _);

            // 销毁过期会话，促使断开连接
            var handler = item.Value;
            handler.Close(nameof(RemoveNotAlive));
            handler.TryDispose();
        }
    }
    #endregion

    #region 消息管理
    class SubscriptionItem
    {
        public Int32 Id { get; set; }
        public QualityOfService QoS { get; set; }

        /// <summary>不转发自身发布的消息。MQTT 5.0</summary>
        public Boolean NoLocal { get; set; }

        /// <summary>保留消息按发布时的状态转发。MQTT 5.0</summary>
        public Boolean RetainAsPublished { get; set; }

        /// <summary>保留消息处理方式。MQTT 5.0（0=订阅时发送，1=仅新订阅时发送，2=不发送）</summary>
        public Byte RetainHandling { get; set; }
    }

    /// <summary>主题订阅集合</summary>
    private ConcurrentDictionary<String, List<SubscriptionItem>> _topics = new();

    /// <summary>共享订阅轮询索引。key=共享组名，value=当前索引</summary>
    private readonly ConcurrentDictionary<String, Int32> _sharedGroupIndex = new();

    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息。
    /// 如果消息带有 Retain 标志，还会存储/清除保留消息。
    /// </remarks>
    /// <param name="message">发布消息</param>
    public virtual void Publish(PublishMessage message) => Publish(message, -1);

    /// <summary>发布消息（指定发布者会话ID，用于 NoLocal 过滤）</summary>
    /// <param name="message">发布消息</param>
    /// <param name="publisherSessionId">发布者会话标识，用于 NoLocal 过滤，-1 表示不过滤</param>
    public virtual void Publish(PublishMessage message, Int32 publisherSessionId)
    {
        // 统计接收
        Interlocked.Increment(ref Stats.MessagesReceived);

        // 处理保留消息
        if (message.Retain)
        {
            if (message.Payload == null || message.Payload.Total == 0)
            {
                // 空 payload 的 Retain 消息表示清除该主题的保留消息
                _retainMessages.TryRemove(message.Topic, out _);
            }
            else
            {
                // 存储保留消息（覆盖同主题的旧消息）
                _retainMessages[message.Topic] = message;
            }
            Stats.RetainedMessages = _retainMessages.Count;
        }

        // $SYS 主题不对普通通配符订阅者分发
        if (message.Topic.StartsWith("$SYS/")) return;

        // 遍历所有Topic，找到匹配的订阅者
        foreach (var item in _topics)
        {
            if (!MqttTopicFilter.IsMatch(message.Topic, item.Key)) continue;

            // 检查是否为共享订阅（$share/{group}/{topic}）
            var topicFilter = item.Key;
            var isShared = topicFilter.StartsWith("$share/");

            var subs = item.Value;
            if (isShared)
            {
                // 共享订阅：轮询选择一个订阅者
                var groupKey = topicFilter;
                var activeSubs = subs.Where(s => _sessions.ContainsKey(s.Id)).ToArray();
                if (activeSubs.Length > 0)
                {
                    var idx = _sharedGroupIndex.AddOrUpdate(groupKey, 0, (_, v) => v + 1);
                    var sub = activeSubs[idx % activeSubs.Length];

                    if (_sessions.TryGetValue(sub.Id, out var session))
                    {
                        var msg = new PublishMessage
                        {
                            Topic = message.Topic,
                            Payload = message.Payload,
                            QoS = sub.QoS,
                        };
                        session.PublishAsync(msg);
                        Interlocked.Increment(ref Stats.MessagesSent);
                    }
                }
            }
            else
            {
                // 普通订阅：发送给所有匹配的订阅者
                foreach (var sub in subs.ToArray())
                {
                    // MQTT 5.0 NoLocal：不转发给发布者自身
                    if (sub.NoLocal && sub.Id == publisherSessionId) continue;

                    if (_sessions.TryGetValue(sub.Id, out var session))
                    {
                        // 使用指定Qos发送消息
                        // MQTT 5.0 RetainAsPublished：保留消息按发布时的状态转发
                        var msg = new PublishMessage
                        {
                            Topic = message.Topic,
                            Payload = message.Payload,
                            QoS = sub.QoS,
                            Retain = sub.RetainAsPublished && message.Retain,
                        };
                        session.PublishAsync(msg);
                        Interlocked.Increment(ref Stats.MessagesSent);
                    }
                    else
                    {
                        // 没有找到订阅者，删除订阅关系
                        lock (subs)
                        {
                            subs.Remove(sub);
                        }
                    }
                }
            }
        }
    }

    /// <summary>订阅主题</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos) => Subscribe(sessionId, topic, qos, false, false, 0);

    /// <summary>订阅主题（含 MQTT 5.0 订阅选项）</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    /// <param name="noLocal">不转发自身发布的消息</param>
    /// <param name="retainAsPublished">保留消息按发布时的状态转发</param>
    /// <param name="retainHandling">保留消息处理方式</param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos, Boolean noLocal, Boolean retainAsPublished, Byte retainHandling)
    {
        // 保存订阅关系
        var subs = _topics.GetOrAdd(topic, []);
        var isNewSub = false;

        lock (subs)
        {
            // 检查是否为新订阅
            isNewSub = !subs.Any(e => e.Id == sessionId);

            // 删除旧的订阅关系
            subs.RemoveAll(e => e.Id == sessionId);
            subs.Add(new SubscriptionItem
            {
                Id = sessionId,
                QoS = qos,
                NoLocal = noLocal,
                RetainAsPublished = retainAsPublished,
                RetainHandling = retainHandling,
            });
        }

        // 更新统计
        Stats.Topics = _topics.Count;

        // 根据 RetainHandling 决定是否推送保留消息
        // 0=订阅时发送，1=仅新订阅时发送，2=不发送
        if (retainHandling == 2) return;
        if (retainHandling == 1 && !isNewSub) return;

        // 向订阅者推送匹配的保留消息
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            foreach (var retain in _retainMessages)
            {
                if (MqttTopicFilter.IsMatch(retain.Key, topic))
                {
                    var msg = new PublishMessage
                    {
                        Topic = retain.Value.Topic,
                        Payload = retain.Value.Payload,
                        QoS = qos,
                        Retain = retainAsPublished || retain.Value.Retain,
                    };
                    session.PublishAsync(msg);
                }
            }
        }
    }

    /// <summary>取消主题订阅</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    public virtual void Unsubscribe(Int32 sessionId, String topic)
    {
        if (_topics.TryGetValue(topic, out var subs))
        {
            lock (subs)
            {
                subs.RemoveAll(e => e.Id == sessionId);
            }

            // 没有订阅者了，删除主题
            if (subs.Count == 0) _topics.TryRemove(topic, out _);
        }
    }

    /// <summary>获取匹配主题的保留消息</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <returns></returns>
    public IList<PublishMessage> GetRetainMessages(String topicFilter)
    {
        var list = new List<PublishMessage>();
        foreach (var item in _retainMessages)
        {
            if (MqttTopicFilter.IsMatch(item.Key, topicFilter))
                list.Add(item.Value);
        }
        return list;
    }

    /// <summary>保存持久化会话（CleanSession=0）</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="sessionId">会话标识</param>
    /// <param name="subscriptions">订阅关系</param>
    public virtual void SavePersistentSession(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions)
    {
        if (clientId.IsNullOrEmpty()) return;

        var session = _persistentSessions.GetOrAdd(clientId, k => new PersistentSession { ClientId = k });
        session.SessionId = sessionId;
        session.UpdateTime = DateTime.Now;

        if (subscriptions != null)
        {
            foreach (var item in subscriptions)
            {
                session.Subscriptions[item.Key] = item.Value;
            }
        }
    }

    /// <summary>恢复持久化会话</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="sessionId">新会话标识</param>
    /// <returns>是否存在旧会话</returns>
    public virtual Boolean RestorePersistentSession(String clientId, Int32 sessionId)
    {
        if (clientId.IsNullOrEmpty()) return false;
        if (!_persistentSessions.TryGetValue(clientId, out var session)) return false;

        // 恢复订阅关系
        foreach (var item in session.Subscriptions)
        {
            Subscribe(sessionId, item.Key, item.Value);
        }

        // 更新会话标识
        session.SessionId = sessionId;

        // 推送离线消息
        while (session.OfflineMessages.TryDequeue(out var msg))
        {
            if (_sessions.TryGetValue(sessionId, out var handler))
                handler.PublishAsync(msg);
        }

        return true;
    }

    /// <summary>清除持久化会话</summary>
    /// <param name="clientId">客户端标识</param>
    public virtual void ClearPersistentSession(String clientId)
    {
        if (!clientId.IsNullOrEmpty())
            _persistentSessions.TryRemove(clientId, out _);
    }

    /// <summary>为离线的持久会话暂存消息</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="message">消息</param>
    public virtual void EnqueueOfflineMessage(String clientId, PublishMessage message)
    {
        if (clientId.IsNullOrEmpty()) return;
        if (!_persistentSessions.TryGetValue(clientId, out var session)) return;

        // 限制离线消息队列大小，防止内存溢出
        while (session.OfflineMessages.Count >= 1000)
        {
            session.OfflineMessages.TryDequeue(out _);
        }

        session.OfflineMessages.Enqueue(message);
    }
    #endregion

    #region 统计与查询
    /// <summary>获取所有在线客户端标识</summary>
    /// <returns></returns>
    public IList<String> GetClientIds() => _clientIdMap.Values.ToList();

    /// <summary>获取所有主题过滤器</summary>
    /// <returns></returns>
    public IList<String> GetTopics() => _topics.Keys.ToList();

    /// <summary>获取指定主题的订阅者数量</summary>
    /// <param name="topic">主题过滤器</param>
    /// <returns></returns>
    public Int32 GetSubscriberCount(String topic)
    {
        if (_topics.TryGetValue(topic, out var subs))
            return subs.Count;
        return 0;
    }

    /// <summary>更新统计快照</summary>
    private void UpdateStatsSnapshot()
    {
        Stats.ConnectedClients = _sessions.Count;
        Stats.Topics = _topics.Count;
        Stats.RetainedMessages = _retainMessages.Count;

        var subCount = 0;
        foreach (var item in _topics)
        {
            subCount += item.Value.Count;
        }
        Stats.Subscriptions = subCount;
    }
    #endregion

    #region $SYS 系统主题
    /// <summary>发布 $SYS 系统主题</summary>
    private void PublishSysTopics(Object? state)
    {
        using var span = Tracer?.NewSpan("mqtt:PublishSysTopics");

        UpdateStatsSnapshot();

        // 连接统计
        PublishSys("$SYS/broker/clients/connected", Stats.ConnectedClients.ToString());
        PublishSys("$SYS/broker/clients/total", Stats.TotalConnections.ToString());
        PublishSys("$SYS/broker/clients/maximum", Stats.MaxConnectedClients.ToString());

        // 消息统计
        PublishSys("$SYS/broker/messages/received", Stats.MessagesReceived.ToString());
        PublishSys("$SYS/broker/messages/sent", Stats.MessagesSent.ToString());
        PublishSys("$SYS/broker/messages/received/persecond", Stats.MessagesReceivedPerSecond.ToString());
        PublishSys("$SYS/broker/messages/sent/persecond", Stats.MessagesSentPerSecond.ToString());

        // 字节统计
        PublishSys("$SYS/broker/bytes/received", Stats.BytesReceived.ToString());
        PublishSys("$SYS/broker/bytes/sent", Stats.BytesSent.ToString());

        // 订阅统计
        PublishSys("$SYS/broker/subscriptions/count", Stats.Subscriptions.ToString());
        PublishSys("$SYS/broker/topics/count", Stats.Topics.ToString());
        PublishSys("$SYS/broker/retained messages/count", Stats.RetainedMessages.ToString());

        // 系统信息
        PublishSys("$SYS/broker/uptime", Stats.Uptime.ToString());
        PublishSys("$SYS/broker/version", Stats.Version);
    }

    /// <summary>发布单个 $SYS 主题消息</summary>
    private void PublishSys(String topic, String payload)
    {
        var msg = new PublishMessage
        {
            Topic = topic,
            Payload = (ArrayPacket)payload.GetBytes(),
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
        };

        // 存储为保留消息
        _retainMessages[topic] = msg;

        // 仅发送给显式订阅了 $SYS 主题的订阅者
        foreach (var item in _topics)
        {
            if (!item.Key.StartsWith("$SYS/")) continue;
            if (!MqttTopicFilter.IsMatch(topic, item.Key)) continue;

            foreach (var sub in item.Value.ToArray())
            {
                if (_sessions.TryGetValue(sub.Id, out var session))
                    session.PublishAsync(msg);
            }
        }
    }
    #endregion
}