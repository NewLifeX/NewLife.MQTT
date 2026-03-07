using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT;

/// <summary>MQTT工厂</summary>
public class MqttFactory
{
    /// <summary>创建消息</summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public virtual MqttMessage CreateMessage(MqttType type)
    {
        switch (type)
        {
            case MqttType.Connect: return new ConnectMessage();
            case MqttType.ConnAck: return new ConnAck();
            case MqttType.Publish: return new PublishMessage();
            case MqttType.PubAck: return new PubAck();
            case MqttType.PubRec: return new PubRec();
            case MqttType.PubRel: return new PubRel();
            case MqttType.PubComp: return new PubComp();
            case MqttType.Subscribe: return new SubscribeMessage();
            case MqttType.SubAck: return new SubAck();
            case MqttType.UnSubscribe: return new UnsubscribeMessage();
            case MqttType.UnSubAck: return new UnsubAck();
            case MqttType.PingReq: return new PingRequest();
            case MqttType.PingResp: return new PingResponse();
            case MqttType.Disconnect: return new DisconnectMessage();
            case MqttType.Auth: return new AuthMessage();
            default:
                break;
        }

        throw new NotSupportedException($"{type}");
        //return null;
    }

    /// <summary>读取消息</summary>
    /// <param name="pk"></param>
    /// <returns></returns>
    public virtual MqttMessage? ReadMessage(IPacket pk) => ReadMessage(pk, MqttVersion.V311);

    /// <summary>读取消息（支持 MQTT 5.0 属性）</summary>
    /// <param name="pk">数据包</param>
    /// <param name="protocolLevel">协议版本</param>
    /// <returns></returns>
    public virtual MqttMessage? ReadMessage(IPacket pk, MqttVersion protocolLevel)
    {
        try
        {
            var msg = CreateMessage((MqttType)(pk[0] >> 4));
            // 仅 MQTT 5.0 时将协议版本作为 context 传入（PublishMessage 等需要读取属性）
            Object? ctx = protocolLevel >= MqttVersion.V500 ? (Object)protocolLevel : null;
            if (!msg.Read(pk.GetStream(), ctx)) return null;

            return msg;
        }
        catch (XException)
        {
            // 解析数据异常时，把数据记录到埋点，方便分析异常指令
            DefaultSpan.Current?.SetTag(pk);

            throw;
        }
    }
}