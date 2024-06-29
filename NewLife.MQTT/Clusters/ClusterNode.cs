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
    /// <summary>结点地址。唯一标识</summary>
    public String EndPoint { get; set; } = null!;

    /// <summary>会话连接</summary>
    [IgnoreDataMember]
    public IApiSession? Session { get; set; }

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

        Session.TryDispose();
        Client.TryDispose();
    }
    #endregion

    #region 方法
    private void Init()
    {
        if (Client != null) return;

        var uri = new NetUri(EndPoint);
        if (uri.Type == NetType.Unknown) uri.Type = NetType.Tcp;

        var client = new ApiClient(uri.ToString())
        {
            Log = XTrace.Log
        };
#if DEBUG
        //client.Log = XTrace.Log;
        //client.EncoderLog = client.Log;
#endif
        client.Open();

        Client = client;
    }

    /// <summary>回响</summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public async Task<String?> Echo(String msg)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Echo", msg);
    }

    /// <summary>加入集群</summary>
    public async Task<NodeInfo?> Join(NodeInfo info)
    {
        Init();

        _myNode = info;

        return _remoteNode = await Client.InvokeAsync<NodeInfo>("Cluster/Join", info);
    }

    /// <summary>心跳</summary>
    public async Task<NodeInfo?> Ping(NodeInfo info)
    {
        Init();

        _myNode = info;

        if (_remoteNode == null)
            return await Join(info);
        else
            return await Client.InvokeAsync<NodeInfo>("Cluster/Ping", info);
    }

    /// <summary>离开集群</summary>
    public async Task<String?> Leave(NodeInfo info)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Leave", info);
    }

    /// <summary>订阅</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public async Task<String?> Subscribe(SubscriptionInfo[] infos)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Subscribe", infos);
    }

    /// <summary>退订</summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public async Task<String?> Unsubscribe(SubscriptionInfo[] infos)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Unsubscribe", infos);
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public async Task<String?> Ping()
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Ping");
    }

    /// <summary>发布</summary>
    public async Task<String?> Publish(PublishInfo info)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Publish", info);
    }
    #endregion
}
