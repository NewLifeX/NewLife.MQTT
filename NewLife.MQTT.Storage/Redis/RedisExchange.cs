using NewLife.Caching;
using NewLife.MQTT.Messaging;
using NewLife.Serialization;

namespace NewLife.MQTT.Storage.Redis;

/// <summary>基于 NewLife.Redis（FullRedis）的持久化存储实现</summary>
public class RedisExchange : IStorageProvider
{
    /// <summary>提供者名称</summary>
    public String Name => "Redis";

    private readonly FullRedis _redis;

    /// <summary>实例化</summary>
    /// <param name="redis">FullRedis 实例</param>
    public RedisExchange(FullRedis redis) => _redis = redis;

    /// <summary>初始化存储</summary>
    public Task InitAsync() => Task.CompletedTask;

    /// <summary>保存持久会话</summary>
    public Task SavePersistentSessionAsync(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions)
    {
        var key = $"mqtt:session:{clientId}";
        _redis.Set<String>($"{key}:id", sessionId.ToString());
        _redis.Set<String>($"{key}:subs", subscriptions != null ? subscriptions.Select(kv => $"{kv.Key}:{(Int32)kv.Value}").Join(",") : "");
        _redis.SetExpire(key, TimeSpan.FromDays(7));
        return Task.CompletedTask;
    }

    /// <summary>恢复持久会话</summary>
    public Task<IDictionary<String, QualityOfService>?> RestorePersistentSessionAsync(String clientId)
    {
        var key = $"mqtt:session:{clientId}";
        var subs = _redis.Get<String>($"{key}:subs");
        if (subs.IsNullOrEmpty()) return Task.FromResult<IDictionary<String, QualityOfService>?>(null);

        var result = new Dictionary<String, QualityOfService>();
        foreach (var part in subs.Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && Int32.TryParse(kv[1], out var qos))
                result[kv[0]] = (QualityOfService)qos;
        }
        return Task.FromResult<IDictionary<String, QualityOfService>?>(result);
    }

    /// <summary>清除持久会话</summary>
    public Task ClearPersistentSessionAsync(String clientId)
    {
        var keys = new[] { $"mqtt:session:{clientId}:id", $"mqtt:session:{clientId}:subs", $"mqtt:offline:{clientId}" };
        foreach (var key in keys) _redis.Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>入队离线消息</summary>
    public Task EnqueueOfflineMessageAsync(String clientId, PublishMessage message)
    {
        var key = $"mqtt:offline:{clientId}";
        var list = _redis.GetList<String>(key);
        list.Add(message.ToJson());
        return Task.CompletedTask;
    }

    /// <summary>获取并清空离线消息</summary>
    public Task<List<PublishMessage>> DequeueOfflineMessagesAsync(String clientId)
    {
        var key = $"mqtt:offline:{clientId}";
        var list = _redis.GetList<String>(key);
        var messages = new List<PublishMessage>();
        foreach (var json in list)
        {
            try { var msg = json.ToJsonEntity<PublishMessage>(); if (msg != null) messages.Add(msg); } catch { }
        }
        list.Clear();
        return Task.FromResult(messages);
    }
}
