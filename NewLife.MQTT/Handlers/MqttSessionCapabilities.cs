using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT 5.0 服务端会话能力</summary>
/// <remarks>
/// 管理 MQTT 5.0 协议新增的服务端能力，包括主题别名、流控、最大报文等。
/// 每个客户端连接维护一个实例。
/// 仅 MQTT 5.0 协议使用。
/// </remarks>
public class MqttSessionCapabilities
{
    #region 属性
    /// <summary>客户端请求的接收最大值。限制服务端同时发送的QoS>0未确认消息数</summary>
    public UInt16 ReceiveMaximum { get; set; } = 65535;

    /// <summary>客户端声明的最大报文大小</summary>
    public UInt32 MaximumPacketSize { get; set; } = UInt32.MaxValue;

    /// <summary>客户端支持的主题别名最大值</summary>
    public UInt16 TopicAliasMaximum { get; set; }

    /// <summary>服务端分配的主题别名最大值</summary>
    public UInt16 ServerTopicAliasMaximum { get; set; }

    /// <summary>客户端到服务端的主题别名映射</summary>
    public Dictionary<UInt16, String> ClientTopicAliases { get; set; } = [];

    /// <summary>服务端到客户端的主题别名映射</summary>
    public Dictionary<String, UInt16> ServerTopicAliases { get; set; } = [];

    /// <summary>会话过期间隔（秒）。MQTT 5.0</summary>
    public UInt32 SessionExpiryInterval { get; set; }

    /// <summary>当前 inflight 消息数</summary>
    public Int32 InflightCount { get; set; }

    private UInt16 _nextAlias;
    #endregion

    #region 方法
    /// <summary>从 CONNECT 属性中提取客户端能力</summary>
    /// <param name="properties">连接属性</param>
    public void ApplyConnectProperties(MqttProperties? properties)
    {
        if (properties == null) return;

        var receiveMax = properties.GetUInt16(MqttPropertyId.ReceiveMaximum);
        if (receiveMax.HasValue && receiveMax.Value > 0)
            ReceiveMaximum = receiveMax.Value;

        var maxPacket = properties.GetUInt32(MqttPropertyId.MaximumPacketSize);
        if (maxPacket.HasValue && maxPacket.Value > 0)
            MaximumPacketSize = maxPacket.Value;

        var topicAlias = properties.GetUInt16(MqttPropertyId.TopicAliasMaximum);
        if (topicAlias.HasValue)
            TopicAliasMaximum = topicAlias.Value;

        var sessionExpiry = properties.GetUInt32(MqttPropertyId.SessionExpiryInterval);
        if (sessionExpiry.HasValue)
            SessionExpiryInterval = sessionExpiry.Value;
    }

    /// <summary>解析客户端发来的主题别名</summary>
    /// <param name="message">发布消息</param>
    /// <returns>是否成功解析</returns>
    public Boolean ResolveTopicAlias(PublishMessage message)
    {
        var alias = message.Properties?.GetUInt16(MqttPropertyId.TopicAlias);
        if (alias == null || alias.Value == 0) return true;

        if (!message.Topic.IsNullOrEmpty())
        {
            // 有主题名 + 别名 → 建立映射
            ClientTopicAliases[alias.Value] = message.Topic;
        }
        else if (ClientTopicAliases.TryGetValue(alias.Value, out var topic))
        {
            // 无主题名 + 已知别名 → 还原主题
            message.Topic = topic;
        }
        else
        {
            // 无主题名 + 未知别名 → 协议错误
            return false;
        }

        return true;
    }

    /// <summary>为服务端到客户端的消息分配主题别名</summary>
    /// <param name="message">发布消息</param>
    public void AssignTopicAlias(PublishMessage message)
    {
        if (TopicAliasMaximum == 0) return;

        if (ServerTopicAliases.TryGetValue(message.Topic, out var alias))
        {
            // 已有别名，使用别名发送（不发主题名可节省带宽）
            message.Properties ??= new MqttProperties();
            message.Properties.SetUInt16(MqttPropertyId.TopicAlias, alias);
            // 注意：第一次发送别名时必须携带主题名，后续可以只发别名
        }
        else if (_nextAlias < TopicAliasMaximum)
        {
            // 分配新别名
            _nextAlias++;
            ServerTopicAliases[message.Topic] = _nextAlias;
            message.Properties ??= new MqttProperties();
            message.Properties.SetUInt16(MqttPropertyId.TopicAlias, _nextAlias);
        }
    }

    /// <summary>构建 CONNACK 属性</summary>
    /// <returns></returns>
    public MqttProperties BuildConnAckProperties()
    {
        var props = new MqttProperties();

        if (ServerTopicAliasMaximum > 0)
            props.SetUInt16(MqttPropertyId.TopicAliasMaximum, ServerTopicAliasMaximum);

        return props;
    }

    /// <summary>检查是否可以发送新的 QoS>0 消息（流控）</summary>
    /// <returns></returns>
    public Boolean CanSendQosMessage() => InflightCount < ReceiveMaximum;
    #endregion
}
