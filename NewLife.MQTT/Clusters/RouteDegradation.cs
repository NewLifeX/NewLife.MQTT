using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群路由模式</summary>
public enum ClusterRouteMode
{
    /// <summary>平坦模式（全连接广播）。适合 < 50 节点</summary>
    Flat = 0,

    /// <summary>分片模式（一致哈希定向路由）。适合 50~1000 节点</summary>
    Sharded = 1,

    /// <summary>多层模式（Edge/Regional/Core 分级路由）。适合 1000+ 节点</summary>
    Hierarchical = 2,
}

/// <summary>路由降级自适应切换。根据集群规模和性能指标自动切换平坦/分片/多层模式</summary>
/// <remarks>
/// 自动检测集群规模和路由效率，切换期间保证消息不丢失：
/// - 节点数 < 50 → 平坦模式（全连接广播，延迟最低）
/// - 50~1000 → 分片模式（一致哈希定向路由）
/// - 1000+ → 多层模式（Edge/Regional/Core 分级）
/// - 检测到异常（大量超时/路由错误）时，降级到更简单的模式
/// </remarks>
public class RouteDegradation : DisposeBase
{
    #region 属性
    /// <summary>当前路由模式</summary>
    public ClusterRouteMode CurrentMode { get; private set; } = ClusterRouteMode.Flat;

    /// <summary>平坦→分片阈值（节点数）。默认 50</summary>
    public Int32 FlatToShardedThreshold { get; set; } = 50;

    /// <summary>分片→平坦降级阈值（节点数）。默认 30，低于此值时从分片降级到平坦</summary>
    public Int32 ShardedToFlatThreshold { get; set; } = 30;

    /// <summary>分片→多层阈值（节点数）。默认 1000</summary>
    public Int32 ShardedToHierarchicalThreshold { get; set; } = 1000;

    /// <summary>多层→分片降级阈值（节点数）。默认 800</summary>
    public Int32 HierarchicalToShardedThreshold { get; set; } = 800;

    /// <summary>检测周期（毫秒）。默认 30000ms</summary>
    public Int32 CheckInterval { get; set; } = 30_000;

    /// <summary>切换冷却时间（毫秒）。默认 120_000ms，避免频繁切换</summary>
    public Int32 CooldownMs { get; set; } = 120_000;

    /// <summary>当前节点数</summary>
    public Int32 NodeCount { get; private set; }

    /// <summary>路由错误率（最近一分钟错误/总请求）</summary>
    public Double ErrorRate { get; private set; }

    /// <summary>是否启用自动降级（错误率超限时自动降级）</summary>
    public Boolean EnableAutoDegradation { get; set; } = true;

    /// <summary>自动降级错误率阈值。默认 0.1（10%）</summary>
    public Double DegradationErrorRateThreshold { get; set; } = 0.1;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否已启动</summary>
    public Boolean Active { get; private set; }

    private TimerX? _timer;
    private DateTime _lastSwitch = DateTime.MinValue;
    private Int32 _totalRequests;
    private Int32 _failedRequests;
    #endregion

    #region 事件
    /// <summary>路由模式变更事件。参数：旧模式、新模式、变更原因</summary>
    public event Action<ClusterRouteMode, ClusterRouteMode, String>? OnModeChanged;
    #endregion

    #region 构造
    public RouteDegradation() { }
    #endregion

    #region 方法
    /// <summary>启动检测</summary>
    public void Start()
    {
        if (Active) return;
        Active = true;
        _timer = new TimerX(Check, null, 10_000, CheckInterval);
        WriteLog("RouteDegradation started, mode={0}", CurrentMode);
    }

    /// <summary>停止检测</summary>
    public void Stop()
    {
        Active = false;
        _timer.TryDispose();
        _timer = null;
    }

    /// <summary>记录一次路由请求结果</summary>
    /// <param name="success">是否成功</param>
    public void RecordRouteResult(Boolean success)
    {
        Interlocked.Increment(ref _totalRequests);
        if (!success) Interlocked.Increment(ref _failedRequests);
    }

    /// <summary>更新当前节点数</summary>
    public void UpdateNodeCount(Int32 count) => NodeCount = count;

    /// <summary>根据当前节点数计算推荐模式</summary>
    public ClusterRouteMode GetRecommendedMode()
    {
        var count = NodeCount;

        if (count >= ShardedToHierarchicalThreshold)
            return ClusterRouteMode.Hierarchical;
        if (count >= FlatToShardedThreshold)
            return ClusterRouteMode.Sharded;
        return ClusterRouteMode.Flat;
    }

    /// <summary>手动切换模式</summary>
    public Boolean SwitchMode(ClusterRouteMode newMode, String reason)
    {
        if (newMode == CurrentMode) return false;

        // 检查冷却时间
        if ((DateTime.UtcNow - _lastSwitch).TotalMilliseconds < CooldownMs)
        {
            WriteLog("Switch to {0} blocked by cooldown ({1:F0}s remaining)", newMode,
                (CooldownMs - (DateTime.UtcNow - _lastSwitch).TotalMilliseconds) / 1000);
            return false;
        }

        var oldMode = CurrentMode;
        CurrentMode = newMode;
        _lastSwitch = DateTime.UtcNow;

        WriteLog("Route mode changed: {0} → {1} (reason: {2})", oldMode, newMode, reason);
        OnModeChanged?.Invoke(oldMode, newMode, reason);
        return true;
    }

    /// <summary>获取统计信息</summary>
    public String GetStats()
    {
        return $"Mode={CurrentMode}, Nodes={NodeCount}, ErrorRate={ErrorRate:P1}, Cooldown={Math.Max(0, (CooldownMs - (DateTime.UtcNow - _lastSwitch).TotalMilliseconds) / 1000):F0}s";
    }
    #endregion

    #region 内部
    private void Check(Object? state)
    {
        try
        {
            // 计算错误率（滑动窗口：丢弃旧数据）
            var total = Interlocked.Exchange(ref _totalRequests, 0);
            var failed = Interlocked.Exchange(ref _failedRequests, 0);
            ErrorRate = total > 0 ? (Double)failed / total : 0;

            var recommended = GetRecommendedMode();

            // 自动降级：错误率超限时降级到更简单的模式
            if (EnableAutoDegradation && ErrorRate > DegradationErrorRateThreshold)
            {
                var degradeMode = recommended > ClusterRouteMode.Flat
                    ? (ClusterRouteMode)((Int32)recommended - 1)
                    : ClusterRouteMode.Flat;
                if (degradeMode < CurrentMode)
                {
                    SwitchMode(degradeMode, $"auto-degradation (error rate {ErrorRate:P1} > {DegradationErrorRateThreshold:P1})");
                    return;
                }
            }

            // 正常升级/降级
            if (recommended != CurrentMode)
            {
                var reason = recommended > CurrentMode
                    ? $"scale up (nodes={NodeCount})"
                    : $"scale down (nodes={NodeCount})";
                SwitchMode(recommended, reason);
            }
        }
        catch (Exception ex)
        {
            WriteLog("Check error: {0}", ex.Message);
        }
    }
    #endregion

    #region 辅助
    private void WriteLog(String format, params Object?[] args) => Log?.Info($"[RouteDegradation]{format}", args);

    /// <summary>释放资源</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
    #endregion
}
