using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>订阅增量裁剪定向推送。将订阅变更按分片定向推送到目标节点子集，而非全集群广播</summary>
/// <remarks>
/// 传统的订阅同步方式是将变更广播到所有节点。在规模较大时，每个节点只持有 1/N 的分片，
/// 全量广播造成大量浪费。
/// 
/// 本组件利用 TopicRouter 的 shard 映射将订阅变更定向推送到持有目标分片的节点子集，
/// 网络流量减少为原来的 1/ShardCount（如 1/1024）。
/// </remarks>
public class SubscriptionDeltaSync : DisposeBase
{
    #region 属性
    /// <summary>主题路由表（用于获取主题→分片→节点映射）</summary>
    public TopicRouter? Router { get; set; }

    /// <summary>批量确认窗口大小（条数）。默认 100</summary>
    public Int32 BatchAckSize { get; set; } = 100;

    /// <summary>批量确认窗口（毫秒）。默认 100ms</summary>
    public Int32 BatchAckWindowMs { get; set; } = 100;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>统计</summary>
    public Int32 TotalSubscriptionsPushed { get; private set; }
    public Int32 TotalBroadcasts { get; private set; }
    public Int32 TotalDeltaPushes { get; private set; }

    /// <summary>是否启用定向推送。关闭时回退到全量广播</summary>
    public Boolean EnableDeltaPush { get; set; } = true;
    #endregion

    #region 内部
    /// <summary>待批量 ACK 的队列（节点→待确认条目列表）</summary>
    private readonly ConcurrentDictionary<String, List<PendingAck>> _pendingAcks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>待批量 ACK 的条目</summary>
    public struct PendingAck
    {
        public String TopicFilter;
        public Boolean IsSubscribe;
        public Int64 Timestamp;
    }
    #endregion

    #region 方法
    /// <summary>获取订阅变更的目标节点。返回应该接收此变更的节点列表</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <param name="allNodes">集群所有节点</param>
    /// <returns>目标节点列表（定向推送）或全量节点（广播模式）</returns>
    public List<String> GetTargetNodes(String topicFilter, List<String> allNodes)
    {
        if (!EnableDeltaPush || Router == null || allNodes.Count == 0)
        {
            TotalBroadcasts++;
            return allNodes;
        }

        // 通过路由表获取该主题的目标节点（按分片定向）
        var targetNodes = Router.GetTargetNodes(topicFilter);
        if (targetNodes == null || targetNodes.Count == 0)
        {
            TotalBroadcasts++;
            return allNodes;
        }

        // 只保留在 allNodes 中的节点（过滤已下线的）
        var result = targetNodes.Where(t => allNodes.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        if (result.Count == 0)
        {
            TotalBroadcasts++;
            return allNodes;
        }

        TotalDeltaPushes++;
        return result;
    }

    /// <summary>记录一次订阅推送（用于统计和批量 ACK）</summary>
    public void RecordSubscriptionPush(String targetNode, String topicFilter, Boolean isSubscribe)
    {
        TotalSubscriptionsPushed++;

        // 加入批量 ACK 队列
        var list = _pendingAcks.GetOrAdd(targetNode, _ => []);
        lock (list)
        {
            list.Add(new PendingAck { TopicFilter = topicFilter, IsSubscribe = isSubscribe, Timestamp = Runtime.TickCount64 });
        }
    }

    /// <summary>获取待 ACK 的条目并清除</summary>
    public List<PendingAck>? GetAndClearPendingAcks(String node)
    {
        if (_pendingAcks.TryRemove(node, out var list))
            return list;
        return null;
    }

    /// <summary>获取统计信息</summary>
    public String GetStats()
    {
        var total = TotalDeltaPushes + TotalBroadcasts;
        var deltaRatio = total > 0 ? (Double)TotalDeltaPushes / total : 0;
        return $"Pushed={TotalSubscriptionsPushed}, Delta={TotalDeltaPushes}, Broadcast={TotalBroadcasts}, DeltaRatio={deltaRatio:P1}";
    }
    #endregion

    /// <summary>释放资源</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _pendingAcks.Clear();
    }
}
