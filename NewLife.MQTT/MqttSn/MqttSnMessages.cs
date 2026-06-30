using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.MqttSn;

/// <summary>MQTT-SN v1.2 message types</summary>
public enum MqttSnMessageType : Byte
{
    Advertise = 0x00,
    SearchGw = 0x01,
    GwInfo = 0x02,
    Connect = 0x04,
    ConnAck = 0x05,
    WillTopicReq = 0x06,
    WillTopic = 0x07,
    WillMsgReq = 0x08,
    WillMsg = 0x09,
    Register = 0x0A,
    RegAck = 0x0B,
    Publish = 0x0C,
    PubAck = 0x0D,
    PubComp = 0x0E,
    PubRec = 0x0F,
    PubRel = 0x10,
    Subscribe = 0x12,
    SubAck = 0x13,
    Unsubscribe = 0x14,
    UnsubAck = 0x15,
    PingReq = 0x16,
    PingResp = 0x17,
    Disconnect = 0x18,
    WillTopicUpd = 0x1A,
    WillTopicResp = 0x1B,
    WillMsgUpd = 0x1C,
    WillMsgResp = 0x1D,
    Encapsulated = 0xFE,
}

/// <summary>MQTT-SN return codes</summary>
public enum MqttSnReturnCode : Byte
{
    Accepted = 0x00,
    RejectedCongestion = 0x01,
    RejectedInvalidTopicId = 0x02,
    RejectedNotSupported = 0x03,
}

/// <summary>Topic ID type in PUBLISH flags</summary>
public enum MqttSnTopicIdType : Byte
{
    Normal = 0x00,
    Predefined = 0x01,
    Short = 0x02,
}

/// <summary>MQTT-SN message base class</summary>
public abstract class MqttSnMessage
{
    /// <summary>Message type</summary>
    public abstract MqttSnMessageType MessageType { get; }

    /// <summary>Encode to byte array</summary>
    public abstract Byte[] Encode();

    /// <summary>Decode from buffer</summary>
    public abstract MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length);
}

/// <summary>ADVERTISE - gateway advertisement</summary>
public class AdvertiseMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Advertise;
    public Byte GatewayId { get; set; }
    public UInt16 Duration { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[5];
        buf[0] = 5;
        buf[1] = (Byte)MqttSnMessageType.Advertise;
        buf[2] = GatewayId;
        buf[3] = (Byte)(Duration >> 8);
        buf[4] = (Byte)(Duration & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        GatewayId = data[offset + 2];
        Duration = (UInt16)((data[offset + 3] << 8) | data[offset + 4]);
        return this;
    }
}

/// <summary>SEARCHGW - search gateway</summary>
public class SearchGwMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.SearchGw;
    public Byte Radius { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[3];
        buf[0] = 3;
        buf[1] = (Byte)MqttSnMessageType.SearchGw;
        buf[2] = Radius;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Radius = data[offset + 2];
        return this;
    }
}

/// <summary>GWINFO - gateway info response</summary>
public class GwInfoMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.GwInfo;
    public Byte GatewayId { get; set; }
    public String? GatewayAddress { get; set; }

    public override Byte[] Encode()
    {
        var addrBytes = GatewayAddress?.GetBytes() ?? [];
        var buf = new Byte[3 + addrBytes.Length];
        buf[0] = (Byte)(3 + addrBytes.Length);
        buf[1] = (Byte)MqttSnMessageType.GwInfo;
        buf[2] = GatewayId;
        if (addrBytes.Length > 0) Array.Copy(addrBytes, 0, buf, 3, addrBytes.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        GatewayId = data[offset + 2];
        if (length > 3) GatewayAddress = data.ToStr(null, offset + 3, length - 3);
        return this;
    }
}

/// <summary>CONNECT - connection request</summary>
public class MqttSnConnectMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Connect;
    public Byte Flags { get; set; }
    public Byte ProtocolId => 1;
    public UInt16 Duration { get; set; }
    public String ClientId { get; set; } = String.Empty;

    public Boolean Will => (Flags & 0x04) != 0;
    public Boolean CleanSession => (Flags & 0x02) != 0;

    public override Byte[] Encode()
    {
        var clientIdBytes = ClientId.GetBytes();
        var buf = new Byte[6 + clientIdBytes.Length];
        buf[0] = (Byte)(6 + clientIdBytes.Length);
        buf[1] = (Byte)MqttSnMessageType.Connect;
        buf[2] = Flags;
        buf[3] = ProtocolId;
        buf[4] = (Byte)(Duration >> 8);
        buf[5] = (Byte)(Duration & 0xFF);
        Array.Copy(clientIdBytes, 0, buf, 6, clientIdBytes.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        Duration = (UInt16)((data[offset + 4] << 8) | data[offset + 5]);
        ClientId = data.ToStr(null, offset + 6, length - 6);
        return this;
    }
}

/// <summary>CONNACK - connection acknowledgement</summary>
public class MqttSnConnAckMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.ConnAck;
    public MqttSnReturnCode ReturnCode { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[3];
        buf[0] = 3;
        buf[1] = (Byte)MqttSnMessageType.ConnAck;
        buf[2] = (Byte)ReturnCode;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        ReturnCode = (MqttSnReturnCode)data[offset + 2];
        return this;
    }
}

/// <summary>REGISTER - topic registration</summary>
public class RegisterMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Register;
    public UInt16 TopicId { get; set; }
    public UInt16 MessageId { get; set; }
    public String TopicName { get; set; } = String.Empty;

    public override Byte[] Encode()
    {
        var topicBytes = TopicName.GetBytes();
        var buf = new Byte[6 + topicBytes.Length];
        buf[0] = (Byte)(6 + topicBytes.Length);
        buf[1] = (Byte)MqttSnMessageType.Register;
        buf[2] = (Byte)(TopicId >> 8);
        buf[3] = (Byte)(TopicId & 0xFF);
        buf[4] = (Byte)(MessageId >> 8);
        buf[5] = (Byte)(MessageId & 0xFF);
        Array.Copy(topicBytes, 0, buf, 6, topicBytes.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        TopicId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        MessageId = (UInt16)((data[offset + 4] << 8) | data[offset + 5]);
        TopicName = data.ToStr(null, offset + 6, length - 6);
        return this;
    }
}

/// <summary>REGACK - topic registration ack</summary>
public class RegAckMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.RegAck;
    public UInt16 TopicId { get; set; }
    public UInt16 MessageId { get; set; }
    public MqttSnReturnCode ReturnCode { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[7];
        buf[0] = 7;
        buf[1] = (Byte)MqttSnMessageType.RegAck;
        buf[2] = (Byte)(TopicId >> 8);
        buf[3] = (Byte)(TopicId & 0xFF);
        buf[4] = (Byte)(MessageId >> 8);
        buf[5] = (Byte)(MessageId & 0xFF);
        buf[6] = (Byte)ReturnCode;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        TopicId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        MessageId = (UInt16)((data[offset + 4] << 8) | data[offset + 5]);
        ReturnCode = (MqttSnReturnCode)data[offset + 6];
        return this;
    }
}

/// <summary>PUBLISH - publish message</summary>
public class MqttSnPublishMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Publish;
    public Byte Flags { get; set; }
    public UInt16 TopicId { get; set; }
    public UInt16 MessageId { get; set; }
    public Byte[] Data { get; set; } = [];

    public Boolean Dup => (Flags & 0x80) != 0;
    public QualityOfService QoS => (QualityOfService)((Flags >> 5) & 0x03);
    public Boolean Retain => (Flags & 0x10) != 0;
    public Boolean Will => (Flags & 0x08) != 0;
    public Boolean CleanSession => (Flags & 0x04) != 0;
    public MqttSnTopicIdType TopicIdType => (MqttSnTopicIdType)(Flags & 0x03);

    public override Byte[] Encode()
    {
        var hasMsgId = QoS > QualityOfService.AtMostOnce;
        var headerLen = hasMsgId ? 7 : 5;
        var buf = new Byte[headerLen + Data.Length];
        buf[0] = (Byte)(headerLen + Data.Length);
        buf[1] = (Byte)MqttSnMessageType.Publish;
        buf[2] = Flags;
        buf[3] = (Byte)(TopicId >> 8);
        buf[4] = (Byte)(TopicId & 0xFF);
        if (hasMsgId)
        {
            buf[5] = (Byte)(MessageId >> 8);
            buf[6] = (Byte)(MessageId & 0xFF);
        }
        Array.Copy(Data, 0, buf, headerLen, Data.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        TopicId = (UInt16)((data[offset + 3] << 8) | data[offset + 4]);
        var headerLen = QoS > QualityOfService.AtMostOnce ? 7 : 5;
        if (headerLen <= length && QoS > QualityOfService.AtMostOnce)
            MessageId = (UInt16)((data[offset + 5] << 8) | data[offset + 6]);
        var dataLen = length - headerLen;
        if (dataLen > 0)
        {
            Data = new Byte[dataLen];
            Array.Copy(data, offset + headerLen, Data, 0, dataLen);
        }
        return this;
    }
}

/// <summary>PUBACK - publish acknowledgement</summary>
public class MqttSnPubAckMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PubAck;
    public UInt16 TopicId { get; set; }
    public UInt16 MessageId { get; set; }
    public MqttSnReturnCode ReturnCode { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[7];
        buf[0] = 7;
        buf[1] = (Byte)MqttSnMessageType.PubAck;
        buf[2] = (Byte)(TopicId >> 8);
        buf[3] = (Byte)(TopicId & 0xFF);
        buf[4] = (Byte)(MessageId >> 8);
        buf[5] = (Byte)(MessageId & 0xFF);
        buf[6] = (Byte)ReturnCode;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        TopicId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        MessageId = (UInt16)((data[offset + 4] << 8) | data[offset + 5]);
        ReturnCode = (MqttSnReturnCode)data[offset + 6];
        return this;
    }
}

/// <summary>PUBREC / PUBREL / PUBCOMP - QoS 2 flow messages</summary>
public class PubRecMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PubRec;
    public UInt16 MessageId { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[4];
        buf[0] = 4;
        buf[1] = (Byte)MqttSnMessageType.PubRec;
        buf[2] = (Byte)(MessageId >> 8);
        buf[3] = (Byte)(MessageId & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        MessageId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        return this;
    }
}

public class PubRelMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PubRel;
    public UInt16 MessageId { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[4];
        buf[0] = 4;
        buf[1] = (Byte)MqttSnMessageType.PubRel;
        buf[2] = (Byte)(MessageId >> 8);
        buf[3] = (Byte)(MessageId & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        MessageId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        return this;
    }
}

public class PubCompMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PubComp;
    public UInt16 MessageId { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[4];
        buf[0] = 4;
        buf[1] = (Byte)MqttSnMessageType.PubComp;
        buf[2] = (Byte)(MessageId >> 8);
        buf[3] = (Byte)(MessageId & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        MessageId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        return this;
    }
}

/// <summary>SUBSCRIBE - subscribe request</summary>
public class MqttSnSubscribeMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Subscribe;
    public Byte Flags { get; set; }
    public UInt16 MessageId { get; set; }
    public String TopicName { get; set; } = String.Empty;
    public UInt16 TopicId { get; set; }
    public MqttSnTopicIdType TopicIdType => (MqttSnTopicIdType)(Flags & 0x03);

    public override Byte[] Encode()
    {
        var topicBytes = TopicName.GetBytes();
        var hasTopicName = TopicIdType == MqttSnTopicIdType.Normal && TopicId == 0;
        var len = hasTopicName ? 5 + topicBytes.Length : 5;
        var buf = new Byte[len];
        buf[0] = (Byte)len;
        buf[1] = (Byte)MqttSnMessageType.Subscribe;
        buf[2] = Flags;
        buf[3] = (Byte)(MessageId >> 8);
        buf[4] = (Byte)(MessageId & 0xFF);
        if (hasTopicName)
            Array.Copy(topicBytes, 0, buf, 5, topicBytes.Length);
        else
        {
            buf[5] = (Byte)(TopicId >> 8);
            buf[6] = (Byte)(TopicId & 0xFF);
        }
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        MessageId = (UInt16)((data[offset + 3] << 8) | data[offset + 4]);
        if (TopicIdType == MqttSnTopicIdType.Short)
            TopicName = new String(new[] { (Char)data[offset + 5], (Char)data[offset + 6] });
        else if (length > 5)
            TopicName = data.ToStr(null, offset + 5, length - 5);
        return this;
    }
}

/// <summary>SUBACK - subscribe acknowledgement</summary>
public class MqttSnSubAckMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.SubAck;
    public Byte Flags { get; set; }
    public UInt16 TopicId { get; set; }
    public UInt16 MessageId { get; set; }
    public MqttSnReturnCode ReturnCode { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[8];
        buf[0] = 8;
        buf[1] = (Byte)MqttSnMessageType.SubAck;
        buf[2] = Flags;
        buf[3] = (Byte)(TopicId >> 8);
        buf[4] = (Byte)(TopicId & 0xFF);
        buf[5] = (Byte)(MessageId >> 8);
        buf[6] = (Byte)(MessageId & 0xFF);
        buf[7] = (Byte)ReturnCode;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        TopicId = (UInt16)((data[offset + 3] << 8) | data[offset + 4]);
        MessageId = (UInt16)((data[offset + 5] << 8) | data[offset + 6]);
        ReturnCode = (MqttSnReturnCode)data[offset + 7];
        return this;
    }
}

/// <summary>UNSUBSCRIBE / UNSUBACK / PINGREQ / PINGRESP / DISCONNECT / WILLTOPIC / WILLMSG</summary>
public class MqttSnUnsubscribeMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Unsubscribe;
    public Byte Flags { get; set; }
    public UInt16 MessageId { get; set; }
    public String TopicName { get; set; } = String.Empty;
    public UInt16 TopicId { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[7];
        buf[0] = 7;
        buf[1] = (Byte)MqttSnMessageType.Unsubscribe;
        buf[2] = Flags;
        buf[3] = (Byte)(MessageId >> 8);
        buf[4] = (Byte)(MessageId & 0xFF);
        buf[5] = (Byte)(TopicId >> 8);
        buf[6] = (Byte)(TopicId & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        MessageId = (UInt16)((data[offset + 3] << 8) | data[offset + 4]);
        TopicId = (UInt16)((data[offset + 5] << 8) | data[offset + 6]);
        return this;
    }
}

public class MqttSnUnsubAckMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.UnsubAck;
    public UInt16 MessageId { get; set; }

    public override Byte[] Encode()
    {
        var buf = new Byte[4];
        buf[0] = 4;
        buf[1] = (Byte)MqttSnMessageType.UnsubAck;
        buf[2] = (Byte)(MessageId >> 8);
        buf[3] = (Byte)(MessageId & 0xFF);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        MessageId = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        return this;
    }
}

public class MqttSnPingReqMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PingReq;
    public String? ClientId { get; set; }

    public override Byte[] Encode()
    {
        var clientBytes = ClientId?.GetBytes() ?? [];
        var buf = new Byte[2 + clientBytes.Length];
        buf[0] = (Byte)(2 + clientBytes.Length);
        buf[1] = (Byte)MqttSnMessageType.PingReq;
        if (clientBytes.Length > 0) Array.Copy(clientBytes, 0, buf, 2, clientBytes.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        if (length > 2) ClientId = data.ToStr(null, offset + 2, length - 2);
        return this;
    }
}

public class MqttSnPingRespMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.PingResp;

    public override Byte[] Encode()
    {
        var buf = new Byte[2];
        buf[0] = 2;
        buf[1] = (Byte)MqttSnMessageType.PingResp;
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length) => this;
}

public class MqttSnDisconnectMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.Disconnect;
    public UInt16? Duration { get; set; }

    public override Byte[] Encode()
    {
        if (Duration.HasValue)
        {
            var buf = new Byte[4];
            buf[0] = 4;
            buf[1] = (Byte)MqttSnMessageType.Disconnect;
            buf[2] = (Byte)(Duration.Value >> 8);
            buf[3] = (Byte)(Duration.Value & 0xFF);
            return buf;
        }
        var buf2 = new Byte[2];
        buf2[0] = 2;
        buf2[1] = (Byte)MqttSnMessageType.Disconnect;
        return buf2;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        if (length >= 4)
            Duration = (UInt16)((data[offset + 2] << 8) | data[offset + 3]);
        return this;
    }
}

public class WillTopicMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.WillTopic;
    public Byte Flags { get; set; }
    public String TopicName { get; set; } = String.Empty;
    public QualityOfService QoS => (QualityOfService)((Flags >> 5) & 0x03);
    public Boolean Retain => (Flags & 0x10) != 0;

    public override Byte[] Encode()
    {
        var topicBytes = TopicName.GetBytes();
        var buf = new Byte[3 + topicBytes.Length];
        buf[0] = (Byte)(3 + topicBytes.Length);
        buf[1] = (Byte)MqttSnMessageType.WillTopic;
        buf[2] = Flags;
        Array.Copy(topicBytes, 0, buf, 3, topicBytes.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        Flags = data[offset + 2];
        TopicName = data.ToStr(null, offset + 3, length - 3);
        return this;
    }
}

public class WillMsgMessage : MqttSnMessage
{
    public override MqttSnMessageType MessageType => MqttSnMessageType.WillMsg;
    public Byte[] Data { get; set; } = [];

    public override Byte[] Encode()
    {
        var buf = new Byte[2 + Data.Length];
        buf[0] = (Byte)(2 + Data.Length);
        buf[1] = (Byte)MqttSnMessageType.WillMsg;
        Array.Copy(Data, 0, buf, 2, Data.Length);
        return buf;
    }

    public override MqttSnMessage Decode(Byte[] data, Int32 offset, Int32 length)
    {
        var dataLen = length - 2;
        if (dataLen > 0)
        {
            Data = new Byte[dataLen];
            Array.Copy(data, offset + 2, Data, 0, dataLen);
        }
        return this;
    }
}
