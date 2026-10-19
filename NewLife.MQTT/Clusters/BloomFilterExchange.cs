using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>布隆过滤器交换机。为每个远程节点维护一个布隆过滤器，加速跨节点消息转发预检</summary>
/// <remarks>
/// 每个远程节点的布隆过滤器包含该节点上所有客户端的订阅主题过滤器。
/// 转发消息前先查布隆过滤器：命中才进入详细 topic 匹配，大幅减少无效遍历。
/// 默认 60 秒周期性全量重建，1% 误判率。
/// </remarks>
public class BloomFilterExchange
{
    #region 属性
    /// <summary>集群服务引用</summary>
    public ClusterServer? Cluster { get; set; }

    /// <summary>每个远程节点的布隆过滤器</summary>
    private ConcurrentDictionary<String, BloomFilter> _nodeFilters = new();

    /// <summary>重建周期（秒），默认 60s</summary>
    public Int32 RebuildInterval { get; set; } = 60;

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; } = true;

    private TimerX? _rebuildTimer;
    #endregion

    #region 构造
    /// <summary>启动布隆过滤器交换</summary>
    public void Start()
    {
        if (_rebuildTimer != null) return;

        _rebuildTimer = new TimerX(DoRebuild, null, RebuildInterval * 1000, RebuildInterval * 1000)
        {
            Async = true
        };

        WriteLog("BloomFilterExchange started; rebuild interval {0} s", RebuildInterval);
    }

    /// <summary>停止布隆过滤器交换</summary>
    public void Stop()
    {
        _rebuildTimer?.Dispose();
        _rebuildTimer = null;

        _nodeFilters.Clear();
        WriteLog("BloomFilterExchange stopped");
    }
    #endregion

    #region 方法
    /// <summary>订阅变更时同步更新布隆过滤器（增量更新）</summary>
    /// <param name="topicFilter">订阅主题过滤器</param>
    /// <param name="endpoint">节点地址</param>
    /// <param name="isSubscribe">true=订阅，false=退订</param>
    public void OnSubscriptionChanged(String topicFilter, String endpoint, Boolean isSubscribe)
    {
        if (!Enable) return;

        if (_nodeFilters.TryGetValue(endpoint, out var filter))
        {
            if (isSubscribe)
            {
                filter.Add(EncodeTopic(topicFilter));
            }
            // 退订不做删除（布隆过滤器不支持删除），定期重建时自然清理
        }
    }

    /// <summary>检查指定节点是否可能存在匹配的订阅</summary>
    /// <param name="topic">发布主题</param>
    /// <param name="endpoint">目标节点地址</param>
    /// <returns>true 表示可能存在匹配（需进一步精确匹配），false 表示一定不存在</returns>
    public Boolean MightContain(String topic, String endpoint)
    {
        if (!Enable) return true; // 未启用时不过滤，保持原有行为

        if (!_nodeFilters.TryGetValue(endpoint, out var filter)) return true;

        return filter.Contains(EncodeTopic(topic));
    }

    /// <summary>获取所有已知节点的布隆过滤器信息</summary>
    public ConcurrentDictionary<String, BloomFilter> GetNodeFilters() => _nodeFilters;
    #endregion

    #region 重建
    private void DoRebuild(Object? state)
    {
        if (Cluster == null) return;

        using var span = Tracer?.NewSpan("BloomFilterRebuild");

        try
        {
            // 收集每个节点的所有订阅主题过滤器
            var exchange = Cluster.Exchange as ClusterExchange;
            if (exchange == null) return;

            var topics = exchange.GetAllSubscriptions();

            // 按节点分组：endpoint → topic filters
            var nodeTopics = new Dictionary<String, List<String>>();
            foreach (var kv in topics)
            {
                var topicFilter = kv.Key;
                foreach (var sub in kv.Value)
                {
                    if (!nodeTopics.TryGetValue(sub.Endpoint, out var list))
                    {
                        list = new List<String>();
                        nodeTopics[sub.Endpoint] = list;
                    }
                    list.Add(topicFilter);
                }
            }

            // 为每个节点重建布隆过滤器
            var newFilters = new ConcurrentDictionary<String, BloomFilter>();
            foreach (var kv in nodeTopics)
            {
                var endpoint = kv.Key;
                var topicList = kv.Value;
                var expectedCount = Math.Max(topicList.Count, 100);
                var filter = new BloomFilter(expectedCount);
                foreach (var tf in topicList)
                {
                    filter.Add(EncodeTopic(tf));
                }
                newFilters[endpoint] = filter;
            }

            // 原子替换
            var old = Interlocked.Exchange(ref _nodeFilters, newFilters);

            WriteLog("BloomFilter rebuild completed; nodes={0}, total topics={1}", newFilters.Count, nodeTopics.Values.Sum(v => v.Count));
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }
    #endregion

    #region 辅助
    /// <summary>编码主题为字节序列（用于布隆过滤器哈希输入）</summary>
    private static ReadOnlySpan<Byte> EncodeTopic(String topic)
    {
        return System.Text.Encoding.UTF8.GetBytes(topic);
    }
    #endregion

    #region 日志
    private ILog Log => XTrace.Log;
    private ITracer? Tracer => Cluster?.Tracer;

    /// <summary>写日志</summary>
    private void WriteLog(String format, params Object?[] args) => Log.InfoPrefixed("[BloomFilter] ", format, args);
    #endregion
}
