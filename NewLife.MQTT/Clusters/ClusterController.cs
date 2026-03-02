using NewLife.Data;
using NewLife.MQTT.Handlers;
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
    /// <summary></summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public String Echo(String msg) => msg;

    /// <summary>加入集群</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public NodeInfo Join(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times = 1;
        node.LastActive = DateTime.Now;

        Session["Node"] = node;

        //if (node.Times == 1)
        _cluster.WriteLog("节点[{0}]加入集群，来自：{1}", node, Session);

        return _cluster.GetNodeInfo();
    }

    /// <summary>心跳</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public NodeInfo Ping(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times++;
        node.LastActive = DateTime.Now;

        return _cluster.GetNodeInfo();
    }

    /// <summary>离开集群</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public String Leave(NodeInfo info)
    {
        var endpoint = info.EndPoint;

        if (_cluster.Nodes.TryRemove(endpoint, out var node))
        {
            _cluster.WriteLog("节点[{0}]退出集群，来自：{1}", node, Session);
            node.TryDispose();
        }

        return "OK";
    }
    #endregion

    #region 订阅管理
    /// <summary>订阅</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public String Subscribe(SubscriptionInfo[] infos)
    {
        var node = Session["Node"] as ClusterNode;
        //info.Node = node;

        _cluster.WriteLog("节点[{0}]订阅：{1}", node, infos.Join(",", e => e.Topic));

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

    /// <summary>取消订阅</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public String Unsubscribe(SubscriptionInfo[] infos)
    {
        var node = Session["Node"] as ClusterNode;
        //info.Node = node;

        _cluster.WriteLog("节点[{0}]退订：{1}", node, infos.Join(",", e => e.Topic));

        // 集群退订3，收到节点广播的订阅关系
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
    /// <summary>发布消息</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public String Publish(PublishInfo info)
    {
        // 集群发布3，收到其它节点转发的消息
        var exchange = _cluster.Exchange;
        exchange?.Publish(info.Message);

        return "OK";
    }
    #endregion

    #region 会话漂移
    /// <summary>从本节点查询客户端的持久会话（供重连到其他节点的客户端使用）</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>会话信息；本节点无此会话时返回 null</returns>
    public SessionInfo? GetSession(String clientId)
    {
        if (_cluster.Exchange is not MqttExchange exchange) return null;

        var session = exchange.GetPersistentSessionInfo(clientId);
        if (session == null) return null;

        return new SessionInfo
        {
            ClientId = clientId,
            Subscriptions = session.Subscriptions.ToDictionary(kv => kv.Key, kv => (Int32)kv.Value),
        };
    }

    /// <summary>删除本节点的持久会话（会话迁移至新节点后调用，避免重复）</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>"OK"</returns>
    public String DeleteSession(String clientId)
    {
        if (_cluster.Exchange is MqttExchange exchange)
            exchange.DeletePersistentSession(clientId);

        return "OK";
    }
    #endregion
}
