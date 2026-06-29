using System.Collections.Concurrent;

namespace NewLife.MQTT.Handlers;

/// <summary>连接速率限制器。基于 IP 的滑动窗口限流，防止单 IP 短时间内大量连接</summary>
/// <remarks>
/// 使用滑动窗口算法，记录每个 IP 的连接时间戳。
/// 当窗口内连接数超过限制时拒绝新连接。
/// 线程安全，适用于高并发场景。
/// </remarks>
public class MqttConnectionRateLimiter
{
    #region 属性
    /// <summary>每个 IP 在窗口内的最大连接数。默认 100</summary>
    public Int32 MaxConnectionsPerWindow { get; set; } = 100;

    /// <summary>滑动窗口时长。默认 10 秒</summary>
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>IP 连接时间戳记录</summary>
    private readonly ConcurrentDictionary<String, ConcurrentQueue<DateTime>> _records = new();

    /// <summary>清理定时器</summary>
    private Timer? _cleanupTimer;
    #endregion

    #region 方法
    /// <summary>检查是否允许该 IP 建立新连接</summary>
    /// <param name="ip">客户端 IP 地址</param>
    /// <returns>是否允许连接</returns>
    public Boolean IsConnectionAllowed(String ip)
    {
        if (ip.IsNullOrEmpty()) return true;

        var now = DateTime.UtcNow;
        var windowStart = now - WindowDuration;

        var queue = _records.GetOrAdd(ip, _ => new ConcurrentQueue<DateTime>());

        // 清理过期记录
        while (queue.TryPeek(out var ts) && ts < windowStart)
            queue.TryDequeue(out _);

        // 检查是否超限
        if (queue.Count >= MaxConnectionsPerWindow)
            return false;

        // 记录本次连接
        queue.Enqueue(now);

        // 启动清理定时器
        _cleanupTimer ??= new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        return true;
    }

    /// <summary>获取指定 IP 在当前窗口内的连接数</summary>
    /// <param name="ip">客户端 IP 地址</param>
    /// <returns>连接数</returns>
    public Int32 GetConnectionCount(String ip)
    {
        if (!_records.TryGetValue(ip, out var queue)) return 0;

        var windowStart = DateTime.UtcNow - WindowDuration;
        while (queue.TryPeek(out var ts) && ts < windowStart)
            queue.TryDequeue(out _);

        return queue.Count;
    }

    /// <summary>重置所有记录</summary>
    public void Reset() => _records.Clear();

    /// <summary>清理过期记录</summary>
    private void CleanupExpired(Object? state)
    {
        var windowStart = DateTime.UtcNow - WindowDuration;
        foreach (var kv in _records)
        {
            var queue = kv.Value;
            while (queue.TryPeek(out var ts) && ts < windowStart)
                queue.TryDequeue(out _);
        }
    }
    #endregion
}
