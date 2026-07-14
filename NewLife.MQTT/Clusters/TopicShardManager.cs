using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NewLife.MQTT.Clusters;

/// <summary>一致哈希环。将物理节点通过虚拟节点映射到哈希环上，实现主题分片路由</summary>
/// <remarks>
/// 固定 1024 个分片，每物理节点 256 个虚拟节点。
/// 节点加入/离开时仅影响相邻虚拟节点区间的分片归属，最小化扰动。
/// </remarks>
public class ConsistentHashRing
{
    #region 内部结构
    /// <summary>虚拟节点条目</summary>
    private struct VirtualNode : IComparable<VirtualNode>
    {
        /// <summary>哈希位置（0 ~ 2^32-1）</summary>
        public UInt32 Hash;

        /// <summary>所属物理节点地址</summary>
        public String Endpoint;

        public Int32 CompareTo(VirtualNode other) => Hash.CompareTo(other.Hash);
    }
    #endregion

    #region 属性
    /// <summary>分片总数</summary>
    public Int32 ShardCount { get; } = 1024;

    /// <summary>每物理节点虚拟节点数</summary>
    public Int32 VirtualNodesPerNode { get; } = 256;

    /// <summary>每分片副本数</summary>
    public Int32 ReplicaCount { get; } = 3;

    /// <summary>有序虚拟节点列表（环形）</summary>
    private List<VirtualNode> _ring = [];

    /// <summary>物理节点 → 虚拟节点哈希值集合</summary>
    private ConcurrentDictionary<String, HashSet<UInt32>> _nodeVNodes = new();

    private readonly Object _lock = new();
    #endregion

    #region 构造
    /// <summary>创建一致哈希环</summary>
    /// <param name="shardCount">分片数，默认 1024</param>
    /// <param name="virtualNodesPerNode">每节点虚拟节点数，默认 256</param>
    /// <param name="replicaCount">副本数，默认 3</param>
    public ConsistentHashRing(Int32 shardCount = 1024, Int32 virtualNodesPerNode = 256, Int32 replicaCount = 3)
    {
        ShardCount = shardCount;
        VirtualNodesPerNode = virtualNodesPerNode;
        ReplicaCount = replicaCount;
    }
    #endregion

    #region 节点管理
    /// <summary>添加物理节点到哈希环</summary>
    /// <param name="endpoint">节点地址</param>
    public void AddNode(String endpoint)
    {
        lock (_lock)
        {
            if (_nodeVNodes.ContainsKey(endpoint)) return;

            var vnodes = new HashSet<UInt32>();
            for (var i = 0; i < VirtualNodesPerNode; i++)
            {
                // 虚拟节点命名：endpoint + ":" + 序号，使每个虚拟节点哈希到不同位置
                var vnodeKey = $"{endpoint}:{i}";
                var hash = HashString(vnodeKey);
                vnodes.Add(hash);
                _ring.Add(new VirtualNode { Hash = hash, Endpoint = endpoint });
            }
            _nodeVNodes[endpoint] = vnodes;

            // 保持有序排列
            _ring.Sort((a, b) => a.Hash.CompareTo(b.Hash));
        }
    }

    /// <summary>从哈希环移除物理节点</summary>
    /// <param name="endpoint">节点地址</param>
    public void RemoveNode(String endpoint)
    {
        lock (_lock)
        {
            if (!_nodeVNodes.TryRemove(endpoint, out _)) return;

            _ring.RemoveAll(v => v.Endpoint == endpoint);
        }
    }

    /// <summary>获取当前所有物理节点</summary>
    public String[] GetNodes()
    {
        lock (_lock)
        {
            return _nodeVNodes.Keys.ToArray();
        }
    }

    /// <summary>节点数量</summary>
    public Int32 NodeCount => _nodeVNodes.Count;
    #endregion

    #region 查询
    /// <summary>获取主题对应的分片索引</summary>
    /// <param name="topic">主题名</param>
    /// <returns>分片索引 (0 ~ ShardCount-1)</returns>
    public Int32 GetShardIndex(String topic)
    {
        var hash = HashString(topic);

        // 将哈希值映射到分片空间
        var shardIndex = (Int32)((UInt64)hash * (UInt64)ShardCount / ((UInt64)UInt32.MaxValue + 1));
        if (shardIndex < 0) shardIndex += ShardCount;
        if (shardIndex >= ShardCount) shardIndex = ShardCount - 1;

        return shardIndex;
    }

    /// <summary>获取分片对应的副本节点列表（按优先级排序）</summary>
    /// <param name="shardIndex">分片索引</param>
    /// <returns>负责该分片的节点地址列表（去重，最多 ReplicaCount 个）</returns>
    public List<String> GetShardNodes(Int32 shardIndex)
    {
        lock (_lock)
        {
            if (_ring.Count == 0) return [];

            // 分片中心点哈希值
            var shardHash = (UInt32)((UInt64)shardIndex * ((UInt64)UInt32.MaxValue + 1) / (UInt64)ShardCount);

            // 在环上找到分片中心点之后的虚拟节点
            var startIndex = FindFirstAfter(shardHash);

            var result = new List<String>();
            var seen = new HashSet<String>();

            // 顺时针收集副本节点
            for (var i = 0; i < _ring.Count && result.Count < ReplicaCount; i++)
            {
                var idx = (startIndex + i) % _ring.Count;
                var endpoint = _ring[idx].Endpoint;
                if (seen.Add(endpoint))
                    result.Add(endpoint);
            }

            return result;
        }
    }

    /// <summary>获取主题应转发到的目标节点列表</summary>
    /// <param name="topic">主题名</param>
    /// <returns>目标节点地址列表</returns>
    public List<String> GetTopicNodes(String topic)
    {
        var shardIndex = GetShardIndex(topic);
        return GetShardNodes(shardIndex);
    }
    #endregion

    #region 内部
    /// <summary>在有序环上查找第一个哈希值 >= target 的虚拟节点索引</summary>
    private Int32 FindFirstAfter(UInt32 target)
    {
        // 二分查找
        var lo = 0;
        var hi = _ring.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (_ring[mid].Hash >= target)
                hi = mid - 1;
            else
                lo = mid + 1;
        }

        // 环回绕到第一个
        if (lo >= _ring.Count) lo = 0;

        return lo;
    }

    /// <summary>计算字符串的 32 位哈希值</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 HashString(String value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return (UInt32)MurmurHash3.Hash32(bytes, 0);
    }
    #endregion
}
