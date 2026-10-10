using NewLife.Data;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Storage.Database;

/// <summary>基于 NewLife.XCode 的数据库持久化存储实现</summary>
public class DbExchange : IStorageProvider
{
    /// <summary>提供者名称</summary>
    public String Name => "Database";

    /// <summary>初始化存储</summary>
    public Task InitAsync() => Task.FromResult(0);

    /// <summary>保存持久会话</summary>
    public Task SavePersistentSessionAsync(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions)
    {
        var entity = new MqttSessionEntity
        {
            ClientId = clientId,
            SessionId = sessionId,
            Subscriptions = subscriptions?.Select(kv => $"{kv.Key}:{(Int32)kv.Value}").Join(","),
            ExpireTime = DateTime.Now.AddDays(7),
        };
        entity.Save();
        return Task.FromResult(0);
    }

    /// <summary>恢复持久会话</summary>
    public Task<IDictionary<String, QualityOfService>?> RestorePersistentSessionAsync(String clientId)
    {
        var entity = MqttSessionEntity.FindByClientId(clientId);
        if (entity?.Subscriptions == null) return Task.FromResult<IDictionary<String, QualityOfService>?>(null);

        var dict = new Dictionary<String, QualityOfService>();
        foreach (var part in entity.Subscriptions.Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && Int32.TryParse(kv[1], out var qos))
                dict[kv[0]] = (QualityOfService)qos;
        }
        return Task.FromResult<IDictionary<String, QualityOfService>?>(dict);
    }

    /// <summary>清除持久会话</summary>
    public Task ClearPersistentSessionAsync(String clientId)
    {
        MqttSessionEntity.FindByClientId(clientId)?.Delete();
        MqttOfflineMessageEntity.DeleteByClientId(clientId);
        return Task.FromResult(0);
    }

    /// <summary>入队离线消息</summary>
    public Task EnqueueOfflineMessageAsync(String clientId, PublishMessage message)
    {
        new MqttOfflineMessageEntity
        {
            ClientId = clientId,
            Topic = message.Topic,
            Payload = message.Payload?.ToArray(),
            QoS = (Int32)message.QoS,
            Retain = message.Retain,
        }.Save();
        return Task.FromResult(0);
    }

    /// <summary>获取并清空离线消息</summary>
    public Task<List<PublishMessage>> DequeueOfflineMessagesAsync(String clientId)
    {
        var entities = MqttOfflineMessageEntity.FindAllByClientId(clientId);
        var messages = new List<PublishMessage>();
        foreach (var e in entities)
        {
            var pm = new PublishMessage { Topic = e.Topic, QoS = (QualityOfService)e.QoS, Retain = e.Retain };
            if (e.Payload != null) pm.Payload = new ArrayPacket(e.Payload);
            messages.Add(pm);
        }
        MqttOfflineMessageEntity.DeleteByClientId(clientId);
        return Task.FromResult(messages);
    }
}
