using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Clusters;

/// <summary>发布信息</summary>
public class PublishInfo
{
    /// <summary>消息</summary>
    public PublishMessage Message { get; set; } = null!;

    /// <summary>远程地址。订阅该主题的客户端远程地址</summary>
    public String? RemoteEndpoint { get; set; }

    /// <summary>生存跳数。每转发一次减1，为0时丢弃。默认3</summary>
    public Int32 TTL { get; set; } = 3;

    /// <summary>已访问节点路径。用于检测消息环路</summary>
    public List<String>? NodePath { get; set; }
}
