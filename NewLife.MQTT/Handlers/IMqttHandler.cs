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
        switch (message.Type)
        {
            case MqttType.Connect:
                rs = OnConnect(session, message as ConnectMessage);
                break;
            case MqttType.Publish:
                rs = OnPublish(session, message as PublishMessage);
                break;
            case MqttType.PubRel:
                rs = OnPublishRelease(session, message as PubRel);
                break;
            case MqttType.Subscribe:
                rs = OnSubscribe(session, message as SubscribeMessage);
                break;
            case MqttType.UnSubscribe:
                rs = OnUnsubscribe(session, message as UnsubscribeMessage);
                break;
            case MqttType.PingReq:
                rs = OnPing(session, message as PingRequest);
                break;
            case MqttType.Disconnect:
                rs = OnDisconnect(session, message as DisconnectMessage);
                break;
            default:
                rs = null;
                break;
        }

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

    /// <summary>收到订阅请求时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual SubAck OnSubscribe(INetSession session, SubscribeMessage message) => new();

    /// <summary>收到取消订阅时</summary>
    /// <param name="session">网络会话</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual UnsubAck OnUnsubscribe(INetSession session, UnsubscribeMessage message) => new();
}