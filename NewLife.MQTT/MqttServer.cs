using System.Net;
using System.Security.Authentication;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Http;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.ProxyProtocol;
using NewLife.MQTT.Quic;
using NewLife.Net;
using NewLife.Serialization;

namespace NewLife.MQTT;

/// <summary>MQTT服务端</summary>
public class MqttServer : NetServer<MqttSession>
{
    /// <summary>消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>认证器。可插拔的 ACL 权限控制</summary>
    public IMqttAuthenticator? Authenticator { get; set; }

    /// <summary>连接速率限制器。基于 IP 的滑动窗口限流，防止单 IP 短时间大量连接</summary>
    public MqttConnectionRateLimiter? ConnectionRateLimiter { get; set; }

    /// <summary>消息速率限制器。基于令牌桶算法，按客户端控制消息发布速率</summary>
    public MqttMessageRateLimiter? MessageRateLimiter { get; set; }

    /// <summary>集群端口。指定后将自动创建集群</summary>
    public Int32 ClusterPort { get; set; }

    /// <summary>集群节点。服务启动时，自动加入这些节点到集群中</summary>
    public String[]? ClusterNodes { get; set; }

    /// <summary>集群服务端</summary>
    public ClusterServer? Cluster { get; set; }

    /// <summary>HTTP 管理端口。设置后将在该端口启动 HTTP 管理 API（stats/clients/topics/retained）。0 表示不启用</summary>
    public Int32 ManagementPort { get; set; }
    /// <summary>是否启用 Web 看板。需要先设置 <see cref="ManagementPort"/> 才生效。看板端口默认为 ManagementPort + 1</summary>
    public Boolean EnableDashboard { get; set; }
    /// <summary>HTTP 管理服务端</summary>
    public MqttManagementServer? Management { get; set; }

    /// <summary>是否启用 WebSocket 桥接。见 <see cref="WebSocketPort"/></summary>
    public Boolean EnableWebSocket { get; set; }

    /// <summary>WebSocket 桥接端口。设置非0值时自动启用；启用状态下设为0则由系统自动分配空闲端口</summary>
    /// <remarks>TCP 与 WebSocket 共用端口需要按首字节嗅探协议，v12 起改为独立端口，见 <see cref="WebSocketPath"/></remarks>
    public Int32 WebSocketPort { get; set; }

    /// <summary>WebSocket 桥接路径。默认 /mqtt</summary>
    public String WebSocketPath { get; set; } = "/mqtt";

    /// <summary>WebSocket 桥接证书。设置后桥接端口启用 TLS，客户端需用 wss:// 连接。为空表示明文 ws://</summary>
    public System.Security.Cryptography.X509Certificates.X509Certificate2? WebSocketCertificate { get; set; }

    /// <summary>WebSocket 桥接服务端</summary>
    public HttpServer? WebSocketServer { get; set; }

    /// <summary>编码器。决定对象存储序列化格式，默认json</summary>
    public IPacketEncoder Encoder { get; set; } = null!;

    /// <summary>QUIC 监听端口。设置为非 0 值（如 14567）时启动 QUIC 监听。需要 .NET 7+ 和操作系统 QUIC 支持。QUIC 强制 TLS，需同时设置 <see cref="QuicCertificate"/></summary>
    public Int32 QuicPort { get; set; }

    /// <summary>QUIC TLS 证书。QUIC 强制 TLS，启动 QUIC 监听必须指定证书。可使用 pfx 或 pem 证书文件</summary>
    public System.Security.Cryptography.X509Certificates.X509Certificate2? QuicCertificate { get; set; }

#if NET7_0_OR_GREATER
    /// <summary>QUIC 监听器</summary>
    private MqttQuicListener? _quicListener;
#endif

    /// <summary>会话接收缓冲区大小（字节）。默认 2048，避免默认 8KB 缓冲在小消息高吞吐场景产生大量大对象分配；小于等于 0 时使用框架全局默认</summary>
    /// <remarks>
    /// 消息大于缓冲时由 v12 消息泵跨轮拼帧，不影响正确性，仅影响单次读取的系统调用次数。
    /// 大载荷为主的场景（如文件切片传输）可调大以减少读取次数。
    /// </remarks>
    public Int32 SessionBufferSize { get; set; } = 2 * 1024;

    /// <summary>实例化MQTT服务器</summary>
    public MqttServer()
    {
        Port = 1883;
        ProtocolType = NetType.Tcp;
    }

    /// <summary>创建会话时应用 MQTT 专属接收缓冲大小</summary>
    /// <param name="session">底层Socket会话</param>
    /// <returns></returns>
    protected override INetSession CreateSession(ISocketSession session)
    {
        // 接收环挂在底层 Socket 会话上，必须在会话启动（建立接收环）之前设置
        if (SessionBufferSize > 0 && session is SessionBase sb) sb.BufferSize = SessionBufferSize;

        return base.CreateSession(session);
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => Name ?? base.ToString();

    /// <summary>启动</summary>
    protected override void OnStart()
    {
        var provider = (ServiceProvider ?? ObjectContainer.Provider) ?? throw new NotSupportedException("未配置服务提供者ServiceProvider");

        if (Name.IsNullOrEmpty() || Name == "Mqtt") Name = $"Mqtt{Port}";

        //JsonHost ??= ServiceProvider.GetService<IJsonHost>() ?? JsonHelper.Default;
        Encoder ??= provider.GetService<IPacketEncoder>() ?? new DefaultPacketEncoder();
        if (Encoder is DefaultPacketEncoder encoder)
        {
            var jsonHost = provider.GetService<IJsonHost>();
            if (jsonHost != null) encoder.JsonHost = jsonHost;

            // 较大Int64转字符串，避免精度丢失。针对Web和嵌入式设备
            encoder.JsonHost.Options.Int64AsString = true;
        }

        // 初始化认证器
        Authenticator ??= provider.GetService<IMqttAuthenticator>();

        var exchange = Exchange;
        exchange ??= provider.GetService<IMqttExchange>();
        exchange ??= new MqttExchange();

        if (exchange is ITracerFeature feature)
            feature.Tracer ??= Tracer;

        Exchange = exchange;

        // v12 协议泵：MQTT 帧编解码随监听服务下发到各会话；PROXY 头由编解码器定界为独立帧
        // 注意：HTTP/WebSocket 接入不再走管线编解码器，由独立 HttpServer 桥接（见 MqttWebSocketHandler）
        Protocol = new MqttCodec();

        base.OnStart();

        // 创建集群
        CreateCluster();

        // 创建管理服务
        CreateManagement();

        // 创建WebSocket桥接
        CreateWebSocket();

#if NET7_0_OR_GREATER
        // 启动 QUIC 监听器
        CreateQuicListener();
#endif
    }

    /// <summary>创建集群</summary>
    protected virtual void CreateCluster()
    {
        var cluster = Cluster;
        var nodes = ClusterNodes;
        if (cluster == null && (nodes != null && nodes.Length > 0 || ClusterPort > 0))
            cluster = Cluster = new ClusterServer { Port = ClusterPort };

        if (cluster != null)
        {
            var provider = (ServiceProvider ?? ObjectContainer.Provider) ?? throw new NotSupportedException("未配置服务提供者ServiceProvider");
            var exchange = Exchange ?? throw new NotSupportedException("未配置消息交换机Exchange");

            WriteLog("创建MQTT集群 {0}，端口 {1}", cluster.Name, cluster.Port);

            // 启动集群服务
            cluster.ServiceProvider = provider;
            cluster.Log = Log;
            cluster.Start();

            ClusterPort = cluster.Port;
            //Name = $"Mqtt{Port}-Cluster{ClusterPort}";

            // 添加集群节点
            if (nodes != null && nodes.Length > 0)
            {
                foreach (var item in nodes)
                {
                    cluster.AddNode(item);
                }
            }

            // 创建集群交换机
            var exchange2 = provider?.GetService<ClusterExchange>();
            exchange2 ??= new ClusterExchange();

            exchange2.Cluster = cluster;
            exchange2.Tracer = Tracer;

            cluster.Exchange = exchange;
            cluster.ClusterExchange = exchange2;

            // 设置 MqttExchange 的集群交换机引用，使会话清理时能广播退订
            if (exchange is MqttExchange mqttExchange)
                mqttExchange.ClusterExchange = exchange2;
        }
    }

    /// <summary>创建 HTTP 管理服务</summary>
    protected virtual void CreateManagement()
    {
        if (ManagementPort <= 0) return;

        var exchange = Exchange as MqttExchange;
        if (exchange == null) return;

        var mgmt = Management ?? new MqttManagementServer();
        mgmt.Port = ManagementPort;
        mgmt.Exchange = exchange;
        mgmt.EnableDashboard = EnableDashboard;
        mgmt.Log = Log;
        mgmt.Tracer = Tracer;
        mgmt.Start();

        Management = mgmt;
    }

    /// <summary>创建 WebSocket 桥接服务。MQTT over WebSocket 由独立 HTTP 服务承载</summary>
    protected virtual void CreateWebSocket()
    {
        if (!EnableWebSocket && WebSocketPort <= 0) return;

        var provider = (ServiceProvider ?? ObjectContainer.Provider) ?? throw new NotSupportedException("未配置服务提供者ServiceProvider");

        WriteLog("创建WebSocket桥接，端口 {0}，路径 {1}", WebSocketPort, WebSocketPath);

        var server = new HttpServer
        {
            Port = WebSocketPort,
            ServiceProvider = provider,
            Log = Log,
            Tracer = Tracer,
        };

        // 配置证书后桥接端口即 WSS：TLS 由底层 TCP 服务提供
        if (WebSocketCertificate != null)
        {
            server.Certificate = WebSocketCertificate;
            server.SslProtocol = SslProtocol != SslProtocols.None ? SslProtocol : SslProtocols.Tls12;
        }

        server.Map(WebSocketPath, new MqttWebSocketHandler(this));
        server.Start();

        WebSocketPort = server.Port;
        WebSocketServer = server;
    }

#if NET7_0_OR_GREATER
    /// <summary>创建 QUIC 监听器</summary>
    protected virtual void CreateQuicListener()
    {
        if (QuicPort <= 0) return;

        if (QuicCertificate == null) throw new InvalidOperationException("启动 QUIC 监听必须指定 QuicCertificate，QUIC 强制 TLS");

        WriteLog("创建 QUIC 监听器，端口 {0}", QuicPort);

        var provider = (ServiceProvider ?? ObjectContainer.Provider) ?? throw new NotSupportedException("未配置服务提供者ServiceProvider");
        var exchange = Exchange ?? throw new NotSupportedException("未配置消息交换机Exchange");

        var listener = new MqttQuicListener
        {
            Port = QuicPort,
            Certificate = QuicCertificate,
            Log = Log,
            Tracer = Tracer,
        };

        listener.ConnectionReceived += (sender, e) =>
        {
            WriteLog("QUIC 新连接: {0}", e.Connection.RemoteEndPoint);

            // 处理器可空，由协议会话按默认规则创建；交换机/集群/认证等由 MqttSession 统一注入
            var session = new MqttQuicSession(e.Connection, e.Stream)
            {
                Handler = provider.GetService<IMqttHandler>(),
                Log = Log,
                Tracer = Tracer,
            };

            session.Start(this);
        };

        _ = listener.StartAsync().ConfigureAwait(false);
        _quicListener = listener;
    }
#endif

    /// <summary>停止</summary>
    /// <param name="reason"></param>
    protected override void OnStop(String? reason)
    {
        Management.TryDispose();
        Cluster.TryDispose();
        WebSocketServer.TryDispose();

#if NET7_0_OR_GREATER
        if (_quicListener != null)
        {
            _quicListener.StopAsync().Wait(5000);
            _quicListener.TryDispose();
            _quicListener = null;
        }
#endif

        base.OnStop(reason);
    }
}

/// <summary>会话</summary>
public class MqttSession : NetSession<MqttServer>
{
    /// <summary>远程地址信息。经过代理之前的地址</summary>
    public new NetUri Remote { get; set; } = null!;

    /// <summary>指令处理器</summary>
    public IMqttHandler MqttHandler { get; set; } = null!;

    /// <summary>当前连接协议版本。CONNECT 解码后确定，供帧编解码使用</summary>
    public MqttVersion ProtocolLevel { get; set; } = MqttVersion.V311;

    /// <summary>会话标识。TCP 会话取底层连接编号，WebSocket 桥接会话取负数自增编号，避免两者冲突</summary>
    public Int32 SessionId => ID != 0 ? ID : _wsId;

    /// <summary>发送委托。WebSocket 桥接等没有协议泵的场景设置，发送走该委托而不是底层连接</summary>
    public Func<IMessage, Int32>? SendTransport { get; set; }

    /// <summary>等待响应超时时间（毫秒）。WebSocket 桥接模式没有协议泵配对，由会话自行超时</summary>
    public Int32 ResponseTimeout { get; set; } = 5_000;

    private static Int32 _wsSeed;
    private Int32 _wsId;

    /// <summary>挂起的请求。WebSocket 桥接模式没有协议泵配对，需自行登记等待队列并在接收时配对</summary>
    private readonly List<PendingFrame> _pending = [];

    private sealed class PendingFrame
    {
        public MqttFrame Request = null!;

#if NET45
        // net45 没有 RunContinuationsAsynchronously，接受同步续体
        public TaskCompletionSource<IMessage> Source = new();
#else
        public TaskCompletionSource<IMessage> Source = new(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
    }

    /// <summary>MQTT 消息工厂</summary>
    private readonly MqttFactory _factory = new();

    /// <summary>帧配对器。WebSocket 桥接模式复用协议编解码器的配对规则</summary>
    private readonly MqttCodec _matchCodec = new();

    /// <summary>设备连接时，准备处理器</summary>
    /// <exception cref="NotSupportedException"></exception>
    protected override void OnConnected()
    {
        // 先取远端地址。连接速率限制等逻辑需要使用经过代理之前的客户端地址，
        // WebSocket 桥接可能没有底层传输会话，此时保留调用方设置的占位地址
        if (Session != null) Remote = base.Remote;

        if (!InitHandler(Host)) return;

        // v12 协议泵：帧体跨轮到达时会被绑定为流式体，需开启匹配队列以确保交付前物化为内存，供同步解码
        if (Session is SessionBase sb) sb.MatchQueue ??= new DefaultMatchQueue();

        base.OnConnected();
    }

    /// <summary>为 WebSocket 桥接初始化会话。没有底层连接协议泵，发送走委托，配对由会话自行完成</summary>
    /// <param name="host">MQTT 服务端</param>
    /// <param name="transport">底层传输会话，用于会话标识与生命周期。可为空</param>
    /// <param name="send">发送委托，把 MQTT 帧写到 WebSocket</param>
    /// <returns>是否初始化成功，连接速率超限时为false</returns>
    public Boolean AttachWebSocket(MqttServer host, ISocketSession? transport, Func<IMessage, Int32> send)
    {
        _wsId = -Interlocked.Increment(ref _wsSeed);
        SendTransport = send;

        if (transport != null)
            Session = transport;
        else
            Remote = new NetUri();

        Log = host.SessionLog;

        // 走 NetSession 标准启动流程。直接调用 base.OnConnected 会绕过 _running 闸门，
        // 使后续 Close/Dispose 变成空操作，会话无法从交换机注销，也无法回收
        Host = host;
        Start();

        // 速率超限时 InitHandler 已经关闭会话
        return MqttHandler != null;
    }

#if NET7_0_OR_GREATER
    /// <summary>为 QUIC 桥接初始化会话。没有协议泵，发送写 QUIC 流，配对由会话自行完成</summary>
    /// <param name="host">MQTT 服务端</param>
    /// <param name="remote">远端地址，用于会话标识与速率限制</param>
    /// <param name="send">发送委托，把 MQTT 帧写到 QUIC 流</param>
    /// <returns>是否初始化成功，连接速率超限时为false</returns>
    public Boolean AttachQuic(MqttServer host, String? remote, Func<IMessage, Int32> send)
    {
        _wsId = -Interlocked.Increment(ref _wsSeed);
        SendTransport = send;

        Remote = !remote.IsNullOrEmpty() ? new NetUri($"udp://{remote}") : new NetUri();

        Log = host.SessionLog;

        // 走 NetSession 标准启动流程，交换机注销与资源回收依赖 _running 闸门
        Host = host;
        Start();

        // 速率超限时 InitHandler 已经关闭会话
        return MqttHandler != null;
    }
#endif

    /// <summary>准备指令处理器</summary>
    /// <param name="host">MQTT 服务端</param>
    /// <returns>是否通过连接速率限制</returns>
    private Boolean InitHandler(MqttServer? host)
    {
        // 连接速率限制检查
        if (host?.ConnectionRateLimiter != null)
        {
            var ip = Remote?.Address?.ToString();
            if (!ip.IsNullOrEmpty() && !host.ConnectionRateLimiter.IsConnectionAllowed(ip))
            {
                WriteLog("IP[{0}] 连接速率超限，拒绝连接", ip);
                Close("连接速率超限");
                return false;
            }
        }

        var handler = MqttHandler;
        handler ??= ServiceProvider?.GetService<IMqttHandler>();
        handler ??= new MqttHandler();

        if (handler is MqttHandler mqttHandler)
        {
            mqttHandler.Session = this;
            mqttHandler.Log = host?.SessionLog;
            mqttHandler.Tracer = host?.Tracer;
            mqttHandler.Exchange = host?.Exchange;
            mqttHandler.ClusterExchange = host?.Cluster?.ClusterExchange;
            mqttHandler.Encoder = host?.Encoder;
            mqttHandler.Authenticator = host?.Authenticator;
            mqttHandler.ConnectionRateLimiter = host?.ConnectionRateLimiter;
            mqttHandler.MessageRateLimiter = host?.MessageRateLimiter;
        }

        MqttHandler = handler;

        return true;
    }

    /// <summary>客户端连接已断开</summary>
    /// <param name="reason"></param>
    protected override void OnDisconnected(String reason)
    {
        MqttHandler?.Close(reason);

        // 唤醒所有等待者，避免断开后调用方一直等到超时
        lock (_pending)
        {
            foreach (var item in _pending) item.Source.TrySetCanceled();
            _pending.Clear();
        }

        base.OnDisconnected(reason);
    }

    /// <summary>接收指令</summary>
    /// <param name="e"></param>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        Process(e.Message as IMessage, e.Remote, e.Packet);

        // 父级 OnReceive 触发事件，调用 NetServer.OnReceive
        base.OnReceive(e);
    }

    /// <summary>处理协议帧。TCP 接收与 WebSocket 桥接共用同一条处理链</summary>
    /// <param name="message">协议帧</param>
    /// <param name="remote">远端地址</param>
    /// <param name="packet">原始数据包，仅用于异常日志</param>
    public void Process(IMessage? message, IPEndPoint? remote = null, IPacket? packet = null)
    {
        // 代理头帧：修正客户端地址后结束本轮
        if (message is MqttProxyFrame proxy)
        {
            var client = proxy.Client;
            if (client != null) Remote.EndPoint = client.EndPoint;

            return;
        }

        var debug = XTrace.Log.Level <= LogLevel.Debug;

        var msg = Decode(message);
        if (msg is ConnectMessage connect) ProtocolLevel = connect.ProtocolLevel;

        if (debug) WriteLog("<={0}", msg);
        if (msg == null) return;

        // WebSocket 桥接没有协议泵，响应需在此唤醒等待方
        if (msg.Reply && TryMatchPending(message!)) return;

        MqttMessage? result = null;
        using var span = Host?.Tracer?.NewSpan($"mqtt:{msg.Type}", msg);
        try
        {
            // 在连接指令中，修正远程地址，替换为经过代理之前的地址
            if (msg.Type is MqttType.Connect && remote != null) Remote.EndPoint = remote;

            // 执行处理器
            result = MqttHandler?.Process(msg);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            var hex = packet?.ToHex(1024);
            span?.SetError(ex, hex);

            if (!hex.IsNullOrEmpty()) XTrace.WriteLine(hex);
        }

        // 处理响应
        if (result != null)
        {
            // 匹配Id
            if (result is MqttIdMessage response && response.Id == 0 && msg is MqttIdMessage request) response.Id = request.Id;

            if (debug) WriteLog("=> {0}", result);

            SendMessage(result);
        }

        if (msg.Type == MqttType.Disconnect) Dispose();
    }

    /// <summary>解码协议帧为 MQTT 消息</summary>
    /// <param name="message">协议帧或 MQTT 消息</param>
    /// <returns></returns>
    public MqttMessage? Decode(Object? message) => message switch
    {
        MqttFrame frame => _factory.ReadMessage(frame, ProtocolLevel),
        MqttMessage msg => msg,
        _ => null,
    };

    /// <summary>包装 MQTT 消息为协议帧</summary>
    /// <param name="message">MQTT 消息</param>
    /// <returns></returns>
    public MqttFrame Encode(MqttMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        var flag = (Byte)((Byte)message.Type << 4);

        return new MqttFrame
        {
            Mqtt = message,
            Version = ProtocolLevel,
            FirstByte = flag,
            Kind = MqttFrame.GetKind(flag),
        };
    }

    /// <summary>发送 MQTT 消息。包装为帧后经协议泵或发送委托发出</summary>
    /// <param name="message">MQTT 消息</param>
    /// <returns>发送字节数</returns>
    public Int32 SendMessage(MqttMessage message)
    {
        var frame = Encode(message);

        // WebSocket 桥接没有协议泵，走发送委托
        if (SendTransport != null) return SendTransport(frame);

        return SendMessage((Object)frame);
    }

    /// <summary>发送 MQTT 消息，可选等待匹配的响应消息</summary>
    /// <param name="message">消息</param>
    /// <param name="waitForResponse">是否等待响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息；不等待或无响应时为null</returns>
    public async ValueTask<MqttMessage?> SendMessageAsync(MqttMessage message, Boolean waitForResponse, CancellationToken cancellationToken = default)
    {
        var frame = Encode(message);

        // WebSocket 桥接没有协议泵，自行登记等待队列，由接收链配对唤醒
        if (SendTransport != null)
        {
            if (!waitForResponse)
            {
                SendTransport(frame);
                return null;
            }

            var pending = new PendingFrame { Request = frame };
            lock (_pending) _pending.Add(pending);

            using var timeout = new CancellationTokenSource(ResponseTimeout);
            try
            {
                SendTransport(frame);

                using var reg = timeout.Token.Register(() => pending.Source.TrySetCanceled());
                var rep = await pending.Source.Task.ConfigureAwait(false);

                return Decode(rep);
            }
            finally
            {
                lock (_pending) _pending.Remove(pending);
            }
        }

        if (!waitForResponse)
        {
            SendMessage((Object)frame);
            return null;
        }

        var rs = await base.SendMessageAsync(frame, cancellationToken).ConfigureAwait(false);

        return Decode(rs);
    }

    /// <summary>把响应帧交给等待方。配对规则与协议泵一致</summary>
    /// <param name="frame">收到的响应帧</param>
    /// <returns>是否已配对</returns>
    private Boolean TryMatchPending(IMessage frame)
    {
        PendingFrame? pending = null;
        lock (_pending)
        {
            foreach (var item in _pending)
            {
                if (_matchCodec.Match(item.Request, frame))
                {
                    pending = item;
                    break;
                }
            }

            if (pending != null) _pending.Remove(pending);
        }

        return pending != null && pending.Source.TrySetResult(frame);
    }
}