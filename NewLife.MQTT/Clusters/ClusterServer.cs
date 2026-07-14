using System.Collections.Concurrent;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
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

    /// <summary>Raft共识引擎。节点数≥3时自动启用</summary>
    public RaftCluster? RaftCluster { get; set; }

    /// <summary>是否启用 UDP 广播自动发现。启用后在局域网内无需手动配置节点地址。默认 false</summary>
    public Boolean EnableDiscovery { get; set; }

    /// <summary>集群共享密钥。设置后集群端口(2883)需相同密钥才允许加入。同网段UDP发现的节点自动信任。默认 null 表示不校验</summary>
    public String? ClusterSecret { get; set; }

    /// <summary>自动发现服务实例</summary>
    public ClusterDiscovery? Discovery { get; private set; }

    /// <summary>服务提供者。主要用于创建控制器实例，支持构造函数注入</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private ApiServer? _server;
    private TimerX? _timer;
    private TimerX? _metricsTimer;

    /// <summary>集群转发消息计数</summary>
    private Int64 _messageCount;

    /// <summary>集群转发字节计数</summary>
    private Int64 _byteCount;
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

        // 启动自动发现
        if (EnableDiscovery)
        {
            var discovery = new ClusterDiscovery
            {
                Cluster = this,
                Log = Log,
            };
            discovery.Start();
            Discovery = discovery;
        }

        // 启动Raft共识引擎（如已注入）
        RaftCluster?.Start();

        // 启动集群指标定时发布
        _metricsTimer = new TimerX(PublishMetrics, null, 10_000, 60_000) { Async = true };
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

        _metricsTimer.TryDispose();
        _metricsTimer = null;

        RaftCluster?.Stop();

        Discovery?.Stop();
        Discovery = null;

        _server.TryDispose();
        _server = null;
    }

    /// <summary>获取节点信息</summary>
    public NodeInfo GetNodeInfo() => new() { EndPoint = $"{NetHelper.MyIP()}:{Port}", ClusterSecret = ClusterSecret };

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
            LogEvent("NodeTimeout", "节点 {0} 超时退出，LastActive={1}", item, DateTime.Now.AddMinutes(-5));

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

    /// <summary>获取Raft共识引擎（供ClusterController等使用）</summary>
    public RaftCluster? GetRaft() => RaftCluster;

    /// <summary>记录集群转发的消息</summary>
    public void IncrementMessageCount(Int32 byteSize)
    {
        Interlocked.Increment(ref _messageCount);
        Interlocked.Add(ref _byteCount, byteSize);
    }
    /// <summary>记录集群事件</summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="format">消息格式</param>
    /// <param name="args">参数</param>
    public void LogEvent(String eventType, String format, params Object?[] args)
    {
        var msg = $"[{eventType}] {String.Format(format, args)}";
        WriteLog(msg);
    }
    private void PublishMetrics(Object? state)
    {
        var exchange = Exchange;
        if (exchange == null) return;

        var nodeCount = Nodes.Count + 1;
        var raftState = RaftCluster != null ? RaftCluster.State.ToString() : "Disabled";

        // 发布集群指标到 $SYS 主题
        var msgs = new (String topic, String payload)[]
        {
            ("$SYS/cluster/nodes/count", nodeCount.ToString()),
            ("$SYS/cluster/nodes/active", Nodes.Count(e => e.Value.LastActive > DateTime.Now.AddSeconds(-30)).ToString()),
            ("$SYS/cluster/messages/forwarded", Interlocked.Read(ref _messageCount).ToString()),
            ("$SYS/cluster/bytes/forwarded", Interlocked.Read(ref _byteCount).ToString()),
            ("$SYS/cluster/raft/state", raftState),
        };

        foreach (var (topic, payload) in msgs)
        {
            var pm = new PublishMessage();
            pm.Topic = topic;
            pm.QoS = QualityOfService.AtMostOnce;
            pm.Retain = false;
            pm.Payload = new Packet(payload.GetBytes());
            exchange.Publish(pm);
        }
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
