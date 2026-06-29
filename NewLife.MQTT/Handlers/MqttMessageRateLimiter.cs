using System.Collections.Concurrent;

namespace NewLife.MQTT.Handlers;

/// <summary>消息速率限制器。基于令牌桶算法的客户端消息速率控制</summary>
/// <remarks>
/// 使用令牌桶算法控制每个客户端的消息发布速率。
/// 令牌以固定速率生成，每条消息消耗一个令牌。
/// 桶满时多余的令牌被丢弃，支持短时突发流量。
/// </remarks>
public class MqttMessageRateLimiter
{
    #region 属性
    /// <summary>每秒允许的最大消息数。默认 100</summary>
    public Double MaxMessagesPerSecond { get; set; } = 100;

    /// <summary>最大突发消息数（令牌桶容量）。默认与 MaxMessagesPerSecond 相同</summary>
    public Double MaxBurstSize { get; set; } = 100;

    /// <summary>客户端令牌桶</summary>
    private readonly ConcurrentDictionary<String, TokenBucket> _buckets = new();

    /// <summary>清理定时器</summary>
    private Timer? _cleanupTimer;

    /// <summary>令牌桶过期时间。默认 5 分钟无活动后清理</summary>
    public TimeSpan BucketExpiry { get; set; } = TimeSpan.FromMinutes(5);
    #endregion

    #region 方法
    /// <summary>检查是否允许该客户端发布消息</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>是否允许</returns>
    public Boolean IsMessageAllowed(String clientId)
    {
        if (clientId.IsNullOrEmpty()) return true;

        var bucket = _buckets.GetOrAdd(clientId, _ => new TokenBucket(MaxBurstSize, MaxMessagesPerSecond));
        var allowed = bucket.TryConsume();

        _cleanupTimer ??= new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        return allowed;
    }

    /// <summary>获取指定客户端的当前可用令牌数</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>可用令牌数</returns>
    public Double GetAvailableTokens(String clientId)
    {
        if (!_buckets.TryGetValue(clientId, out var bucket)) return MaxBurstSize;
        return bucket.AvailableTokens;
    }

    /// <summary>重置所有令牌桶</summary>
    public void Reset() => _buckets.Clear();

    /// <summary>清理过期的令牌桶</summary>
    private void CleanupExpired(Object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _buckets)
        {
            if (now - kv.Value.LastAccessTime > BucketExpiry)
                _buckets.TryRemove(kv.Key, out _);
        }
    }
    #endregion

    #region 令牌桶
    /// <summary>令牌桶</summary>
    private class TokenBucket
    {
        private readonly Double _capacity;
        private readonly Double _refillRate; // 每秒补充的令牌数
        private Double _tokens;
        private DateTime _lastRefill;

        /// <summary>当前可用令牌数</summary>
        public Double AvailableTokens
        {
            get
            {
                Refill();
                return _tokens;
            }
        }

        /// <summary>最后访问时间</summary>
        public DateTime LastAccessTime { get; private set; }

        /// <summary>实例化令牌桶</summary>
        /// <param name="capacity">桶容量</param>
        /// <param name="refillRate">每秒令牌补充速率</param>
        public TokenBucket(Double capacity, Double refillRate)
        {
            _capacity = capacity;
            _refillRate = refillRate;
            _tokens = capacity;
            _lastRefill = DateTime.UtcNow;
            LastAccessTime = DateTime.UtcNow;
        }

        /// <summary>尝试消费一个令牌</summary>
        /// <returns>是否成功消费</returns>
        public Boolean TryConsume()
        {
            LastAccessTime = DateTime.UtcNow;
            Refill();

            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }

            return false;
        }

        /// <summary>根据时间流逝补充令牌</summary>
        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            if (elapsed <= 0) return;

            _tokens = Math.Min(_capacity, _tokens + elapsed * _refillRate);
            _lastRefill = now;
        }
    }
    #endregion
}
