using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群服务器</summary>
public class ClusterServer : IServer, /*IServiceProvider,*/ ILogFeature, ITracerFeature
{
    #region 属性
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
        XTrace.WriteLine("new ClusterServer");
    }
    #endregion

    #region 启动停止
    public void Start()
    {
        ServiceProvider ??= ObjectContainer.Provider;

        var server = new ApiServer
        {
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

        _timer = new TimerX(DoPing, null, 1_000, 60_000) { Async = true };
    }

    public void Stop(String reason)
    {
        _timer.TryDispose();
        _timer = null;

        _server.TryDispose();
        _server = null;
    }

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
                node.Client.TryDispose();
                node.Session.TryDispose();
            }
        }

        var ip = NetHelper.MyIP();
        var myNode = new NodeInfo { EndPoint = $"{ip}:{Port}" };

        // 向所有节点发送Ping
        foreach (var item in Nodes)
        {
            var node = item.Value;
            _ = node.Join(myNode);
        }
    }
    #endregion

    #region 业务逻辑
    #endregion

    //#region 服务
    //public Object GetService(Type serviceType)
    //{
    //    if (serviceType == typeof(ClusterServer)) return this;

    //    return ServiceProvider.GetService(serviceType);
    //}
    //#endregion

    #region 日志
    public ITracer? Tracer { get; set; }

    public ILog Log { get; set; } = Logger.Null;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
