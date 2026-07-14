using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Clusters;

/// <summary>发布信息</summary>
public class PublishInfo
{
    /// <summary>消息</summary>
    public PublishMessage Message { get; set; } = null!;

    /// <summary>批量消息列表（批量合并转发模式）</summary>
    public List<PublishMessage>? Messages { get; set; }

    /// <summary>远程地址。订阅该主题的客户端远程地址</summary>
    public String? RemoteEndpoint { get; set; }

    /// <summary>生存跳数。每转发一次减1，为0时丢弃。默认3</summary>
    public Int32 TTL { get; set; } = 3;

    /// <summary>已访问节点路径。用于检测消息环路</summary>
    public List<String>? NodePath { get; set; }

    /// <summary>是否为批量消息</summary>
    public Boolean IsBatch => Messages != null && Messages.Count > 0;
}
