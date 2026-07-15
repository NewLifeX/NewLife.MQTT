using NewLife.Data;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;
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

    /// <summary>更新集群密钥。远程节点调用此方法将新密钥同步到本节点</summary>
    /// <param name="info">节点信息（含新密钥）</param>
    /// <returns>确认消息</returns>
    public String UpdateSecret(NodeInfo info)
    {
        if (info.ClusterSecret == null) return "OK";

        _cluster.WriteLog("收到节点密钥更新[{0}]，同步新密钥", info.EndPoint);
        _cluster.UpdateClusterSecret(info.ClusterSecret);

        return "OK";
    }

    /// <summary>加入集群</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public NodeInfo Join(NodeInfo info)
    {
        // 共享密钥验证
        if (!ValidateClusterSecret(info, out var msg))
        {
            _cluster.WriteLog("节点加入被拒绝[{0}]：{1}", info.EndPoint, msg);
            throw new InvalidOperationException($"集群加入被拒绝：{msg}");
        }

        var endpoint = info.EndPoint;

        var node = _cluster.Nodes.GetOrAdd(endpoint, k => new ClusterNode { EndPoint = k });
        node.Session = Session;
        node.Times = 1;
        node.LastActive = DateTime.Now;

        if (Session != null) Session["Node"] = node;

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
            // 延迟销毁，避免在 RPC 响应返回前关闭 Session 导致客户端超时
            _ = Task.Run(() => { try { node.TryDispose(); } catch (Exception ex) { _cluster?.WriteLog("节点[{0}]延迟销毁异常: {1}", node, ex.Message); } });
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

        _cluster.WriteLog("节点[{0}]订阅：{1}", node, infos.Join(",", e => $"{e.Topic}(client={e.ClientId})"));

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

        _cluster.WriteLog("节点[{0}]退订：{1}", node, infos.Join(",", e => $"{e.Topic}(client={e.ClientId})"));

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

    /// <summary>批量发布。收到其它节点合并转发的批量消息</summary>
    /// <param name="info">批量发布信息</param>
    public String PublishBatch(PublishInfo info)
    {
        if (info.Messages == null || info.Messages.Count == 0) return "OK";

        var exchange = _cluster.Exchange;
        if (exchange == null) return "OK";

        foreach (var msg in info.Messages)
        {
            exchange.Publish(msg);
        }

        return "OK";
    }
    #endregion

    #region Raft共识
    /// <summary>Raft投票请求</summary>
    public RaftVoteResponse RequestVote(RaftVoteRequest request)
    {
        var raft = _cluster.GetRaft();
        return raft?.HandleRequestVote(request) ?? new RaftVoteResponse { Term = 0, VoteGranted = false };
    }

    /// <summary>Raft AppendEntries请求</summary>
    public RaftAppendEntriesResponse AppendEntries(RaftAppendEntriesRequest request)
    {
        var raft = _cluster.GetRaft();
        return raft?.HandleAppendEntries(request) ?? new RaftAppendEntriesResponse { Term = 0, Success = false };
    }

    /// <summary>Raft提交Log（非Leader转发）</summary>
    public String SubmitRaftLog(RaftLogEntry entry)
    {
        var raft = _cluster.GetRaft();
        raft?.Submit(entry);
        return "OK";
    }

    /// <summary>Raft InstallSnapshot（落后节点拉取快照）</summary>
    public RaftAppendEntriesResponse InstallSnapshot(RaftSnapshot snapshot)
    {
        var raft = _cluster.GetRaft();
        return raft?.HandleInstallSnapshot(snapshot) ?? new RaftAppendEntriesResponse { Term = 0, Success = false };
    }
    #endregion

    #region 系统管理
    /// <summary>集群健康检查</summary>
    public Object Health() => new
    {
        NodeId = _cluster.GetNodeInfo().EndPoint,
        Nodes = _cluster.Nodes.Count,
        ActiveNodes = _cluster.Nodes.Count(e => e.Value.LastActive > DateTime.Now.AddSeconds(-30)),
        RaftState = _cluster.RaftCluster?.State.ToString() ?? "Disabled",
        RaftLeader = _cluster.RaftCluster?.LeaderId,
        ClusterSecret = !_cluster.ClusterSecret.IsNullOrEmpty() ? "configured" : null,
    };
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

        // 提取离线消息（从队列中取出，随会话迁移到新节点）
        var pendingMessages = new List<PublishMessage>();
        while (session.OfflineMessages.TryDequeue(out var msg))
        {
            pendingMessages.Add(msg);
        }

        return new SessionInfo
        {
            ClientId = clientId,
            Subscriptions = session.Subscriptions.ToDictionary(kv => kv.Key, kv => (Int32)kv.Value),
            PendingMessages = pendingMessages.Count > 0 ? pendingMessages : null,
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

    /// <summary>原子化获取并删除持久会话。避免会话迁移期间被误恢复造成竞争</summary>
    public SessionInfo? TransferSession(String clientId)
    {
        var info = GetSession(clientId);
        if (info != null)
            DeleteSession(clientId);
        return info;
    }
    #endregion

    #region 辅助
    /// <summary>验证集群共享密钥。同一网段自动信任，跨网段必须校验</summary>
    private Boolean ValidateClusterSecret(NodeInfo info, out String message)
    {
        message = String.Empty;

        var secret = _cluster.ClusterSecret;
        if (secret.IsNullOrEmpty())
        {
            // 未配置密钥，全放行
            return true;
        }

        // 同网段自动信任：从请求节点地址提取IP，判断是否与本机同网段
        var endpoint = info.EndPoint;
        if (!endpoint.IsNullOrEmpty())
        {
            var portIdx = endpoint.LastIndexOf(':');
            var remoteIpStr = portIdx > 0 ? endpoint[..portIdx] : endpoint;
            if (System.Net.IPAddress.TryParse(remoteIpStr, out var remoteIp))
            {
                var localIp = NetHelper.MyIP();
                var remoteBytes = remoteIp.GetAddressBytes();
                var localBytes = localIp.GetAddressBytes();
                // 简单同网段判断：前3段相同（/24子网）
                if (remoteBytes.Length >= 3 && localBytes.Length >= 3 &&
                    remoteBytes[0] == localBytes[0] &&
                    remoteBytes[1] == localBytes[1] &&
                    remoteBytes[2] == localBytes[2])
                {
                    return true;
                }
            }
        }

        // 跨网段：必须校验密钥
        if (info.ClusterSecret == secret) return true;

        message = "ClusterSecret 不匹配";
        return false;
    }
    #endregion
}
