using System.Collections.Concurrent;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Clusters;
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

    /// <summary>服务端指定的 KeepAlive（秒）。设置后将在 CONNACK 中通知 MQTT 5.0 客户端覆盖客户端 KeepAlive。0 表示不覆盖</summary>
    public UInt16 ServerKeepAlive { get; set; }

    /// <summary>服务端引用地址。MQTT 5.0，非空时在 CONNACK/DISCONNECT 中附带，引导客户端重定向到另一台 Broker</summary>
    public String? ServerReference { get; set; }

    /// <summary>规则引擎。设置后将在消息发布时调用规则处理</summary>
    public MqttRuleEngine? RuleEngine { get; set; }

    /// <summary>集群交换机。设置后，会话清理时自动广播退订通知</summary>
    public ClusterExchange? ClusterExchange { get; set; }

    /// <summary>本地缓存，保存设备的对象引用，具备定时清理能力</summary>
    private readonly ConcurrentDictionary<Int32, IMqttHandler> _sessions = new();
    private TimerX? _timer;
    private TimerX? _sysTimer;
    private TimerX? _statsTimer;

    /// <summary>保留消息存储。key=主题，value=保留消息</summary>
    private readonly ConcurrentDictionary<String, PublishMessage> _retainMessages = new();

    /// <summary>持久化会话存储。key=ClientId，value=会话状态</summary>
    private readonly ConcurrentDictionary<String, PersistentSession> _persistentSessions = new();

    /// <summary>持久会话反向索引。key=sessionId，value=clientId。用于 Publish 热路径 O(1) 定位离线会话，避免全量扫描</summary>
    private readonly ConcurrentDictionary<Int32, String> _persistentSessionIds = new();

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
        if (!_sessions.TryRemove(sessionId, out var session)) return false;

        // 在清理 _clientIdMap 之前清理订阅，使 RemoveSessionSubscriptions 能取到 clientId
        RemoveSessionSubscriptions(sessionId);

        _clientIdMap.TryRemove(sessionId, out _);

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

            // 清理该会话的所有集群订阅，避免残留订阅导致重复下发
            RemoveSessionSubscriptions(item.Key);

            // 销毁过期会话，促使断开连接
            var handler = item.Value;
            handler.Close(nameof(RemoveNotAlive));
            handler.TryDispose();
        }
    }

    /// <summary>清理指定会话的所有订阅（本地 _topics + 集群广播退订）</summary>
    /// <remarks>
    /// 仅在该主题无本地其他订阅者时才向集群广播退订，避免误删其他客户端在同一节点的订阅记录。
    /// 例如：客户端 A 和 B 都订阅了 "test/#"，A 断开时若立即广播退订，
    /// 集群节点会删除整个节点的订阅记录，导致 B 后续收不到跨节点消息。
    /// </remarks>
    private void RemoveSessionSubscriptions(Int32 sessionId)
    {
        if (ClusterExchange == null) return;

        _clientIdMap.TryGetValue(sessionId, out var clientId);

        var myEndpoint = ClusterExchange.Cluster.GetNodeInfo().EndPoint;
        var infos = new List<SubscriptionInfo>();

        foreach (var kv in _topics)
        {
            var (removed, remaining) = kv.Value.RemoveBySession(sessionId);

            // 只有该主题已无本地订阅者时，才通知集群节点删除订阅记录
            if (removed > 0 && remaining == 0)
                infos.Add(new SubscriptionInfo
                {
                    Topic = kv.Key,
                    Endpoint = myEndpoint,
                    ClientId = clientId,
                    SessionId = sessionId,
                });

            if (remaining == 0)
            {
                if (_topics.TryRemove(kv.Key, out _)) RemoveFromTopicIndex(kv.Key);

                // 主题清空时清理共享订阅轮询索引，避免内存增长
                if (kv.Key.StartsWith("$share/")) _sharedGroupIndex.TryRemove(kv.Key, out _);
            }
        }

        if (infos.Count == 0) return;

        // 向集群其他节点广播取消订阅，清理残留的 ClusterExchange._topics 条目
        var arr = infos.ToArray();
        foreach (var item in ClusterExchange.Cluster.Nodes)
        {
            try
            {
                item.Value.Unsubscribe(arr);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
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

        /// <summary>订阅标识符。MQTT 5.0，0 表示未设置；投递时附带在 PUBLISH Properties 中</summary>
        public UInt32 SubscriptionIdentifier { get; set; }
    }

    /// <summary>订阅者列表。写时复制快照，读路径（Publish）无锁零分配</summary>
    private sealed class SubscriptionList
    {
        private readonly List<SubscriptionItem> _list = [];
        private volatile SubscriptionItem[]? _snapshot;

        /// <summary>订阅者数量</summary>
        public Int32 Count
        {
            get
            {
                var arr = _snapshot;
                return arr != null ? arr.Length : _list.Count;
            }
        }

        /// <summary>获取快照。快照未失效时直接返回缓存数组</summary>
        /// <returns></returns>
        public SubscriptionItem[] Snapshot()
        {
            var arr = _snapshot;
            if (arr != null) return arr;

            lock (_list)
            {
                return _snapshot ??= _list.ToArray();
            }
        }

        /// <summary>添加或替换指定会话的订阅</summary>
        /// <param name="sessionId">会话标识</param>
        /// <param name="item">订阅项</param>
        /// <returns>是否为新订阅（之前不存在同会话订阅）</returns>
        public Boolean Upsert(Int32 sessionId, SubscriptionItem item)
        {
            lock (_list)
            {
                var exists = false;
                for (var i = 0; i < _list.Count; i++)
                {
                    if (_list[i].Id == sessionId)
                    {
                        exists = true;
                        break;
                    }
                }

                _list.RemoveAll(e => e.Id == sessionId);
                _list.Add(item);
                _snapshot = null;

                return !exists;
            }
        }

        /// <summary>移除指定会话的订阅</summary>
        /// <param name="sessionId">会话标识</param>
        /// <returns>移除数量与剩余数量（同一锁内读取，保证一致）</returns>
        public (Int32 Removed, Int32 Remaining) RemoveBySession(Int32 sessionId)
        {
            lock (_list)
            {
                var removed = _list.RemoveAll(e => e.Id == sessionId);
                if (removed > 0) _snapshot = null;

                return (removed, _list.Count);
            }
        }

        /// <summary>移除指定订阅项</summary>
        /// <param name="item">订阅项</param>
        public void Remove(SubscriptionItem item)
        {
            lock (_list)
            {
                if (_list.Remove(item)) _snapshot = null;
            }
        }
    }

    /// <summary>过滤器快照集合。写时复制，读取零分配</summary>
    private sealed class FilterBucket
    {
        private readonly List<String> _list = [];
        private volatile String[]? _snapshot;

        /// <summary>过滤器数量</summary>
        public Int32 Count
        {
            get
            {
                var arr = _snapshot;
                return arr != null ? arr.Length : _list.Count;
            }
        }

        /// <summary>获取快照。快照未失效时直接返回缓存数组</summary>
        /// <returns></returns>
        public String[] Snapshot()
        {
            var arr = _snapshot;
            if (arr != null) return arr;

            lock (_list)
            {
                return _snapshot ??= _list.ToArray();
            }
        }

        /// <summary>添加过滤器（去重）</summary>
        /// <param name="filter">主题过滤器</param>
        public void Add(String filter)
        {
            lock (_list)
            {
                if (_list.Contains(filter)) return;

                _list.Add(filter);
                _snapshot = null;
            }
        }

        /// <summary>移除过滤器</summary>
        /// <param name="filter">主题过滤器</param>
        /// <returns>是否移除成功</returns>
        public Boolean Remove(String filter)
        {
            lock (_list)
            {
                if (!_list.Remove(filter)) return false;

                _snapshot = null;
                return true;
            }
        }
    }

    /// <summary>主题订阅集合</summary>
    private readonly ConcurrentDictionary<String, SubscriptionList> _topics = new();

    /// <summary>共享订阅轮询索引。key=共享组名，value=当前索引</summary>
    private readonly ConcurrentDictionary<String, Int32> _sharedGroupIndex = new();

    /// <summary>主题前缀索引。key=过滤器首层主题段，用于 Publish 热路径按前缀快速定位候选过滤器</summary>
    private readonly ConcurrentDictionary<String, FilterBucket> _topicIndex = new();

    /// <summary>通配符开头的过滤器集合（+ 或 # 开头）。这类过滤器不与 $ 开头的主题匹配</summary>
    private readonly FilterBucket _wildcardFilters = new();

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
        Interlocked.Add(ref Stats.BytesReceived, message.Payload?.Length ?? 0);

        // 记录服务端接收时间，用于 MessageExpiryInterval 过期检查
        if (message.ReceivedAt == default) message.ReceivedAt = DateTime.Now;

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
            // $SYS 内部保留消息不计入用户保留消息统计
            if (!message.Topic.StartsWith("$SYS/", StringComparison.Ordinal))
                Stats.RetainedMessages = _retainMessages.Count(e => !e.Key.StartsWith("$SYS/", StringComparison.Ordinal));
        }

        // $SYS 主题不对普通通配符订阅者分发
        if (message.Topic.StartsWith("$SYS/", StringComparison.Ordinal)) return;

        // 检查消息是否已过期，过期消息不分发
        if (IsMessageExpired(message)) return;

        // 规则引擎处理（转发/丢弃/WebHook等），返回 false 表示丢弃
        if (RuleEngine != null)
        {
            _clientIdMap.TryGetValue(publisherSessionId, out var publisherClientId);
            if (!RuleEngine.ProcessMessage(message, publisherClientId)) return;
        }

        // 按首层主题段定位候选过滤器，避免对全部过滤器做全表扫描
        var topic = message.Topic;
        var firstSegment = GetFirstSegment(topic);
        if (_topicIndex.TryGetValue(firstSegment, out var bucket))
        {
            foreach (var filter in bucket.Snapshot())
            {
                RouteToFilter(filter, message, publisherSessionId);
            }
        }

        // 通配符开头的过滤器不与 $ 开头的主题匹配
        if (topic.Length == 0 || topic[0] != '$')
        {
            foreach (var filter in _wildcardFilters.Snapshot())
            {
                RouteToFilter(filter, message, publisherSessionId);
            }
        }
    }

    /// <summary>将消息路由到指定过滤器的订阅者</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <param name="message">发布消息</param>
    /// <param name="publisherSessionId">发布者会话标识</param>
    private void RouteToFilter(String topicFilter, PublishMessage message, Int32 publisherSessionId)
    {
        if (!MqttTopicFilter.IsMatch(message.Topic, topicFilter)) return;

        if (!_topics.TryGetValue(topicFilter, out var subs)) return;

        // 检查是否为共享订阅（$share/{group}/{topic}）
        var isShared = topicFilter.StartsWith("$share/", StringComparison.Ordinal);
        if (isShared)
        {
            // 共享订阅：轮询选择一个在线订阅者
            var groupKey = topicFilter;
            var snapshot = subs.Snapshot();
            var activeCount = 0;
            foreach (var s in snapshot)
            {
                if (_sessions.ContainsKey(s.Id)) activeCount++;
            }
            if (activeCount > 0)
            {
                var idx = _sharedGroupIndex.AddOrUpdate(groupKey, 0, (_, v) => v + 1);
                var target = idx % activeCount;

                SubscriptionItem? sub = null;
                foreach (var s in snapshot)
                {
                    if (!_sessions.ContainsKey(s.Id)) continue;
                    if (target-- == 0)
                    {
                        sub = s;
                        break;
                    }
                }

                if (sub != null && _sessions.TryGetValue(sub.Id, out var session))
                {
                    var msg = new PublishMessage
                    {
                        Topic = message.Topic,
                        Payload = message.Payload,
                        QoS = sub.QoS,
                        Properties = message.Properties,
                    };
                    // MQTT 5.0 订阅标识符回传
                    if (sub.SubscriptionIdentifier > 0)
                    {
                        msg.Properties = msg.Properties != null ? msg.Properties.Clone() : new MqttProperties();
                        msg.Properties.SetVariableInt(MqttPropertyId.SubscriptionIdentifier, (Int32)sub.SubscriptionIdentifier);
                    }
                    session.PublishAsync(msg);
                    Interlocked.Increment(ref Stats.MessagesSent);
                    Interlocked.Add(ref Stats.BytesSent, msg.Payload?.Length ?? 0);
                }
            }
        }
        else
        {
            // 普通订阅：发送给所有匹配的订阅者，同一会话只投递一次
            var snapshot = subs.Snapshot();
            HashSet<Int32>? delivered = snapshot.Length > 1 ? [] : null;
            foreach (var sub in snapshot)
            {
                // MQTT 5.0 NoLocal：不转发给发布者自身
                if (sub.NoLocal && sub.Id == publisherSessionId) continue;

                if (delivered != null && !delivered.Add(sub.Id)) continue;

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
                        Properties = message.Properties,
                    };
                    // MQTT 5.0 订阅标识符回传
                    if (sub.SubscriptionIdentifier > 0)
                    {
                        msg.Properties = msg.Properties != null ? msg.Properties.Clone() : new MqttProperties();
                        msg.Properties.SetVariableInt(MqttPropertyId.SubscriptionIdentifier, (Int32)sub.SubscriptionIdentifier);
                    }
                    session.PublishAsync(msg);
                    Interlocked.Increment(ref Stats.MessagesSent);
                    Interlocked.Add(ref Stats.BytesSent, msg.Payload?.Length ?? 0);
                    // WebHook 消息投递事件
                    if (RuleEngine?.WebHook is { } webHook2)
                    {
                        _clientIdMap.TryGetValue(sub.Id, out var subscriberClientId2);
                        webHook2.OnMessageDelivered(subscriberClientId2, message);
                    }
                }
                else
                {
                    // 订阅者不在线，检查是否为持久化会话，若是则暂存离线消息
                    var offlineSession = GetPersistentSessionBySessionId(sub.Id);
                    if (offlineSession != null && sub.QoS >= QualityOfService.AtLeastOnce)
                    {
                        // 必须复制 Payload 字节，避免共享网络接收缓冲区导致内容被后续消息覆写
                        var offlineMsg = new PublishMessage
                        {
                            Topic = message.Topic,
                            Payload = message.Payload != null ? (IPacket?)new ArrayPacket(message.Payload.ToArray()) : null,
                            QoS = sub.QoS,
                            Retain = sub.RetainAsPublished && message.Retain,
                        };
                        EnqueueOfflineMessage(offlineSession.ClientId, offlineMsg);
                    }
                    else
                    {
                        // 没有持久化会话，删除订阅关系
                        subs.Remove(sub);
                    }
                }
            }
        }
    }

    /// <summary>取主题的首层段（第一个 / 之前的部分）</summary>
    /// <param name="topic">主题</param>
    /// <returns></returns>
    private static String GetFirstSegment(String topic)
    {
        var index = topic.IndexOf('/');
        return index < 0 ? topic : topic.Substring(0, index);
    }

    /// <summary>将过滤器注册到前缀索引</summary>
    /// <param name="filter">主题过滤器</param>
    private void AddToTopicIndex(String filter)
    {
        var actual = MqttTopicFilter.ExtractActualTopicFilter(filter);
        var first = GetFirstSegment(actual);

        // + 或 # 开头的过滤器不与 $ 主题匹配，单独存放
        if (first.IndexOf('+') >= 0 || first.IndexOf('#') >= 0)
        {
            _wildcardFilters.Add(filter);
            return;
        }

        // 与移除操作竞争时桶可能被摘除，校验桶仍在索引中后完成注册
        while (true)
        {
            var bucket = _topicIndex.GetOrAdd(first, _ => new FilterBucket());
            bucket.Add(filter);

            if (_topicIndex.TryGetValue(first, out var current) && ReferenceEquals(current, bucket)) break;
        }
    }

    /// <summary>将过滤器从前缀索引移除</summary>
    /// <param name="filter">主题过滤器</param>
    private void RemoveFromTopicIndex(String filter)
    {
        var actual = MqttTopicFilter.ExtractActualTopicFilter(filter);
        var first = GetFirstSegment(actual);

        if (first.IndexOf('+') >= 0 || first.IndexOf('#') >= 0)
        {
            _wildcardFilters.Remove(filter);
            return;
        }

        if (_topicIndex.TryGetValue(first, out var bucket) && bucket.Remove(filter) && bucket.Count == 0)
        {
            // 值匹配移除，避免摘除并发添加过滤器的新桶
            ((ICollection<KeyValuePair<String, FilterBucket>>)_topicIndex).Remove(new(first, bucket));
        }
    }

    /// <summary>订阅主题</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos) => Subscribe(sessionId, topic, qos, false, false, 0, 0);
    /// <summary>订阅主题（含 MQTT 5.0 订阅选项）</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    /// <param name="noLocal">不转发自身发布的消息</param>
    /// <param name="retainAsPublished">保留消息按发布时的状态转发</param>
    /// <param name="retainHandling">保留消息处理方式</param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos, Boolean noLocal, Boolean retainAsPublished, Byte retainHandling)
        => Subscribe(sessionId, topic, qos, noLocal, retainAsPublished, retainHandling, 0);

    /// <summary>订阅主题（含 MQTT 5.0 全部订阅选项与订阅标识符）</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    /// <param name="noLocal">不转发自身发布的消息</param>
    /// <param name="retainAsPublished">保留消息按发布时的状态转发</param>
    /// <param name="retainHandling">保留消息处理方式</param>
    /// <param name="subscriptionIdentifier">订阅标识符（0=未设置）。MQTT 5.0，投递时回传给订阅者</param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos, Boolean noLocal, Boolean retainAsPublished, Byte retainHandling, UInt32 subscriptionIdentifier)
    {
        // 保存订阅关系
        var subs = _topics.GetOrAdd(topic, _ => new SubscriptionList());

        // 删除旧的订阅关系后写入新订阅
        var isNewSub = subs.Upsert(sessionId, new SubscriptionItem
        {
            Id = sessionId,
            QoS = qos,
            NoLocal = noLocal,
            RetainAsPublished = retainAsPublished,
            RetainHandling = retainHandling,
            SubscriptionIdentifier = subscriptionIdentifier,
        });

        // 注册到前缀索引（内部去重，重复订阅无副作用）
        AddToTopicIndex(topic);

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
                    // 订阅时检查 retain 消息是否已过期
                    if (IsMessageExpired(retain.Value)) continue;

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
            subs.RemoveBySession(sessionId);

            // 没有订阅者了，删除主题
            if (subs.Count == 0)
            {
                if (_topics.TryRemove(topic, out _)) RemoveFromTopicIndex(topic);

                // 共享订阅主题清空时清理轮询索引，避免内存增长
                if (topic.StartsWith("$share/")) _sharedGroupIndex.TryRemove(topic, out _);
            }
        }
    }

    /// <summary>获取匹配主题的保留消息</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <returns></returns>
    public IList<PublishMessage> GetRetainMessages(String topicFilter)
    {
        // $SYS 内部主题不对外提供保留消息查询，避免污染用户数据
        if (topicFilter.StartsWith("$SYS/")) return [];

        var list = new List<PublishMessage>();
        foreach (var item in _retainMessages)
        {
            // 跳过 $SYS 内部主题
            if (item.Key.StartsWith("$SYS/")) continue;
            if (MqttTopicFilter.IsMatch(item.Key, topicFilter))
                list.Add(item.Value);
        }
        return list;
    }

    /// <summary>检查消息是否已过期（MessageExpiryInterval）。MQTT 5.0</summary>
    /// <param name="message">待检查消息</param>
    /// <returns>true 表示已过期，应丢弃；false 表示未过期或无过期设置</returns>
    private static Boolean IsMessageExpired(PublishMessage message)
    {
        if (message.ReceivedAt == default) return false;

        var expiry = message.Properties?.GetUInt32(MqttPropertyId.MessageExpiryInterval);
        if (expiry == null) return false;

        return (DateTime.Now - message.ReceivedAt).TotalSeconds > expiry.Value;
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

        // 维护反向索引，供 Publish 热路径 O(1) 定位离线会话
        _persistentSessionIds[sessionId] = clientId;

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

        // 更新会话标识与反向索引
        _persistentSessionIds[sessionId] = clientId;
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
        if (clientId.IsNullOrEmpty()) return;

        if (_persistentSessions.TryRemove(clientId, out var session))
            _persistentSessionIds.TryRemove(session.SessionId, out _);
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
    /// <summary>直接向指定客户端注入消息。无需回环客户端，适合服务端主动推送场景</summary>
    /// <param name="clientId">目标客户端标识</param>
    /// <param name="message">发布消息</param>
    /// <returns>是否找到目标客户端（客户端不在线时返回 false）</returns>
    public virtual Boolean InjectMessage(String clientId, PublishMessage message)
    {
        if (clientId.IsNullOrEmpty()) return false;
        if (message == null) throw new ArgumentNullException(nameof(message));

        // 通过 clientId 反查 sessionId
        foreach (var kv in _clientIdMap)
        {
            if (kv.Value == clientId && _sessions.TryGetValue(kv.Key, out var session))
            {
                session.PublishAsync(message);
                Interlocked.Increment(ref Stats.MessagesSent);
                Interlocked.Add(ref Stats.BytesSent, message.Payload?.Length ?? 0);
                return true;
            }
        }
        return false;
    }

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
        // $SYS 内部保留消息不计入用户保留消息统计
        Stats.RetainedMessages = _retainMessages.Count(e => !e.Key.StartsWith("$SYS/"));

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

            foreach (var sub in item.Value.Snapshot())
            {
                if (_sessions.TryGetValue(sub.Id, out var session))
                    session.PublishAsync(msg);
            }
        }
    }
    #endregion

    #region 管理查询
    /// <summary>获取持久会话信息（只读，不恢复订阅关系）。用于集群会话漂移 RPC 查询</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>持久会话；不存在时返回 null</returns>
    public PersistentSession? GetPersistentSessionInfo(String clientId) =>
        _persistentSessions.TryGetValue(clientId, out var session) ? session : null;

    /// <summary>删除持久会话（用于集群会话迁移后清理源节点数据）</summary>
    /// <param name="clientId">客户端标识</param>
    public void DeletePersistentSession(String clientId)
    {
        // 仅删除持久会话，不影响保留消息计数。保留消息数由 _retainMessages 字典驱动
        if (_persistentSessions.TryRemove(clientId, out var session))
            _persistentSessionIds.TryRemove(session.SessionId, out _);
    }

    /// <summary>按会话标识查找持久化会话。O(1) 索引查找，避免 Publish 热路径全量扫描</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns>持久化会话；不存在时返回 null</returns>
    private PersistentSession? GetPersistentSessionBySessionId(Int32 sessionId)
    {
        if (!_persistentSessionIds.TryGetValue(sessionId, out var clientId)) return null;
        if (clientId.IsNullOrEmpty()) return null;

        return _persistentSessions.TryGetValue(clientId, out var session) ? session : null;
    }

    /// <summary>获取已连接客户端 ID 列表</summary>
    public IReadOnlyList<String> GetConnectedClients()
    {
        var list = new List<String>(_clientIdMap.Count);
        foreach (var kv in _clientIdMap)
        {
            if (!kv.Value.IsNullOrEmpty())
                list.Add(kv.Value);
        }
        return list;
    }

    /// <summary>获取主题订阅统计。key=主题，value=订阅者数量</summary>
    public IReadOnlyDictionary<String, Int32> GetTopicSubscriptions()
    {
        var result = new Dictionary<String, Int32>(_topics.Count);
        foreach (var kv in _topics)
            result[kv.Key] = kv.Value.Count;
        return result;
    }

    /// <summary>获取保留消息的主题列表</summary>
    public IReadOnlyList<String> GetRetainedTopics() => _retainMessages.Keys.Where(e => !e.StartsWith("$SYS/")).ToArray();

    /// <summary>断开指定客户端连接。由管理 API 或集群调用</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="reason">断开原因</param>
    /// <returns>是否找到并断开了该客户端</returns>
    public Boolean DisconnectClient(String clientId, String? reason = null)
    {
        if (clientId.IsNullOrEmpty()) return false;

        var text = reason ?? "管理 API 断开";
        foreach (var kv in _clientIdMap)
        {
            if (kv.Value == clientId)
            {
                if (_sessions.TryGetValue(kv.Key, out var handler))
                {
                    // 断开的是连接本身：走会话标准关闭流程触发 OnDisconnected（含遗嘱发布与交换机注销）。
                    // 只调 handler.Close 是“连接已关闭”的通知语义——连接会留在原处，仅清掉交换机登记，
                    // 表现为客户端仍在线却收不到任何消息
                    var session = (handler as MqttHandler)?.Session;
                    if (session != null)
                        session.Close(text);
                    else
                        handler.Close(text);

                    Remove(kv.Key);
                    return true;
                }
            }
        }
        return false;
    }
    #endregion
}