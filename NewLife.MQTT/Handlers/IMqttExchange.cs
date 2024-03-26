using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>消息交换机</summary>
public interface IMqttExchange
{
    /// <summary>添加会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    Boolean Add(Int32 sessionId, IMqttHandler session);

    /// <summary>获取会话</summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    Boolean TryGetValue(Int32 sessionId, out IMqttHandler session);

    /// <summary>删除会话</summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    Boolean Remove(Int32 sessionId);

    /// <summary>发布消息</summary>
    /// <remarks>
    /// 找到匹配该主题的订阅者，然后发送消息
    /// </remarks>
    /// <param name="message"></param>
    void Publish(PublishMessage message);

    /// <summary>订阅主题</summary>
    /// <param name="sessionId"></param>
    /// <param name="topic"></param>
    /// <param name="qos"></param>
    void Subscribe(Int32 sessionId, String topic, QualityOfService qos);

    /// <summary>取消主题订阅</summary>
    /// <param name="sessionId"></param>
    /// <param name="topic"></param>
    void Unsubscribe(Int32 sessionId, String topic);
}