using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Serialization;

namespace NewLife.MQTT;

/// <summary>MQTT服务端</summary>
public class MqttServer : NetServer<MqttSession>
{
    /// <summary>消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>集群端口。指定后将自动创建集群</summary>
    public Int32 ClusterPort { get; set; }

    /// <summary>集群节点。服务启动时，自动加入这些节点到集群中</summary>
    public String[]? ClusterNodes { get; set; }

    /// <summary>集群服务端</summary>
    public ClusterServer? Cluster { get; set; }

    /// <summary>编码器。决定对象存储序列化格式，默认json</summary>
    public IPacketEncoder Encoder { get; set; } = null!;

    /// <summary>实例化MQTT服务器</summary>
    public MqttServer() => Port = 1883;

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => Name ?? base.ToString();

    /// <summary>启动</summary>
    protected override void OnStart()
    {
        if (ServiceProvider == null) throw new NotSupportedException("未配置服务提供者ServiceProvider");

        Name = $"Mqtt{Port}";

        //JsonHost ??= ServiceProvider.GetService<IJsonHost>() ?? JsonHelper.Default;
        Encoder ??= ServiceProvider.GetService<IPacketEncoder>() ?? new DefaultPacketEncoder();
        if (Encoder is DefaultPacketEncoder encoder)
        {
            var jsonHost = ServiceProvider.GetService<IJsonHost>();
            if (jsonHost != null) encoder.JsonHost = jsonHost;
        }

        var exchange = Exchange;
        exchange ??= ServiceProvider.GetService<IMqttExchange>();
        exchange ??= new MqttExchange();

        if (exchange is ITracerFeature feature)
            feature.Tracer ??= Tracer;

        Exchange = exchange;

        // 创建集群
        CreateCluster();

        Add(new MqttCodec());

        base.OnStart();
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
            var exchange = Exchange ?? throw new NotSupportedException("未配置消息交换机Exchange");

            // 启动集群服务
            cluster.ServiceProvider = ServiceProvider;
            cluster.Log = Log;
            cluster.Start();

            ClusterPort = cluster.Port;
            Name = $"Mqtt{Port}-Cluster{ClusterPort}";

            // 添加集群节点
            if (nodes != null && nodes.Length > 0)
            {
                foreach (var item in nodes)
                {
                    cluster.AddNode(item);
                }
            }

            // 创建集群交换机
            var exchange2 = ServiceProvider?.GetService<ClusterExchange>();
            exchange2 ??= new ClusterExchange();

            exchange2.Cluster = cluster;
            exchange2.Tracer = Tracer;

            cluster.Exchange = exchange;
            cluster.ClusterExchange = exchange2;
        }
    }

    /// <summary>停止</summary>
    /// <param name="reason"></param>
    protected override void OnStop(String? reason)
    {
        Cluster.TryDispose();

        base.OnStop(reason);
    }
}

/// <summary>会话</summary>
public class MqttSession : NetSession<MqttServer>
{
    /// <summary>指令处理器</summary>
    public IMqttHandler MqttHandler { get; set; } = null!;

    /// <summary>设备连接时，准备处理器</summary>
    /// <exception cref="NotSupportedException"></exception>
    protected override void OnConnected()
    {
        var handler = MqttHandler;
        handler ??= ServiceProvider?.GetService<IMqttHandler>();
        handler ??= new MqttHandler();
        if (handler == null) throw new NotSupportedException("未注册指令处理器");

        if (handler is MqttHandler mqttHandler)
        {
            mqttHandler.Session = this;
            mqttHandler.Exchange = Host.Exchange;
            mqttHandler.ClusterExchange = Host.Cluster?.ClusterExchange;
            mqttHandler.Encoder = Host.Encoder;
        }

        MqttHandler = handler;

        base.OnConnected();
    }

    /// <summary>客户端连接已断开</summary>
    /// <param name="reason"></param>
    protected override void OnDisconnected(String reason)
    {
        MqttHandler?.Close(reason);

        base.OnDisconnected(reason);
    }

    /// <summary>接收指令</summary>
    /// <param name="e"></param>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        var debug = XTrace.Log.Level <= LogLevel.Debug;
        var msg = e.Message as MqttMessage;

        if (debug) WriteLog("<={0}", msg);
        if (msg != null)
        {
            MqttMessage? result = null;
            using var span = Host.Tracer?.NewSpan($"mqtt:{msg.Type}", msg);
            try
            {
                // 执行处理器
                result = MqttHandler?.Process(msg);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);

                var hex = e.Packet?.ToHex(1024);
                span?.SetError(ex, hex);

                if (!hex.IsNullOrEmpty()) XTrace.WriteLine(hex);
            }

            // 处理响应
            if (result != null)
            {
                // 匹配Id
                if (result is MqttIdMessage response && response.Id == 0 && msg is MqttIdMessage request) response.Id = request.Id;

                if (debug) WriteLog("=> {0}", result);

                Session.SendMessage(result);
            }
        }

        // 父级 OnReceive 触发事件，调用 NetServer.OnReceive
        base.OnReceive(e);

        if (msg != null && msg.Type == MqttType.Disconnect) Dispose();
    }
}