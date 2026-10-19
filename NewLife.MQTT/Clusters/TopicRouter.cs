using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NewLife.Log;

namespace NewLife.MQTT.Clusters;

/// <summary>分片感知路由表。基于一致哈希环，将主题映射到分片→节点，提供定向转发路由</summary>
/// <remarks>
/// 维护主题→分片→节点列表的查询缓存，节点变更时自动失效。
/// 与 BloomFilterExchange 配合使用：分片路由确定候选节点，布隆过滤器进一步过滤。
/// </remarks>
public class TopicRouter
{
    #region 属性
    /// <summary>一致哈希环</summary>
    public ConsistentHashRing HashRing { get; }

    /// <summary>集群服务引用</summary>
    public ClusterServer? Cluster { get; set; }

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; } = true;

    /// <summary>主题→分片索引缓存</summary>
    private ConcurrentDictionary<String, Int32> _shardCache = new();

    /// <summary>分片索引→节点列表缓存</summary>
    private ConcurrentDictionary<Int32, List<String>> _nodeCache = new();

    /// <summary>缓存命中计数</summary>
    private Int64 _cacheHits;

    /// <summary>缓存未命中计数</summary>
    private Int64 _cacheMisses;
    #endregion

    #region 构造
    /// <summary>创建分片路由表</summary>
    public TopicRouter(ConsistentHashRing hashRing)
    {
        HashRing = hashRing ?? throw new ArgumentNullException(nameof(hashRing));
    }

    /// <summary>创建分片路由表（使用默认参数）</summary>
    public TopicRouter() : this(new ConsistentHashRing()) { }
    #endregion

    #region 路由查询
    /// <summary>获取主题应转发到的目标节点列表</summary>
    /// <param name="topic">主题名</param>
    /// <returns>目标节点地址列表（按优先级排序，最多 ReplicaCount 个）</returns>
    public List<String> GetTargetNodes(String topic)
    {
        if (!Enable || HashRing.NodeCount == 0) return [];

        // 查缓存
        if (_shardCache.TryGetValue(topic, out var shardIndex))
        {
            Interlocked.Increment(ref _cacheHits);

            if (_nodeCache.TryGetValue(shardIndex, out var cachedNodes))
                return cachedNodes;
        }
        else
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        // 计算分片
        shardIndex = HashRing.GetShardIndex(topic);
        _shardCache[topic] = shardIndex;

        // 获取节点列表
        var nodes = HashRing.GetShardNodes(shardIndex);
        _nodeCache[shardIndex] = nodes;

        return nodes;
    }

    /// <summary>获取主题对应的分片索引</summary>
    /// <param name="topic">主题名</param>
    public Int32 GetShardIndex(String topic)
    {
        if (_shardCache.TryGetValue(topic, out var shardIndex))
            return shardIndex;

        shardIndex = HashRing.GetShardIndex(topic);
        _shardCache[topic] = shardIndex;

        return shardIndex;
    }

    /// <summary>检查指定节点是否是该主题的路由目标</summary>
    /// <param name="topic">主题名</param>
    /// <param name="endpoint">节点地址</param>
    /// <returns>true 表示该节点是该主题的优先路由目标</returns>
    public Boolean IsTargetNode(String topic, String endpoint)
    {
        var nodes = GetTargetNodes(topic);
        return nodes.Count > 0 && nodes.Contains(endpoint);
    }
    #endregion

    #region 节点变更
    /// <summary>节点加入时更新路由表</summary>
    /// <param name="endpoint">新节点地址</param>
    public void OnNodeJoined(String endpoint)
    {
        HashRing.AddNode(endpoint);
        InvalidateNodeCache();
        WriteLog("Node added to the routing table: {0}; now {1} nodes and {2} virtual nodes", endpoint, HashRing.NodeCount, HashRing.VirtualNodesPerNode);
    }

    /// <summary>节点离开时更新路由表</summary>
    /// <param name="endpoint">离开节点地址</param>
    public void OnNodeLeft(String endpoint)
    {
        HashRing.RemoveNode(endpoint);
        InvalidateNodeCache();
        WriteLog("Node removed from the routing table: {0}; {1} nodes remaining", endpoint, HashRing.NodeCount);
    }

    /// <summary>全量同步集群节点到路由表</summary>
    public void SyncNodes()
    {
        if (Cluster == null) return;

        var currentNodes = new HashSet<String>(HashRing.GetNodes());
        var clusterNodes = new HashSet<String>(Cluster.Nodes.Keys);

        // 加入新节点
        foreach (var endpoint in clusterNodes)
        {
            if (!currentNodes.Contains(endpoint))
                HashRing.AddNode(endpoint);
        }

        // 移除离线节点
        foreach (var endpoint in currentNodes)
        {
            if (!clusterNodes.Contains(endpoint))
                HashRing.RemoveNode(endpoint);
        }

        InvalidateNodeCache();
    }

    /// <summary>清除所有路由缓存</summary>
    public void InvalidateCache()
    {
        _shardCache.Clear();
        _nodeCache.Clear();
    }

    private void InvalidateNodeCache()
    {
        _nodeCache.Clear();
    }
    #endregion

    #region 统计
    /// <summary>获取缓存命中率</summary>
    public Double CacheHitRate
    {
        get
        {
            var total = _cacheHits + _cacheMisses;
            return total > 0 ? (Double)_cacheHits / total : 0;
        }
    }

    /// <summary>获取统计信息</summary>
    public String GetStats()
    {
        return $"Nodes={HashRing.NodeCount}, cache hits={_cacheHits}, misses={_cacheMisses}, hit rate={CacheHitRate:P1}";
    }
    #endregion

    #region 日志
    private void WriteLog(String format, params Object?[] args) => XTrace.Log.InfoPrefixed("[TopicRouter] ", format, args);
    #endregion
}
