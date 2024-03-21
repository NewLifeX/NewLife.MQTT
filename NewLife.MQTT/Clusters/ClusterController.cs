using NewLife.Remoting;

namespace NewLife.MQTT.Clusters;

/// <summary>接口控制器。对外提供RPC接口</summary>
public class ClusterController : IApi
{
    #region 属性
    public IApiSession Session { get; set; }

    private readonly ClusterServer _cluster;
    #endregion

    public ClusterController(ClusterServer cluster)
    {
        _cluster = cluster;
    }

    #region 集群管理
    public String Echo(String msg) => msg;

    public NodeInfo Join(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times = 1;
        node.LastActive = DateTime.Now;

        _cluster.WriteLog("节点加入集群：{0}", node);

        return _cluster.GetNodeInfo();
    }

    public NodeInfo Ping(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times++;
        node.LastActive = DateTime.Now;

        return _cluster.GetNodeInfo();
    }

    public String Leave(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        if (_cluster.Nodes.TryRemove(endpoint, out var node))
        {
            _cluster.WriteLog("节点退出集群：{0}", node);
        }

        return "OK";
    }
    #endregion

    #region 订阅管理
    public String Subscribe()
    {
        return "OK";
    }

    public String Unsubscribe()
    {
        return "OK";
    }
    #endregion

    #region 消息管理
    public String Publish()
    {
        return "OK";
    }
    #endregion

}
