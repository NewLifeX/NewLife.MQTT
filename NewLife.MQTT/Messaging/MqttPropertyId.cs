namespace NewLife.MQTT.Messaging;

/// <summary>MQTT 5.0 属性标识符</summary>
/// <remarks>
/// MQTT 5.0 引入属性系统，每种属性有唯一ID和固定的数据类型。
/// 属性可以附加在 CONNECT/CONNACK/PUBLISH/PUBACK/PUBREC/PUBREL/PUBCOMP/SUBSCRIBE/SUBACK/UNSUBSCRIBE/UNSUBACK/DISCONNECT/AUTH 报文中。
/// </remarks>
public enum MqttPropertyId : Byte
{
    /// <summary>载荷格式标识。Byte类型，标识 Payload 是 UTF-8(1) 还是二进制(0)</summary>
    PayloadFormatIndicator = 0x01,

    /// <summary>消息过期间隔。UInt32类型，秒为单位</summary>
    MessageExpiryInterval = 0x02,

    /// <summary>内容类型。UTF-8字符串，类似 MIME type</summary>
    ContentType = 0x03,

    /// <summary>响应主题。UTF-8字符串，用于请求/响应模式</summary>
    ResponseTopic = 0x08,

    /// <summary>关联数据。二进制数据，用于请求/响应模式中关联请求和响应</summary>
    CorrelationData = 0x09,

    /// <summary>订阅标识符。变长整数（1~268435455）</summary>
    SubscriptionIdentifier = 0x0B,

    /// <summary>会话过期间隔。UInt32类型，秒为单位</summary>
    SessionExpiryInterval = 0x11,

    /// <summary>分配客户端标识符。UTF-8字符串，服务端为空ClientId客户端分配的标识</summary>
    AssignedClientIdentifier = 0x12,

    /// <summary>服务端保持连接。UInt16类型，服务端可覆盖客户端的KeepAlive值</summary>
    ServerKeepAlive = 0x13,

    /// <summary>认证方法。UTF-8字符串，增强认证使用的方法名</summary>
    AuthenticationMethod = 0x15,

    /// <summary>认证数据。二进制数据，增强认证使用的数据</summary>
    AuthenticationData = 0x16,

    /// <summary>请求问题信息。Byte类型，客户端请求服务端返回原因字符串和用户属性</summary>
    RequestProblemInformation = 0x17,

    /// <summary>遗嘱延迟间隔。UInt32类型，秒为单位</summary>
    WillDelayInterval = 0x18,

    /// <summary>请求响应信息。Byte类型，客户端请求服务端返回响应信息</summary>
    RequestResponseInformation = 0x19,

    /// <summary>响应信息。UTF-8字符串</summary>
    ResponseInformation = 0x1A,

    /// <summary>服务端引用。UTF-8字符串，指示客户端连接到另一个服务端</summary>
    ServerReference = 0x1C,

    /// <summary>原因字符串。UTF-8字符串，用于诊断</summary>
    ReasonString = 0x1F,

    /// <summary>接收最大值。UInt16类型，限制未确认的QoS>0消息数量</summary>
    ReceiveMaximum = 0x21,

    /// <summary>主题别名最大值。UInt16类型，支持的主题别名最大数量</summary>
    TopicAliasMaximum = 0x22,

    /// <summary>主题别名。UInt16类型，用数字代替主题名以减少带宽</summary>
    TopicAlias = 0x23,

    /// <summary>最大QoS。Byte类型（0或1），服务端支持的最大QoS级别</summary>
    MaximumQoS = 0x24,

    /// <summary>保留消息可用。Byte类型（0或1），服务端是否支持保留消息</summary>
    RetainAvailable = 0x25,

    /// <summary>用户属性。UTF-8字符串对（键值对），可出现多次</summary>
    UserProperty = 0x26,

    /// <summary>最大报文大小。UInt32类型，客户端/服务端可接受的最大报文长度</summary>
    MaximumPacketSize = 0x27,

    /// <summary>通配符订阅可用。Byte类型（0或1）</summary>
    WildcardSubscriptionAvailable = 0x28,

    /// <summary>订阅标识符可用。Byte类型（0或1）</summary>
    SubscriptionIdentifierAvailable = 0x29,

    /// <summary>共享订阅可用。Byte类型（0或1）</summary>
    SharedSubscriptionAvailable = 0x2A,
}
