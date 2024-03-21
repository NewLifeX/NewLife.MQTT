using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群服务器</summary>
public class ClusterServer : DisposeBase, IServer, /*IServiceProvider,*/ ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>集群名称</summary>
    public String Name { get; set; }

    /// <summary>集群端口。默认2883</summary>
    public Int32 Port { get; set; } = 2883;

    /// <summary>集群节点集合</summary>
    public ConcurrentDictionary<String, ClusterNode> Nodes { get; } = new ConcurrentDictionary<String, ClusterNode>();

    public IServiceProvider? ServiceProvider { get; set; }

    private ApiServer? _server;
    private TimerX? _timer;
    #endregion

    #region 构造
    public ClusterServer()
    {
        //XTrace.WriteLine("new ClusterServer");
    }

    public override String ToString() => Name ?? base.ToString();

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }
    #endregion

    #region 启动停止
    public void Start()
    {
        if (Name.IsNullOrEmpty()) Name = $"Cluster{Port}";

        ServiceProvider ??= ObjectContainer.Provider;

        var server = new ApiServer
        {
            Name = Name,
            Port = Port,
            ServiceProvider = ServiceProvider,

            Tracer = Tracer,
            Log = Log,
        };

        server.Register<ClusterController>();

#if DEBUG
        server.EncoderLog = Log;
#endif

        server.Start();

        _server = server;

        _timer = new TimerX(DoPing, null, 1_000, 15_000) { Async = true };
    }

    public void Stop(String reason)
    {
        // 所有节点退出
        //var myNode = GetNodeInfo();
        foreach (var item in Nodes)
        {
            var node = item.Value;
            //_ = node.Leave(myNode);
            node.TryDispose();
        }

        Nodes.Clear();

        _timer.TryDispose();
        _timer = null;

        _server.TryDispose();
        _server = null;
    }

    public NodeInfo GetNodeInfo() => new() { EndPoint = $"{NetHelper.MyIP()}:{Port}" };

    private void DoPing(Object state)
    {
        // 清理超时节点
        var exp = DateTime.Now.AddMinutes(-5);
        var dels = new List<String>();
        foreach (var item in Nodes)
        {
            if (item.Value.LastActive < exp)
            {
                dels.Add(item.Key);
                Nodes.TryRemove(item.Key, out _);
            }
        }

        foreach (var item in dels)
        {
            WriteLog("节点超时退出：{0}", item);

            if (Nodes.TryRemove(item, out var node))
            {
                node.TryDispose();
            }
        }

        var myNode = GetNodeInfo();

        // 向所有节点发送Ping
        foreach (var item in Nodes)
        {
            var node = item.Value;
            _ = node.Ping(myNode);
        }
    }
    #endregion

    #region 业务逻辑
    /// <summary>添加集群节点，建立长连接</summary>
    /// <param name="endpoint"></param>
    public void AddNode(String endpoint)
    {
        var node = new ClusterNode { EndPoint = endpoint };
        if (!Nodes.TryAdd(endpoint, node)) return;

        // 马上开始连接并心跳
        //_timer?.SetNext(200);

        _ = node.Join(GetNodeInfo());
    }
    #endregion

    #region 日志
    public ITracer? Tracer { get; set; }

    public ILog Log { get; set; } = Logger.Null;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
