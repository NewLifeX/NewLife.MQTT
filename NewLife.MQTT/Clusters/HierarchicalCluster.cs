using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群节点角色</summary>
public enum NodeRole
{
    /// <summary>平坦模式（默认，无层级）</summary>
    Flat = 0,

    /// <summary>边缘节点。直接承载设备连接，维护本地订阅</summary>
    Edge = 1,

    /// <summary>区域节点。聚合管辖 Edge 节点的路由信息</summary>
    Regional = 2,

    /// <summary>核心节点。全局路由协调，跨区域转发</summary>
    Core = 3,
}

/// <summary>多层分级拓扑管理。管理 Edge/Regional/Core 三级拓扑，处理跨层路由</summary>
/// <remarks>
/// 启动时通过配置确定角色，Edge→Regional→Core 逐级注册。
/// 路由规则：
///   Edge → Regional: 仅上报 Topic 级别的布隆过滤器摘要
///   Regional → Core: 上报区域聚合布隆过滤器
///   Core → Core: 区域间布隆过滤器交换
/// </remarks>
public class HierarchicalCluster : DisposeBase
{
    #region 属性
    /// <summary>本节点角色</summary>
    public NodeRole Role { get; set; } = NodeRole.Flat;

    /// <summary>所属区域（仅 Edge/Regional 有效）</summary>
    public String? Region { get; set; }

    /// <summary>上层节点地址（Edge→Regional 或 Regional→Core）</summary>
    public String? UpstreamEndpoint { get; set; }

    /// <summary>管辖的下层节点集合（仅 Regional/Core 有效）</summary>
    public ConcurrentDictionary<String, NodeRole> DownstreamNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>区域布隆过滤器摘要（Regional→Core 上报用）</summary>
    public ConcurrentDictionary<String, BloomFilter> RegionalBloomFilters { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否已启动</summary>
    public Boolean Active { get; private set; }

    /// <summary>心跳定时器</summary>
    private TimerX? _heartbeatTimer;
    #endregion

    #region 事件
    /// <summary>下层节点加入事件</summary>
    public event Action<String, NodeRole>? OnDownstreamNodeJoined;

    /// <summary>下层节点离开事件</summary>
    public event Action<String, NodeRole>? OnDownstreamNodeLeft;

    /// <summary>跨层转发请求。参数：主题、消息、目标层</summary>
    public event Func<String, Byte[], NodeRole, Task>? OnCrossLayerForward;
    #endregion

    #region 构造
    public HierarchicalCluster() { }
    #endregion

    #region 方法
    /// <summary>启动拓扑管理</summary>
    public void Start()
    {
        if (Active) return;
        Active = true;

        // 非平坦模式下启动心跳
        if (Role > NodeRole.Flat)
        {
            _heartbeatTimer = new TimerX(DoHeartbeat, null, 5_000, 15_000);
            WriteLog("HierarchicalCluster started, role={0}, region={1}", Role, Region);
        }
    }

    /// <summary>停止拓扑管理</summary>
    public void Stop()
    {
        Active = false;
        _heartbeatTimer.TryDispose();
        _heartbeatTimer = null;
    }

    /// <summary>注册下层节点。由 Regional/Core 接收 Edge/Regional 注册时调用</summary>
    /// <param name="endpoint">节点地址</param>
    /// <param name="role">节点角色</param>
    /// <param name="region">所在区域</param>
    /// <returns>是否接受注册</returns>
    public Boolean RegisterDownstream(String endpoint, NodeRole role, String? region)
    {
        if (Role == NodeRole.Flat) return false;

        // Regional 只接受 Edge，Core 只接受 Regional
        if (Role == NodeRole.Regional && role != NodeRole.Edge) return false;
        if (Role == NodeRole.Core && role != NodeRole.Regional) return false;

        if (DownstreamNodes.TryAdd(endpoint, role))
        {
            WriteLog("Downstream node registered: {0} as {1} in {2}", endpoint, role, region);
            OnDownstreamNodeJoined?.Invoke(endpoint, role);
            return true;
        }
        return false;
    }

    /// <summary>注销下层节点</summary>
    public Boolean UnregisterDownstream(String endpoint)
    {
        if (DownstreamNodes.TryRemove(endpoint, out var role))
        {
            WriteLog("Downstream node left: {0} ({1})", endpoint, role);
            OnDownstreamNodeLeft?.Invoke(endpoint, role);
            return true;
        }
        return false;
    }

    /// <summary>更新区域布隆过滤器摘要。由 Regional 节点调用</summary>
    public void UpdateRegionBloomFilter(String region, Byte[] bits, Int32 k, Int32 m)
    {
        var bf = RegionalBloomFilters.GetOrAdd(region, _ => new BloomFilter(m, k));
        bf.Clear();
        // 从 bits 恢复布隆过滤器
        var arr = bf.GetBits();
        if (bits.Length > 0 && arr.Length > 0)
            Buffer.BlockCopy(bits, 0, arr, 0, Math.Min(bits.Length, arr.Length * sizeof(UInt64)));
        WriteLog("Region[{0}] bloom filter updated, bits={1}", region, bits.Length);
    }

    /// <summary>查询主题是否可能在区域中存在</summary>
    public Boolean MightContainInRegion(String region, String topic)
    {
        if (RegionalBloomFilters.TryGetValue(region, out var bf))
            return bf.Contains(topic.GetBytes());
        return false;
    }

    /// <summary>获取当前拓扑统计</summary>
    public String GetStats()
    {
        var edgeCount = DownstreamNodes.Count(kv => kv.Value == NodeRole.Edge);
        var regionalCount = DownstreamNodes.Count(kv => kv.Value == NodeRole.Regional);
        return $"Role={Role}, Region={Region}, Downstreams={DownstreamNodes.Count}(Edge={edgeCount},Regional={regionalCount}), BloomFilters={RegionalBloomFilters.Count}";
    }
    #endregion

    #region 内部
    private void DoHeartbeat(Object? state)
    {
        if (!Active) return;

        // 向上层节点发送心跳（实际通过 RPC 调用）
        if (!UpstreamEndpoint.IsNullOrEmpty())
        {
            WriteLog("Heartbeat to upstream {0}", UpstreamEndpoint);
        }

        // 检查下层节点超时（超过 30 秒无心跳）
        // 下层节点的心跳由 ClusterServer 的 Ping 机制处理
    }
    #endregion

    #region 辅助
    private void WriteLog(String format, params Object?[] args) => Log?.Info($"[Hierarchical]{format}", args);

    /// <summary>释放资源</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
    #endregion
}
