using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>消息交换机</summary>
public interface IMqttExchange
{
    #region 会话管理
    /// <summary>添加会话</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="session">处理器</param>
    /// <returns></returns>
    Boolean Add(Int32 sessionId, IMqttHandler session);

    /// <summary>获取会话</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="session">处理器</param>
    /// <returns></returns>
    Boolean TryGetValue(Int32 sessionId, out IMqttHandler session);

    /// <summary>删除会话</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns></returns>
    Boolean Remove(Int32 sessionId);
    #endregion

    #region 消息发布与订阅
    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息。
    /// 如果消息带有 Retain 标志，还会存储/清除保留消息。
    /// </remarks>
    /// <param name="message">发布消息</param>
    void Publish(PublishMessage message);

    /// <summary>订阅主题</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    void Subscribe(Int32 sessionId, String topic, QualityOfService qos);

    /// <summary>取消主题订阅</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="topic">主题过滤器</param>
    void Unsubscribe(Int32 sessionId, String topic);

    /// <summary>获取匹配主题的保留消息</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <returns></returns>
    IList<PublishMessage> GetRetainMessages(String topicFilter);
    #endregion

    #region 持久化会话
    /// <summary>保存持久化会话（CleanSession=0）</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="sessionId">会话标识</param>
    /// <param name="subscriptions">订阅关系</param>
    void SavePersistentSession(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions);

    /// <summary>恢复持久化会话</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="sessionId">新会话标识</param>
    /// <returns>是否存在旧会话</returns>
    Boolean RestorePersistentSession(String clientId, Int32 sessionId);

    /// <summary>清除持久化会话</summary>
    /// <param name="clientId">客户端标识</param>
    void ClearPersistentSession(String clientId);

    /// <summary>为离线的持久会话暂存消息</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="message">消息</param>
    void EnqueueOfflineMessage(String clientId, PublishMessage message);
    #endregion

    #region 统计与查询
    /// <summary>运行统计数据</summary>
    MqttStats Stats { get; }

    /// <summary>获取所有在线客户端标识</summary>
    /// <returns></returns>
    IList<String> GetClientIds();

    /// <summary>获取所有主题过滤器</summary>
    /// <returns></returns>
    IList<String> GetTopics();

    /// <summary>获取指定主题的订阅者数量</summary>
    /// <param name="topic">主题过滤器</param>
    /// <returns></returns>
    Int32 GetSubscriberCount(String topic);
    #endregion
}