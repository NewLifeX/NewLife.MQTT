namespace NewLife.MQTT.Clusters;

/// <summary>集群节点发现提供者接口。支持 UPD 广播、Stardust、Consul 等多种实现</summary>
public interface IClusterDiscoveryProvider
{
    /// <summary>提供者名称</summary>
    String Name { get; }

    /// <summary>启动发现服务</summary>
    /// <param name="cluster">集群服务器实例</param>
    /// <param name="token">取消令牌</param>
    Task StartAsync(ClusterServer cluster, CancellationToken token);

    /// <summary>停止发现服务</summary>
    /// <param name="token">取消令牌</param>
    Task StopAsync(CancellationToken token);
}
