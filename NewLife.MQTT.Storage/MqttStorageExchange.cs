using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Storage;

/// <summary>持久化消息交换机。基于 IStorageProvider 实现持久会话和离线消息的存取</summary>
public class MqttStorageExchange : MqttExchange
{
    /// <summary>存储提供者</summary>
    public IStorageProvider? Provider { get; set; }

    /// <summary>保存持久会话（委托给存储提供者）</summary>
    public override void SavePersistentSession(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions)
    {
        base.SavePersistentSession(clientId, sessionId, subscriptions);
        _ = Provider?.SavePersistentSessionAsync(clientId, sessionId, subscriptions);
    }

    /// <summary>清除持久会话（委托给存储提供者）</summary>
    public override void ClearPersistentSession(String clientId)
    {
        base.ClearPersistentSession(clientId);
        _ = Provider?.ClearPersistentSessionAsync(clientId);
    }

    /// <summary>入队离线消息（委托给存储提供者）</summary>
    public override void EnqueueOfflineMessage(String clientId, PublishMessage message)
    {
        base.EnqueueOfflineMessage(clientId, message);
        _ = Provider?.EnqueueOfflineMessageAsync(clientId, message);
    }

    /// <summary>初始化存储</summary>
    public async Task InitAsync() => await (Provider?.InitAsync() ?? Task.CompletedTask);
}
