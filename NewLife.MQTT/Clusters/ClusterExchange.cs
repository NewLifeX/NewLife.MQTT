using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Clusters;

/// <summary>集群交换机</summary>
/// <remarks>
/// 集群交换机主要用于维护主题订阅关系，以及消息转发
/// </remarks>
public class ClusterExchange : DisposeBase, IMqttExchange, ITracerFeature
{
    #region 属性
    /// <summary>集群服务端</summary>
    public ClusterServer Cluster { get; set; } = null!;

    /// <summary>内部交换机</summary>
    public IMqttExchange Inner { get; set; } = null!;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 会话管理
    /// <summary>添加会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public virtual Boolean Add(Int32 sessionId, IMqttHandler session) => Inner.Add(sessionId, session);

    /// <summary>获取会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public virtual Boolean TryGetValue(Int32 sessionId, out IMqttHandler session) => Inner.TryGetValue(sessionId, out session);

    /// <summary>删除会话</summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public virtual Boolean Remove(Int32 sessionId) => Inner.Remove(sessionId);
    #endregion

    #region 订阅关系
    /// <summary>订阅主题</summary>
    /// <param name="sessionId"></param>
    /// <param name="topic"></param>
    /// <param name="qos"></param>
    public virtual void Subscribe(Int32 sessionId, String topic, QualityOfService qos) => Inner.Subscribe(sessionId, topic, qos);//todo 向其它节点广播订阅关系

    /// <summary>取消主题订阅</summary>
    /// <param name="sessionId"></param>
    /// <param name="topic"></param>
    public virtual void Unsubscribe(Int32 sessionId, String topic) => Inner.Unsubscribe(sessionId, topic);//todo 向其它节点广播取消订阅关系

    #endregion

    #region 转发消息
    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息
    /// </remarks>
    /// <param name="message"></param>
    public virtual void Publish(PublishMessage message) => Inner.Publish(message);//todo 查找其它节点订阅关系，然后转发消息

    #endregion
}
