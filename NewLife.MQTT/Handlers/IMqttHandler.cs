using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT处理器</summary>
/// <returns></returns>
public interface IMqttHandler
{
    /// <summary>处理消息</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    MqttMessage Process(INetSession session, MqttMessage message);
}

/// <summary>MQTT处理器基类</summary>
public abstract class MqttHandler : IMqttHandler
{
    /// <summary>处理消息</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public virtual MqttMessage Process(INetSession session, MqttMessage message)
    {
        MqttMessage rs = null;
        rs = message.Type switch
        {
            MqttType.Connect => OnConnect(session, message as ConnectMessage),
            MqttType.Publish => OnPublish(session, message as PublishMessage),
            MqttType.PubRel => OnPublishRelease(session, message as PubRel),
            MqttType.PubRec => OnPublishReceive(session, message as PubRec),
            MqttType.Subscribe => OnSubscribe(session, message as SubscribeMessage),
            MqttType.UnSubscribe => OnUnsubscribe(session, message as UnsubscribeMessage),
            MqttType.PingReq => OnPing(session, message as PingRequest),
            MqttType.Disconnect => OnDisconnect(session, message as DisconnectMessage),
            _ => null,
        };
        return rs;
    }

    /// <summary>客户端连接时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual ConnAck OnConnect(INetSession session, ConnectMessage message) => new() { ReturnCode = ConnectReturnCode.Accepted };

    /// <summary>客户端断开时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage OnDisconnect(INetSession session, DisconnectMessage message) => null;

    /// <summary>收到心跳时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PingResponse OnPing(INetSession session, PingRequest message) => new();

    /// <summary>收到发布消息时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttIdMessage OnPublish(INetSession session, PublishMessage message)
    {
        return message.QoS switch
        {
            QualityOfService.AtMostOnce => null,
            QualityOfService.AtLeastOnce => new PubAck(),
            QualityOfService.ExactlyOnce => new PubRec(),
            _ => null,
        };
    }

    /// <summary>收到发布消息时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubComp OnPublishRelease(INetSession session, PubRel message) => new();

    /// <summary>收到发布已接收消息时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubRel OnPublishReceive(INetSession session, PubRec message) => new();

    /// <summary>收到订阅请求时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual SubAck OnSubscribe(INetSession session, SubscribeMessage message) => new()
    {
        GrantedQos = message.Requests.Select(x => x.QualityOfService).ToList(),
        Id = message.Id,
        QoS = message.QoS
    };

    /// <summary>收到取消订阅时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual UnsubAck OnUnsubscribe(INetSession session, UnsubscribeMessage message) => new();
}