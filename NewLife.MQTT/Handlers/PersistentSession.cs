using System.Collections.Concurrent;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>持久化会话状态。用于 CleanSession=0 时保存订阅关系和离线消息</summary>
/// <remarks>
/// MQTT 协议要求当 CleanSession=0 时，服务端必须保存会话状态，
/// 包括订阅关系和未确认的 QoS>0 消息，以便客户端重连后恢复。
/// </remarks>
public class PersistentSession
{
    /// <summary>客户端标识</summary>
    public String ClientId { get; set; } = null!;

    /// <summary>会话标识</summary>
    public Int32 SessionId { get; set; }

    /// <summary>订阅关系。key=主题过滤器，value=QoS</summary>
    public ConcurrentDictionary<String, QualityOfService> Subscriptions { get; set; } = new();

    /// <summary>离线消息队列。暂存 QoS>0 的消息</summary>
    public ConcurrentQueue<PublishMessage> OfflineMessages { get; set; } = new();

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}
