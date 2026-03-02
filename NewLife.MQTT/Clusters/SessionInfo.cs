namespace NewLife.MQTT.Clusters;

/// <summary>跨节点传输的会话信息</summary>
/// <remarks>
/// 用于集群会话漂移（F035）。当客户端重连到不同节点时，
/// 新节点通过 RPC 从其他节点获取该会话的订阅关系，完成恢复。
/// 离线 QoS>0 消息暂不迁移（未来扩展）。
/// </remarks>
public class SessionInfo
{
    /// <summary>客户端标识</summary>
    public String ClientId { get; set; } = null!;

    /// <summary>订阅关系。key=主题过滤器，value=QoS 级别（0/1/2）</summary>
    public Dictionary<String, Int32> Subscriptions { get; set; } = [];
}
