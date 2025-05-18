using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Handlers;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群服务器</summary>
public class ClusterServer : DisposeBase, IServer, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>集群名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>集群端口。默认2883</summary>
    public Int32 Port { get; set; } = 2883;

    /// <summary>集群节点集合</summary>
    public ConcurrentDictionary<String, ClusterNode> Nodes { get; } = new ConcurrentDictionary<String, ClusterNode>();

    /// <summary>消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>集群交换机</summary>
    public ClusterExchange? ClusterExchange { get; set; }

    /// <summary>服务提供者。主要用于创建控制器实例，支持构造函数注入</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private ApiServer? _server;
    private TimerX? _timer;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ClusterServer() { }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => Name ?? base.ToString();

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }
    #endregion

    #region 启动停止
    /// <summary>启动服务</summary>
    public void Start()
    {
        if (Name.IsNullOrEmpty()) Name = $"Cluster{Port}";

        ServiceProvider ??= ObjectContainer.Provider;

        var uri = new NetUri(NetType.Tcp, "*", Port);
        var server = new ApiServer(uri)
        {
            Name = Name,
            //Port = Port,
            ServiceProvider = ServiceProvider,

            Tracer = Tracer,
            Log = Log,
        };

        // 传递集群实例到控制器
        server["Cluster"] = this;
        server.Register<ClusterController>();

#if DEBUG
        //server.EncoderLog = Log;
#endif

        server.Start();

        _server = server;

        _timer = new TimerX(DoPing, null, 1_000, 15_000) { Async = true };
    }

    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    public void Stop(String? reason)
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

    /// <summary>获取节点信息</summary>
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
    public Boolean AddNode(String endpoint)
    {
        // 跳过自己
        var uri = new NetUri(endpoint);
        var ip = NetHelper.MyIP();
        if ((uri.Address == ip || uri.Address.IsLocal()) && uri.Port == Port) return false;

        // 本地环回地址，替换为外网地址
        if (uri.Address.IsLocal()) endpoint = $"{ip}:{uri.Port}";

        var node = new ClusterNode { EndPoint = endpoint };
        if (!Nodes.TryAdd(endpoint, node)) return false;

        // 马上开始连接并心跳
        _timer?.SetNext(200);

        //_ = node.Join(GetNodeInfo());

        return true;
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}
