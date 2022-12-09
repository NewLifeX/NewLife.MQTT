using System;
using System.Linq;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MqttServer;

internal class MqttController : MqttHandler
{
    private readonly ILog _log;

    private DefaultManagedMqttClient defaultManagedMqttClient { get; set; }

    public MqttController(ILog log, DefaultManagedMqttClient dmmc)
    {
        defaultManagedMqttClient = dmmc;
        _log = log;
        if (defaultManagedMqttClient != null)
        {
            defaultManagedMqttClient.StartAsync(new System.Threading.CancellationToken());
        }
    }

    protected override ConnAck OnConnect(INetSession session, ConnectMessage message)
    {
        _log.Info("客户端[{0}]连接 user={0} pass={1} clientId={2}", session.Remote.EndPoint, message.Username, message.Password, message.ClientId);
        //将连接加入管理
        defaultManagedMqttClient.AddClient(session);
        return base.OnConnect(session, message);
    }

    protected override MqttMessage OnDisconnect(INetSession session, DisconnectMessage message)
    {
        _log.Info("客户端[{0}]断开", session.Remote);
        defaultManagedMqttClient.RemoveClient(session);
        return base.OnDisconnect(session, message);
    }

    protected override MqttIdMessage OnPublish(INetSession session, PublishMessage message)
    {
        _log.Info("发布[{0}:qos={1}]: {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());
        defaultManagedMqttClient.Enqueue(message);
        return base.OnPublish(session, message);
    }

    protected override UnsubAck OnUnsubscribe(INetSession session, UnsubscribeMessage message)
    {
        _log.Info("客户端[{0}]取消订阅主题[{1}]", session.Remote, string.Join("、", message.TopicFilters));
        defaultManagedMqttClient.UnSubTopic(session, message.TopicFilters);
        return base.OnUnsubscribe(session, message);
    }

    protected override SubAck OnSubscribe(INetSession session, SubscribeMessage message)
    {
        _log.Info("客户端[{0}]订阅主题[{1}]", session.Remote, string.Join("、", message.Requests.Select(p => p.TopicFilter)));

        defaultManagedMqttClient.SubTopic(session, message.Requests);
        return base.OnSubscribe(session, message);
    }
}