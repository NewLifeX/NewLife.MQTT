using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;

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

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 订阅关系
    /// <summary>订阅主题</summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    public virtual void Subscribe(MqttSession session, SubscribeMessage message)
    {
        var myEndpoint = Cluster.GetNodeInfo().EndPoint;
        var infos = message.Requests.Select(e => new SubscriptionInfo
        {
            Topic = e.TopicFilter,
            Endpoint = myEndpoint,
            RemoteEndpoint = session.Remote + "",
        }).ToArray();

        // 集群订阅2，向其它节点广播订阅关系
        Parallel.ForEach(Cluster.Nodes, item =>
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
        });
    }

    /// <summary>取消主题订阅</summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    public virtual void Unsubscribe(MqttSession session, UnsubscribeMessage message)
    {
        var myEndpoint = Cluster.GetNodeInfo().EndPoint;
        var infos = message.TopicFilters.Select(e => new SubscriptionInfo
        {
            Topic = e,
            Endpoint = myEndpoint,
            RemoteEndpoint = session.Remote + "",
        }).ToArray();

        // 集群退订2，向其它节点广播取消订阅关系
        Parallel.ForEach(Cluster.Nodes, item =>
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
        });
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
    #endregion

    #region 转发消息
    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息
    /// </remarks>
    /// <param name="session"></param>
    /// <param name="message"></param>
    public virtual void Publish(MqttSession session, PublishMessage message)
    {
        PublishInfo? info = null;

        // 集群发布2，查找其它节点订阅关系，然后转发消息
        // 遍历所有Topic，找到匹配的订阅者
        foreach (var item in _topics)
        {
            if (!MqttTopicFilter.IsMatch(message.Topic, item.Key)) continue;

            // 遍历所有订阅者
            var subs = item.Value;
            foreach (var sub in subs.ToArray())
            {
                if (Cluster.Nodes.TryGetValue(sub.Endpoint, out var node))
                {
                    info ??= new PublishInfo
                    {
                        Message = message,
                        RemoteEndpoint = session.Remote + "",
                    };

                    _ = node.Publish(info);
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
