using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Clusters;

/// <summary>集群交换机</summary>
/// <remarks>
/// 集群交换机主要用于维护主题订阅关系，以及消息转发
/// </remarks>
public class ClusterExchange : DisposeBase, ITracerFeature
{
    #region 属性
    /// <summary>集群服务端</summary>
    public ClusterServer Cluster { get; set; } = null!;

    /// <summary>主题订阅集合</summary>
    private ConcurrentDictionary<String, List<SubscriptionInfo>> _topics = new();

    /// <summary>布隆过滤器交换机。用于跨节点转发预检，减少无效RPC调用</summary>
    public BloomFilterExchange? BloomFilter { get; set; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 订阅关系
    /// <summary>获取所有订阅关系（用于布隆过滤器重建等场景）</summary>
    public ConcurrentDictionary<String, List<SubscriptionInfo>> GetAllSubscriptions() => _topics;
    /// <summary>订阅主题</summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    public virtual void Subscribe(MqttSession session, SubscribeMessage message, String? clientId = null)
    {
        var myEndpoint = Cluster.GetNodeInfo().EndPoint;
        var infos = message.Requests.Select(e => new SubscriptionInfo
        {
            Topic = e.TopicFilter,
            Endpoint = myEndpoint,
            RemoteEndpoint = session.Remote + "",
            ClientId = clientId,
            SessionId = session.ID,
        }).ToArray();

        // 集群订阅2，向其它节点广播订阅增量（仅发送本次变化的订阅，非全量）
        foreach (var item in Cluster.Nodes)
        {
            try
            {
                var node = item.Value;
                _ = node.Subscribe(infos);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    /// <summary>取消主题订阅</summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    public virtual void Unsubscribe(MqttSession session, UnsubscribeMessage message, String? clientId = null)
    {
        var myEndpoint = Cluster.GetNodeInfo().EndPoint;
        var infos = message.TopicFilters.Select(e => new SubscriptionInfo
        {
            Topic = e,
            Endpoint = myEndpoint,
            RemoteEndpoint = session.Remote + "",
            ClientId = clientId,
            SessionId = session.ID,
        }).ToArray();

        // 集群退订2，向其它节点广播取消订阅关系。顺序发送避免并发撑爆RPC队列
        foreach (var item in Cluster.Nodes)
        {
            try
            {
                var node = item.Value;
                _ = node.Unsubscribe(infos);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    /// <summary>添加远程订阅关系</summary>
    /// <param name="info"></param>
    public void AddSubscription(SubscriptionInfo info)
    {
        // 集群订阅4，保存订阅关系
        var subs = _topics.GetOrAdd(info.Topic, k => []);
        lock (subs)
        {
            if (!subs.Any(e => e.Endpoint == info.Endpoint)) subs.Add(info);
        }

        // 增量更新布隆过滤器：远程节点 info.Endpoint 现在有订阅 info.Topic
        BloomFilter?.OnSubscriptionChanged(info.Topic, info.Endpoint, true);
    }

    /// <summary>移除远程订阅关系</summary>
    /// <param name="info"></param>
    public void RemoveSubscription(SubscriptionInfo info)
    {
        // 集群退订4，删除订阅关系
        if (_topics.TryGetValue(info.Topic, out var subs))
        {
            lock (subs)
            {
                subs.RemoveAll(e => e.Endpoint == info.Endpoint);
            }

            // 没有订阅者了，删除主题
            if (subs.Count == 0) _topics.TryRemove(info.Topic, out _);
        }
    }

    /// <summary>获取所有主题订阅（供Raft快照使用）</summary>
    public ConcurrentDictionary<String, List<SubscriptionInfo>> GetAllTopics() => _topics;

    /// <summary>替换所有主题订阅（供Raft快照恢复使用）</summary>
    public void ReplaceAllTopics(Dictionary<String, List<SubscriptionInfo>> topics)
    {
        _topics = new ConcurrentDictionary<String, List<SubscriptionInfo>>(topics);
    }
    #endregion

    #region 转发消息
    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息。
    /// 增加环路检测：TTL（默认3）耗尽丢弃，NodePath 包含本节点则跳过。
    /// </remarks>
    /// <param name="session"></param>
    /// <param name="message"></param>
    /// <param name="ttl">生存跳数，每转发一次减1。默认3</param>
    /// <param name="nodePath">已访问节点路径</param>
    public virtual void Publish(MqttSession session, PublishMessage message, Int32 ttl = 3, List<String>? nodePath = null)
    {
        // TTL 耗尽，丢弃消息防止无限循环
        if (ttl <= 0) return;

        var myEndpoint = Cluster.GetNodeInfo().EndPoint;

        // 初始化或追加本节点到路径中
        nodePath ??= [];
        nodePath.Add(myEndpoint);

        PublishInfo? info = null;

        // 集群发布2，查找其它节点订阅关系，然后转发消息
        // 先通过布隆过滤器快速跳过无匹配的节点，再遍历所有Topic精确匹配
        var bloomFilter = BloomFilter;

        foreach (var item in _topics)
        {
            if (!MqttTopicFilter.IsMatch(message.Topic, item.Key)) continue;

            // 遍历所有订阅者
            var subs = item.Value;
            foreach (var sub in subs.ToArray())
            {
                if (Cluster.Nodes.TryGetValue(sub.Endpoint, out var node))
                {
                    // 环路检测：如果目标节点已在转发路径中，跳过
                    if (nodePath.Contains(sub.Endpoint)) continue;

                    // 布隆过滤器预检：快速跳过确定无匹配订阅的节点
                    if (bloomFilter != null && !bloomFilter.MightContain(message.Topic, sub.Endpoint)) continue;

                    info ??= new PublishInfo
                    {
                        Message = message,
                        RemoteEndpoint = session.Remote + "",
                        TTL = ttl - 1,
                        NodePath = [.. nodePath],
                    };

                    // 优先使用批量合并转发管道，小消息合并为大请求
                    var batchPipe = Cluster.BatchPipe;
                    if (batchPipe != null)
                        batchPipe.Enqueue(node, info);
                    else
                        _ = node.Publish(info);

                    // 记录转发统计
                    Cluster.IncrementMessageCount(message.Payload?.Length ?? 0);
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
    #endregion
}
