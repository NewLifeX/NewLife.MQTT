namespace NewLife.MQTT.MqttSn;

/// <summary>MQTT-SN codec. Message type identification and byte conversion</summary>
public static class MqttSnCodec
{
    /// <summary>Decode MQTT-SN message from buffer</summary>
    public static MqttSnMessage? Decode(Byte[] data, Int32 offset, Int32 count)
    {
        if (count < 2) return null;

        Int32 length;
        Int32 headerOffset;
        if (data[offset] == 0x01)
        {
            if (count < 3) return null;
            length = (data[offset + 1] << 8) | data[offset + 2];
            headerOffset = 3;
        }
        else
        {
            length = data[offset];
            headerOffset = 1;
        }

        if (length < 2 || count < length) return null;
        var type = (MqttSnMessageType)data[offset + headerOffset];

        var msg = CreateMessage(type);
        if (msg == null) return null;

        return msg.Decode(data, offset, length);
    }

    /// <summary>Encode message to byte array</summary>
    public static Byte[] Encode(MqttSnMessage message) => message.Encode();

    /// <summary>Create message instance by type</summary>
    public static MqttSnMessage? CreateMessage(MqttSnMessageType type)
    {
        return type switch
        {
            MqttSnMessageType.Advertise => new AdvertiseMessage(),
            MqttSnMessageType.SearchGw => new SearchGwMessage(),
            MqttSnMessageType.GwInfo => new GwInfoMessage(),
            MqttSnMessageType.Connect => new MqttSnConnectMessage(),
            MqttSnMessageType.ConnAck => new MqttSnConnAckMessage(),
            MqttSnMessageType.WillTopic => new WillTopicMessage(),
            MqttSnMessageType.WillMsg => new WillMsgMessage(),
            MqttSnMessageType.Register => new RegisterMessage(),
            MqttSnMessageType.RegAck => new RegAckMessage(),
            MqttSnMessageType.Publish => new MqttSnPublishMessage(),
            MqttSnMessageType.PubAck => new MqttSnPubAckMessage(),
            MqttSnMessageType.PubRec => new PubRecMessage(),
            MqttSnMessageType.PubRel => new PubRelMessage(),
            MqttSnMessageType.PubComp => new PubCompMessage(),
            MqttSnMessageType.Subscribe => new MqttSnSubscribeMessage(),
            MqttSnMessageType.SubAck => new MqttSnSubAckMessage(),
            MqttSnMessageType.Unsubscribe => new MqttSnUnsubscribeMessage(),
            MqttSnMessageType.UnsubAck => new MqttSnUnsubAckMessage(),
            MqttSnMessageType.PingReq => new MqttSnPingReqMessage(),
            MqttSnMessageType.PingResp => new MqttSnPingRespMessage(),
            MqttSnMessageType.Disconnect => new MqttSnDisconnectMessage(),
            MqttSnMessageType.WillTopicUpd => new WillTopicMessage(),
            MqttSnMessageType.WillMsgUpd => new WillMsgMessage(),
            _ => null,
        };
    }
}
