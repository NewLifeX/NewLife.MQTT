using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>热点主题检测与分级</summary>
/// <remarks>
/// 滑动窗口统计各主题发布频率，按冷/温/热/极热四级分级，触发副本因子动态调整事件。
/// 
/// 使用方式：
/// <code>
/// var detector = new HotTopicDetector
/// {
///     WindowSeconds = 10,
///     Log = XTrace.Log,
/// };
/// detector.OnLevelChanged += (topic, oldLevel, newLevel, suggestedReplicas) =>
/// {
///     // 调整主题的分片副本数
/// };
/// detector.Start();
/// 
/// // 每次收到消息时记录
/// detector.RecordPublish(topic);
/// </code>
/// </remarks>
public class HotTopicDetector : DisposeBase
{
    #region 属性
    /// <summary>滑动窗口大小（秒）。默认 10 秒</summary>
    public Int32 WindowSeconds { get; set; } = 10;

    /// <summary>检测周期（毫秒）。默认 10000ms</summary>
    public Int32 CheckInterval { get; set; } = 10_000;

    /// <summary>降级确认时间（毫秒）。持续低负载超过此时间才降级，默认 300 秒</summary>
    public Int32 DegradeConfirmMs { get; set; } = 300_000;

    /// <summary>冷→温阈值（发布次数/秒）。默认 100</summary>
    public Double ColdThreshold { get; set; } = 100;

    /// <summary>温→热阈值（发布次数/秒）。默认 10_000</summary>
    public Double WarmThreshold { get; set; } = 10_000;

    /// <summary>热→极热阈值（发布次数/秒）。默认 100_000</summary>
    public Double HotThreshold { get; set; } = 100_000;

    /// <summary>是否已启动</summary>
    public Boolean Active { get; private set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    #region 内部数据
    /// <summary>各主题上次检测时的级别</summary>
    private readonly ConcurrentDictionary<String, HotTopicLevel> _topicLevels = new();

    /// <summary>各主题降级计时（进入低负载的时间戳）</summary>
    private readonly ConcurrentDictionary<String, Int64> _degradeTimers = new();

    /// <summary>检测定时器</summary>
    private TimerX? _timer;

    /// <summary>级别变更事件锁</summary>
    private readonly Object _eventLock = new();
    #endregion

    #region 事件
    /// <summary>主题级别变更事件。参数：主题、旧级别、新级别、建议副本数</summary>
    public event Action<String, HotTopicLevel, HotTopicLevel, Int32>? OnLevelChanged;

    /// <summary>热点主题检测事件。参数：主题、当前级别、发布速率（次/秒）、订阅者数</summary>
    public event Action<String, HotTopicLevel, Double, Int32>? OnHotTopicDetected;
    #endregion

    #region 构造
    /// <summary>实例化热点主题检测器</summary>
    public HotTopicDetector() { }
    #endregion

    #region 方法
    /// <summary>记录一次主题发布</summary>
    /// <param name="topic">主题名</param>
    public void RecordPublish(String topic)
    {
        if (topic.IsNullOrEmpty()) return;

        var now = Runtime.TickCount64;
        _topicCounts.AddOrUpdate(topic, _ => new PublishRecord(now), (_, rec) =>
        {
            rec.Increment(now);
            return rec;
        });
    }

    /// <summary>启动检测定时器</summary>
    public void Start()
    {
        if (Active) return;
        Active = true;

        _timer = new TimerX(CheckHotTopics, null, CheckInterval, CheckInterval);
        WriteLog("HotTopicDetector started, window={0}s, interval={1}ms", WindowSeconds, CheckInterval);
    }

    /// <summary>停止检测</summary>
    public void Stop()
    {
        Active = false;
        _timer.TryDispose();
        _timer = null;
        WriteLog("HotTopicDetector stopped");
    }

    /// <summary>获取指定主题的当前级别</summary>
    /// <param name="topic">主题名</param>
    /// <returns>主题级别，未识别时返回 Cold</returns>
    public HotTopicLevel GetLevel(String topic)
    {
        return _topicLevels.TryGetValue(topic, out var level) ? level : HotTopicLevel.Cold;
    }

    /// <summary>获取当前所有热点主题（级别 ≥ Warm）</summary>
    public Dictionary<String, HotTopicLevel> GetHotTopics()
    {
        var result = new Dictionary<String, HotTopicLevel>();
        foreach (var kv in _topicLevels)
        {
            if (kv.Value >= HotTopicLevel.Warm)
                result[kv.Key] = kv.Value;
        }
        return result;
    }

    /// <summary>获取统计信息</summary>
    public String GetStats()
    {
        var total = _topicCounts.Count;
        var hot = 0;
        foreach (var level in _topicLevels.Values)
        {
            if (level >= HotTopicLevel.Warm) hot++;
        }
        return $"TotalTopics={total}, HotTopics={hot}";
    }
    #endregion

    #region 内部逻辑
    /// <summary>主题发布计数（滑动窗口）</summary>
    private readonly ConcurrentDictionary<String, PublishRecord> _topicCounts = new();

    private void CheckHotTopics(Object? state)
    {
        try
        {
            var now = Runtime.TickCount64;
            var windowMs = WindowSeconds * 1000L;
            var thresholdCold = ColdThreshold;
            var thresholdWarm = WarmThreshold;
            var thresholdHot = HotThreshold;

            // 复制当前快照，避免遍历期间并发修改
            var snapshot = _topicCounts.ToArray();

            foreach (var kv in snapshot)
            {
                var topic = kv.Key;
                var record = kv.Value;
                var (count, firstTick) = record.GetCountAndReset(now, windowMs);

                // 清理超过 2 个周期无更新的条目
                if (count <= 0 && now - record.LastUpdateTick > windowMs * 2)
                {
                    _topicCounts.TryRemove(topic, out _);
                    _topicLevels.TryRemove(topic, out _);
                    _degradeTimers.TryRemove(topic, out _);
                    continue;
                }

                if (count <= 0) continue;

                // 计算发布速率（次/秒）
                var elapsed = Math.Max(now - firstTick, 1000); // 至少 1 秒
                var rate = (Double)count * 1000 / elapsed;

                // 查询当前级别和建议副本数
                var oldLevel = _topicLevels.TryGetValue(topic, out var l) ? l : HotTopicLevel.Cold;
                var (newLevel, suggestedReplicas) = Classify(rate);

                // 级别未变，跳过
                if (newLevel == oldLevel) continue;

                // 降温（级别下降）需要延迟确认
                if (newLevel < oldLevel)
                {
                    if (!_degradeTimers.TryGetValue(topic, out var degradeStart))
                    {
                        _degradeTimers[topic] = now;
                        continue; // 首次检测到降温，等待确认期
                    }
                    if (now - degradeStart < DegradeConfirmMs) continue; // 确认期内，暂不降级
                    // 确认期已过，执行降级
                    _degradeTimers.TryRemove(topic, out _);
                }
                else
                {
                    // 升温（级别上升）立即执行
                    _degradeTimers.TryRemove(topic, out _);
                }

                // 更新级别并触发事件
                _topicLevels[topic] = newLevel;

                WriteLog("Topic[{0}] level changed: {1}→{2}, rate={3:F1}/s, replicas={4}",
                    topic, oldLevel, newLevel, rate, suggestedReplicas);

                OnLevelChanged?.Invoke(topic, oldLevel, newLevel, suggestedReplicas);

                if (newLevel >= HotTopicLevel.Warm)
                {
                    OnHotTopicDetected?.Invoke(topic, newLevel, rate, 0);
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("CheckHotTopics error: {0}", ex.Message);
        }
    }

    /// <summary>根据发布速率分类主题级别</summary>
    private (HotTopicLevel level, Int32 replicas) Classify(Double rate)
    {
        if (rate >= HotThreshold)
            return (HotTopicLevel.VeryHot, 21);
        if (rate >= WarmThreshold)
            return (HotTopicLevel.Hot, 9);
        if (rate >= ColdThreshold)
            return (HotTopicLevel.Warm, 5);
        return (HotTopicLevel.Cold, 3);
    }
    #endregion

    #region 辅助
    private void WriteLog(String format, params Object?[] args) => Log?.InfoPrefixed("[HotTopicDetector]", format, args);

    /// <summary>发布记录</summary>
    private class PublishRecord
    {
        public Int64 LastUpdateTick;
        private Int32 _count;
        public Int64 FirstTick;

        public PublishRecord(Int64 tick)
        {
            LastUpdateTick = tick;
            FirstTick = tick;
            _count = 1;
        }

        public void Increment(Int64 tick)
        {
            Interlocked.Increment(ref _count);
            LastUpdateTick = tick;
        }

        public (Int32 count, Int64 firstTick) GetCountAndReset(Int64 now, Int64 windowMs)
        {
            var count = Interlocked.Exchange(ref _count, 0);
            var first = FirstTick;
            FirstTick = now;
            return (count, first);
        }
    }
    #endregion

    #region Dispose
    /// <summary>释放资源</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
    #endregion
}

/// <summary>热点主题级别</summary>
public enum HotTopicLevel
{
    /// <summary>冷。正常负载，默认副本因子 3</summary>
    Cold = 0,

    /// <summary>温。较高负载，副本因子 5~9</summary>
    Warm = 1,

    /// <summary>热。高负载，副本因子 9~21</summary>
    Hot = 2,

    /// <summary>极热。极高负载，启动广播分发</summary>
    VeryHot = 3,
}
