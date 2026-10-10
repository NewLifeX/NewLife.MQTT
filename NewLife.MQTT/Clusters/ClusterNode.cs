using System.Runtime.Serialization;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Remoting;

namespace NewLife.MQTT.Clusters;

/// <summary>集群节点</summary>
public class ClusterNode : DisposeBase
{
    #region 属性
    /// <summary>结点地址。唯一标识，形如 host:port 或 http://host:port</summary>
    public String EndPoint { get; set; } = null!;

    /// <summary>集群共享密钥。作为请求令牌携带</summary>
    [IgnoreDataMember]
    public String? Secret { get; set; }

    /// <summary>客户端</summary>
    [IgnoreDataMember]
    public IApiClient Client { get; set; } = null!;

    /// <summary>次数</summary>
    public Int32 Times { get; set; }

    /// <summary>最后活跃</summary>
    public DateTime LastActive { get; set; } = DateTime.Now;

    private NodeInfo? _myNode;
    private NodeInfo? _remoteNode;
    #endregion

    #region 构造
    /// <summary>已重载</summary>
    /// <returns></returns>
    public override String ToString() => EndPoint ?? base.ToString();

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (_remoteNode != null && _myNode != null) _ = Leave(_myNode).Wait(500);
        }
        catch { }

        Client.TryDispose();
    }
    #endregion

    #region 方法
    private void Init()
    {
        if (Client != null) return;

        // 集群 RPC 走 HTTP：EndPoint 为 host:port 时补全协议前缀
        var url = EndPoint.StartsWithIgnoreCase("http://", "https://") ? EndPoint : $"http://{EndPoint}";

        var client = new ApiHttpClient(url)
        {
            Log = XTrace.Log,
            Token = Secret,
            CodeName = "code",
            DataName = "data",
        };

        Client = client;
    }

    /// <summary>更新集群密钥。更新后新建的连接携带新令牌</summary>
    /// <param name="secret">新密钥</param>
    public void UpdateSecret(String? secret)
    {
        Secret = secret;
        if (Client != null) Client.Token = secret;
    }

    /// <summary>通知远程节点同步新密钥</summary>
    /// <param name="info">节点信息（含新密钥）</param>
    /// <returns></returns>
    public Task<String?> UpdateRemoteSecret(NodeInfo info)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/UpdateSecret", info);
    }

    /// <summary>回响</summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public Task<String?> Echo(String msg)
    {
        Init();

        // Http 参数按名称绑定，单参数需以具名对象传递；AOT 下匿名类型属性会被裁剪导致序列化为空对象，改用字典（线格式不变）
        return Client.InvokeAsync<String>("Cluster/Echo", new Dictionary<String, Object?> { ["msg"] = msg });
    }

    /// <summary>加入集群</summary>
    public async Task<NodeInfo?> Join(NodeInfo info)
    {
        Init();

        _myNode = info;

        return _remoteNode = await Client.InvokeAsync<NodeInfo>("Cluster/Join", info).ConfigureAwait(false);
    }

    /// <summary>心跳</summary>
    public Task<NodeInfo?> Ping(NodeInfo info)
    {
        Init();

        _myNode = info;

        if (_remoteNode == null)
            return Join(info);
        else
            return Client.InvokeAsync<NodeInfo>("Cluster/Ping", info);
    }

    /// <summary>离开集群</summary>
    public Task<String?> Leave(NodeInfo info)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/Leave", info);
    }

    /// <summary>订阅</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public Task<String?> Subscribe(SubscriptionInfo[] infos)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/Subscribe", new Dictionary<String, Object?> { ["infos"] = infos });
    }

    /// <summary>退订</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public Task<String?> Unsubscribe(SubscriptionInfo[] infos)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/Unsubscribe", new Dictionary<String, Object?> { ["infos"] = infos });
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public Task<String?> Ping()
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/Ping");
    }

    /// <summary>发布</summary>
    public Task<String?> Publish(PublishInfo info)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/Publish", info);
    }

    /// <summary>批量发布。将多条消息合并为一个RPC请求</summary>
    /// <param name="info">批量发布信息，使用 Messages 字段携带多条消息</param>
    public Task<String?> PublishBatch(PublishInfo info)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/PublishBatch", info);
    }

    /// <summary>从远程节点查询客户端持久会话（集群会话漂移）</summary>
    /// <param name="clientId">客户端标识</param>
    public Task<SessionInfo?> GetSession(String clientId)
    {
        Init();

        return Client.InvokeAsync<SessionInfo>("Cluster/GetSession", new Dictionary<String, Object?> { ["clientId"] = clientId });
    }

    /// <summary>删除远程节点的客户端持久会话（会话迁移完成后调用）</summary>
    /// <param name="clientId">客户端标识</param>
    public Task<String?> DeleteSession(String clientId)
    {
        Init();

        return Client.InvokeAsync<String>("Cluster/DeleteSession", new Dictionary<String, Object?> { ["clientId"] = clientId });
    }

    /// <summary>原子化获取并删除远程节点的持久会话（会话迁移）</summary>
    /// <param name="clientId">客户端标识</param>
    public Task<SessionInfo?> TransferSession(String clientId)
    {
        Init();

        return Client.InvokeAsync<SessionInfo>("Cluster/TransferSession", new Dictionary<String, Object?> { ["clientId"] = clientId });
    }
    #endregion
}
