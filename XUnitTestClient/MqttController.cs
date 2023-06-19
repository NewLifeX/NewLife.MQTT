using System;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace XUnitTestClient;

internal class MqttController : MqttHandler
{
    private readonly ILog _log;

    public MqttController(ILog log) => _log = log;

    protected override ConnAck OnConnect(ConnectMessage message)
    {
        _log.Info("客户端[{0}]连接 user={0} pass={1} clientId={2}", Session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.Info("客户端[{0}]断开", Session.Remote);

        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.Info("发布[{0}:qos={1}]: {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());

        return base.OnPublish(message);
    }
}