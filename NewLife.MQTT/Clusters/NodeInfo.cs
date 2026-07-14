namespace NewLife.MQTT.Clusters;

/// <summary>节点信息</summary>
public class NodeInfo
{
    /// <summary>节点地址</summary>
    public String EndPoint { get; set; } = null!;

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>集群共享密钥。用于跨网段节点加入验证</summary>
    public String? ClusterSecret { get; set; }
}
