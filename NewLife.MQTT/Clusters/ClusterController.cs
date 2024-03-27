using NewLife.Data;
using NewLife.Remoting;

namespace NewLife.MQTT.Clusters;

/// <summary>接口控制器。对外提供RPC接口</summary>
public class ClusterController : IApi, IActionFilter
{
    #region 属性
    /// <summary>连接会话</summary>
    public IApiSession Session { get; set; } = null!;

    private ClusterServer _cluster = null!;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ClusterController()
    {
        //_cluster = cluster;
    }

    /// <summary>执行前</summary>
    /// <param name="filterContext"></param>
    public void OnActionExecuting(ControllerContext filterContext)
    {
        var ss = Session = filterContext.Session!;
        if (ss.Host is IExtend ext)
            _cluster = (ext["Cluster"] as ClusterServer)!;
    }

    /// <summary>执行后</summary>
    public void OnActionExecuted(ControllerContext filterContext) { }
    #endregion

    #region 集群管理
    public String Echo(String msg) => msg;

    public NodeInfo Join(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times = 1;
        node.LastActive = DateTime.Now;

        Session["Node"] = node;

        //if (node.Times == 1)
        _cluster.WriteLog("节点加入集群：{0}，来自：{1}", node, Session);

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
            _cluster.WriteLog("节点退出集群：{0}，来自：{1}", node, Session);
            node.TryDispose();
        }

        return "OK";
    }
    #endregion

    #region 订阅管理
    public String Subscribe(SubscriptionInfo[] infos)
    {
        var node = Session["Node"] as ClusterNode;
        //info.Node = node;

        // 集群订阅3，收到节点广播的订阅关系
        var exchange = _cluster.ClusterExchange;
        if (exchange != null)
        {
            foreach (var item in infos)
            {
                exchange.AddSubscription(item);
            }
        }

        return "OK";
    }

    public String Unsubscribe(SubscriptionInfo[] infos)
    {
        // 集群取消订阅3，收到节点广播的订阅关系
        var exchange = _cluster.ClusterExchange;
        if (exchange != null)
        {
            foreach (var item in infos)
            {
                exchange.RemoveSubscription(item);
            }
        }

        return "OK";
    }
    #endregion

    #region 消息管理
    public String Publish(PublishInfo info)
    {
        // 集群发布3，收到其它节点转发的消息
        var exchange = _cluster.Exchange;
        exchange?.Publish(info.Message);

        return "OK";
    }
    #endregion
}
