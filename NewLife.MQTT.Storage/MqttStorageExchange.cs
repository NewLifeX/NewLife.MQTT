using NewLife.Log;
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
        FireAndForget(Localization.Translate("Save persistent session [{0}]", clientId), () => Provider?.SavePersistentSessionAsync(clientId, sessionId, subscriptions));
    }

    /// <summary>清除持久会话（委托给存储提供者）</summary>
    public override void ClearPersistentSession(String clientId)
    {
        base.ClearPersistentSession(clientId);
        FireAndForget(Localization.Translate("Clear persistent session [{0}]", clientId), () => Provider?.ClearPersistentSessionAsync(clientId));
    }

    /// <summary>入队离线消息（委托给存储提供者）</summary>
    public override void EnqueueOfflineMessage(String clientId, PublishMessage message)
    {
        base.EnqueueOfflineMessage(clientId, message);
        FireAndForget(Localization.Translate("Enqueue offline message [{0}]", clientId), () => Provider?.EnqueueOfflineMessageAsync(clientId, message));
    }

    /// <summary>初始化存储</summary>
    public async Task InitAsync() => await (Provider?.InitAsync() ?? Task.FromResult(false));

    /// <summary>异步执行存储操作并记录异常，避免 fire-and-forget 静默吞掉存储失败</summary>
    /// <param name="action">操作描述</param>
    /// <param name="func">异步存储操作</param>
    private void FireAndForget(String action, Func<Task?> func)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await (func() ?? Task.FromResult(0)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WriteLog("Storage operation failed: {0}, {1}", action, ex.Message);
            }
        });
    }

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.InfoPrefixed($"[{GetType().Name}]", format, args);
    #endregion
}
