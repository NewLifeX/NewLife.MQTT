using System;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MqttServer;

internal class MqttController : MqttHandler
{
    private readonly ILog _log;

    public MqttController(ILog log) => _log = log;

    protected override ConnAck OnConnect(INetSession session, ConnectMessage message)
    {
        _log.Info("客户端[{0}]连接 user={0} pass={1} clientId={2}", session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

        return base.OnConnect(session, message);
    }

    protected override MqttMessage OnDisconnect(INetSession session, DisconnectMessage message)
    {
        _log.Info("客户端[{0}]断开", session.Remote);

        return base.OnDisconnect(session, message);
    }

    protected override MqttIdMessage OnPublish(INetSession session, PublishMessage message)
    {
        _log.Info("发布[{0}:qos={1}]: {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());

        return base.OnPublish(session, message);
    }
}