using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;
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

    /// <summary>布隆过滤器交换机。用于跨节点转发预检加速</summary>
    public BloomFilterExchange? BloomFilter { get; set; }

    /// <summary>批量合并转发管道。将发往同一节点的多条小消息合并为批量RPC</summary>
    public BatchForwardPipe? BatchPipe { get; set; }

    /// <summary>分片路由表。基于一致哈希将主题映射到分片→节点</summary>
    public TopicRouter? TopicRouter { get; set; }

    /// <summary>Raft共识引擎。节点数≥3时自动启用</summary>
    public RaftCluster? RaftCluster { get; set; }

    /// <summary>是否启用 UDP 广播自动发现。启用后在局域网内无需手动配置节点地址。默认 false</summary>
    public Boolean EnableDiscovery { get; set; }

    /// <summary>集群共享密钥。设置后集群端口(2883)需相同密钥才允许加入。同网段UDP发现的节点自动信任。默认 null 表示不校验</summary>
    public String? ClusterSecret { get => _clusterSecret; set => UpdateClusterSecret(value); }

    private String? _clusterSecret;

    /// <summary>密钥轮换过渡窗口（毫秒）。窗口内继续接受旧密钥，容忍各节点更新进度不一致</summary>
    private const Int64 SecretTransitionWindow = 60_000;

    /// <summary>单个节点同步密钥的等待上限（毫秒）</summary>
    private const Int32 SecretSyncTimeout = 3_000;

    private Int64 _previousSecretExpire;

    /// <summary>轮换过渡期内的旧密钥。仅在过渡窗口内有效</summary>
    internal String? PreviousSecret { get; private set; }

    /// <summary>令牌是否为过渡窗口内仍然有效的旧密钥</summary>
    /// <param name="token">请求携带的令牌</param>
    /// <returns></returns>
    internal Boolean IsPreviousSecretValid(String? token)
    {
        var prev = PreviousSecret;
        if (prev.IsNullOrEmpty() || token.IsNullOrEmpty()) return false;

        return Runtime.TickCount64 <= _previousSecretExpire && token == prev;
    }

    /// <summary>集群密钥变更事件。参数：(oldSecret, newSecret)。订阅方可在此事件中通知其他节点或重新验证连接</summary>
    public event Action<String?, String?>? OnClusterSecretChanged;

    /// <summary>更新集群共享密钥（热更新）。更新后广播通知所有已连接节点，不影响已有已验证连接</summary>
    /// <param name="newSecret">新密钥。null 表示关闭密钥校验</param>
    /// <param name="sourceEndPoint">密钥变更来源节点。该节点已持有新密钥，无需重复通知，避免回环</param>
    /// <remarks>
    /// 同步窗口内各节点更新进度不一致：目标节点离线时无法自动同步，需恢复后人工同步密钥。
    /// 传入 null 关闭密钥校验不会广播，远端节点需人工清除密钥。
    /// </remarks>
    public void UpdateClusterSecret(String? newSecret, String? sourceEndPoint = null)
    {
        var oldSecret = _clusterSecret;
        if (oldSecret == newSecret) return;

        _clusterSecret = newSecret;

        // 过渡窗口：切换后一段时间内继续接受旧密钥，避免各节点更新进度不一致时相互拒绝
        if (!oldSecret.IsNullOrEmpty())
        {
            PreviousSecret = oldSecret;
            _previousSecretExpire = Runtime.TickCount64 + SecretTransitionWindow;
        }

        // 广播必须在本地节点令牌切换之前完成：目标节点尚未更新时，只能用旧令牌才被接受
        if (!oldSecret.IsNullOrEmpty() && !newSecret.IsNullOrEmpty())
        {
            var info = GetNodeInfo();
            foreach (var item in Nodes)
            {
                var node = item.Value;
                if (!sourceEndPoint.IsNullOrEmpty() && node.EndPoint.EqualIgnoreCase(sourceEndPoint)) continue;

                try
                {
                    // 受控同步等待：密钥轮换是低频管理操作，同步等待才能保证“先广播后切换”的时序确定
                    if (!node.UpdateRemoteSecret(info).Wait(SecretSyncTimeout))
                        WriteLog("通知节点[{0}]更新密钥超时，需人工同步", node.EndPoint);
                    else
                        WriteLog("节点[{0}]已同步新密钥", node.EndPoint);
                }
                catch (Exception ex)
                {
                    WriteLog("通知节点[{0}]更新密钥失败：{1}，需人工同步", node.EndPoint, ex.GetBaseException().Message);
                }
            }
        }

        WriteLog("集群共享密钥已更新：{0} -> {1}", oldSecret ?? "(null)", newSecret ?? "(null)");

        // 已连接节点改用新令牌；远端节点已在同步窗口内一并更新
        foreach (var node in Nodes.Values) node.UpdateSecret(newSecret);

        // 触发变更事件，允许外部监听方（如 ClusterNode 连接管理器）处理
        OnClusterSecretChanged?.Invoke(oldSecret, newSecret);
    }

    /// <summary>自动发现服务实例</summary>
    public ClusterDiscovery? Discovery { get; private set; }

    /// <summary>服务提供者。主要用于创建控制器实例，支持构造函数注入</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private HttpServer? _server;
    private ObjectContainer? _container;
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
    /// <remarks>AOT 下控制器动作经反射分发，需显式保留控制器公开构造与公开方法的元数据（见 Doc/AOT兼容性分析.md）</remarks>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods, typeof(ClusterController))]
    public void Start()
    {
        if (Name.IsNullOrEmpty()) Name = $"Cluster{Port}";

        ServiceProvider ??= ObjectContainer.Provider;

        WriteLog("创建集群服务 {0}，端口 {1}", Name, Port);

        // 控制器单例经容器解析，构造时注入本集群服务
        var container = new ObjectContainer();
        container.Register(typeof(ClusterController), null, new ClusterController { Server = this });

        var server = new HttpServer
        {
            Name = Name,
            Port = Port,
            ServiceProvider = container.BuildServiceProvider(),
            Tracer = Tracer,
            Log = Log,
        };

        // 集群接口挂在 /Cluster 下，如 POST /Cluster/Join；密钥经请求令牌校验
        var handler = new ClusterSecretHandler(this)
        {
            Inner = new ControllerHandler { ControllerType = typeof(ClusterController), Path = "/Cluster/" },
        };
        server.Map("/Cluster/*", handler);
        server.Start();

        Port = server.Port;
        _server = server;
        _container = container;

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

        // 启动布隆过滤器交换（如已注入）
        if (BloomFilter != null)
        {
            BloomFilter.Cluster = this;
            BloomFilter.Start();
        }

        // 启动批量合并转发管道（如已注入）
        if (BatchPipe != null)
        {
            BatchPipe.Cluster = this;
            BatchPipe.Start();
        }

        // 启动分片路由表（如已注入），关联到 ClusterExchange
        if (TopicRouter != null)
        {
            TopicRouter.Cluster = this;
            if (ClusterExchange != null) ClusterExchange.TopicRouter = TopicRouter;
            // 同步当前已知节点
            foreach (var node in Nodes)
            {
                TopicRouter.OnNodeJoined(node.Key);
            }
        }

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

        BloomFilter?.Stop();

        BatchPipe?.Stop();

        Discovery?.Stop();
        Discovery = null;

        _server.TryDispose();
        _server = null;

        _container.TryDispose();
        _container = null;

        PreviousSecret = null;
        _previousSecretExpire = 0;
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

            // 通知分片路由表节点离开
            TopicRouter?.OnNodeLeft(item);
        }

        var myNode = GetNodeInfo();

        // 向所有节点发送Ping
        foreach (var item in Nodes)
        {
            var node = item.Value;
            _ = node.Ping(myNode).ContinueWith(t =>
            {
                if (t.Exception != null)
                    WriteLog("Ping节点[{0}]失败：{1}", item.Key, t.Exception.GetBaseException().Message);
            }, TaskContinuationOptions.OnlyOnFaulted);
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

        var node = new ClusterNode { EndPoint = endpoint, Secret = ClusterSecret };
        if (!Nodes.TryAdd(endpoint, node)) return false;

        // 马上开始连接并心跳
        _timer?.SetNext(200);

        // 通知分片路由表新节点加入
        TopicRouter?.OnNodeJoined(endpoint);

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
            pm.Payload = new ArrayPacket(payload.GetBytes());
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

/// <summary>集群接口处理器。配置共享密钥时，校验请求令牌后再交给控制器路由</summary>
internal sealed class ClusterSecretHandler : IHttpHandler
{
    private readonly ClusterServer _server;

    /// <summary>实例化</summary>
    /// <param name="server">集群服务</param>
    public ClusterSecretHandler(ClusterServer server) => _server = server;

    /// <summary>内层处理器。密钥校验通过后执行</summary>
    public IHttpHandler? Inner { get; set; }

    /// <summary>处理请求</summary>
    /// <param name="context">Http 上下文</param>
    /// <returns>处理任务</returns>
    public async Task ProcessRequestAsync(IHttpContext context)
    {
        var secret = _server.ClusterSecret;
        if (!secret.IsNullOrEmpty())
        {
            var token = HttpTokenHelper.GetToken(context);

            // 密钥轮换过渡窗口内继续接受旧密钥，避免各节点更新进度不一致时相互拒绝
            if (token != secret && !_server.IsPreviousSecretValid(token)) throw new ApiException(ApiCode.Unauthorized, "集群共享密钥不匹配");
        }

        if (Inner != null) await Inner.ProcessRequestAsync(context).ConfigureAwait(false);

        // 集群 RPC 的响应统一包成 {code,data} 信封：Http 控制器把字符串结果原样写出，
        // 而顶层裸字符串不是合法 RFC JSON（FastJson 无法解析），必须放入对象成员
        var response = context.Response;
        var body = response.Body;
        if (response.StatusCode != HttpStatusCode.OK || body == null || body.Total == 0) return;

        var contentType = response.ContentType;
        var isJson = !contentType.IsNullOrEmpty() && contentType.StartsWithIgnoreCase("application/json");
        var json = isJson ? body.ToStr() : body.ToStr().ToJson();

        response.Body = (ArrayPacket)$"{{\"code\":0,\"data\":{json}}}".GetBytes();
        response.ContentType = "application/json";
    }
}
