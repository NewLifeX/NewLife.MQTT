using System.Runtime.Serialization;
using NewLife.Log;
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

    [IgnoreDataMember]
    public IApiClient Client { get; set; }

    public Int32 Times { get; set; }

    public DateTime LastActive { get; set; } = DateTime.Now;
    #endregion

    #region 构造
    public override String ToString() => EndPoint ?? base.ToString();

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

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
        client.EncoderLog = client.Log;
#endif
        client.Open();

        Client = client;
    }

    public async Task<String> Echo(String msg)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Echo", msg);
    }

    public async Task<NodeInfo> Join(NodeInfo info)
    {
        Init();

        return await Client.InvokeAsync<NodeInfo>("Cluster/Join", info);
    }

    public async Task<NodeInfo> Ping(NodeInfo info)
    {
        Init();

        return await Client.InvokeAsync<NodeInfo>("Cluster/Ping", info);
    }

    public async Task<String> Leave(NodeInfo info)
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Leave", info);
    }

    public async Task<String> Subscribe()
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Subscribe");
    }

    public async Task<String> Unsubscribe()
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Unsubscribe");
    }

    public async Task<String> Ping()
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Ping");
    }

    public async Task<String> Publish()
    {
        Init();

        return await Client.InvokeAsync<String>("Cluster/Publish");
    }
    #endregion
}
