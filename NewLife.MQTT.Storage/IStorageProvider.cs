using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Storage;

/// <summary>持久化存储提供者接口</summary>
public interface IStorageProvider
{
    /// <summary>提供者名称</summary>
    String Name { get; }

    /// <summary>保存持久会话（含订阅关系）</summary>
    Task SavePersistentSessionAsync(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions);

    /// <summary>恢复持久会话（返回订阅关系）</summary>
    Task<IDictionary<String, QualityOfService>?> RestorePersistentSessionAsync(String clientId);

    /// <summary>清除持久会话</summary>
    Task ClearPersistentSessionAsync(String clientId);

    /// <summary>入队离线消息</summary>
    Task EnqueueOfflineMessageAsync(String clientId, PublishMessage message);

    /// <summary>获取并清空离线消息</summary>
    Task<List<PublishMessage>> DequeueOfflineMessagesAsync(String clientId);

    /// <summary>初始化存储</summary>
    Task InitAsync();
}
