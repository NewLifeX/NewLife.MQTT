using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Clusters;

public class PublishInfo
{
    public PublishMessage Message { get; set; } = null!;

    /// <summary>远程地址。订阅该主题的客户端远程地址</summary>
    public String? RemoteEndpoint { get; set; }
}
