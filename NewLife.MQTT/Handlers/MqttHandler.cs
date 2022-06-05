using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT处理器</summary>
/// <param name="session">网络会话</param>
/// <param name="message">消息</param>
/// <returns></returns>
public delegate MqttMessage MqttHandler(INetSession session, MqttMessage message);