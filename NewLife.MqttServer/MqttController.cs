using System;
using System.Linq;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MqttServer;

internal class MqttController : MqttHandler
{
    private readonly ILog _log;

    private DefaultManagedMqttClient defaultManagedMqttClient { get; set; }

    public MqttController(ILog log, DefaultManagedMqttClient dmmc)
    {
        defaultManagedMqttClient = dmmc;
        _log = log;
        //defaultManagedMqttClient?.StartAsync(default);
    }

    protected override MqttMessage? OnConnect(ConnectMessage message)
    {
        _log.InfoPrefixed("[MqttServer] ", "Client [{0}] connected, user={1} pass={2} clientId={3}", Session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

        //将连接加入管理
        defaultManagedMqttClient.AddClient(Session);

        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.InfoPrefixed("[MqttServer] ", "Client [{0}] disconnected", Session.Remote);

        defaultManagedMqttClient.RemoveClient(Session);

        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.InfoPrefixed("[MqttServer] ", "Publish [{0}:qos={1}]: {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());

        //defaultManagedMqttClient.Enqueue(message);

        return base.OnPublish(message);
    }

    protected override UnsubAck OnUnsubscribe(UnsubscribeMessage message)
    {
        _log.InfoPrefixed("[MqttServer] ", "Client [{0}] unsubscribed from topics [{1}]", Session.Remote, String.Join(", ", message.TopicFilters));

        defaultManagedMqttClient.UnSubTopic(Session, message.TopicFilters);

        return base.OnUnsubscribe(message);
    }

    protected override SubAck OnSubscribe(SubscribeMessage message)
    {
        _log.InfoPrefixed("[MqttServer] ", "Client [{0}] subscribed to topics [{1}]", Session.Remote, String.Join(", ", message.Requests.Select(p => p.TopicFilter)));

        defaultManagedMqttClient.SubTopic(Session, message.Requests);

        return base.OnSubscribe(message);
    }
}