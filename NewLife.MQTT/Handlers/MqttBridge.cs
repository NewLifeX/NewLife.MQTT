using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Security;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT 桥接规则</summary>
/// <remarks>
/// 定义本地主题与远端 Broker 之间的消息转发规则。
/// 支持双向桥接：本地消息转发到远端（out），远端消息转发到本地（in）。
/// </remarks>
public class MqttBridgeRule
{
    /// <summary>本地主题过滤器。匹配本地主题则转发到远端</summary>
    public String LocalTopic { get; set; } = "#";

    /// <summary>远端主题过滤器。匹配远端主题则转发到本地</summary>
    public String RemoteTopic { get; set; } = "#";

    /// <summary>桥接方向</summary>
    public BridgeDirection Direction { get; set; } = BridgeDirection.Both;

    /// <summary>主题前缀映射。转发时添加的前缀，为空则保持原主题</summary>
    public String? TopicPrefix { get; set; }

    /// <summary>转发消息的 QoS 上限。默认不限制</summary>
    public QualityOfService MaxQoS { get; set; } = QualityOfService.ExactlyOnce;
}

/// <summary>桥接方向</summary>
public enum BridgeDirection
{
    /// <summary>仅从本地转发到远端</summary>
    Out,

    /// <summary>仅从远端转发到本地</summary>
    In,

    /// <summary>双向转发</summary>
    Both,
}

/// <summary>MQTT 桥接器。连接远端 Broker 实现消息桥接转发</summary>
/// <remarks>
/// 在本地 Broker 和远端 Broker 之间建立消息桥梁，
/// 根据配置的规则将匹配主题的消息在两端之间转发。
/// 支持断线重连，复用 MqttClient 的重连能力。
/// </remarks>
public class MqttBridge : DisposeBase
{
    #region 属性
    /// <summary>桥接名称</summary>
    public String Name { get; set; } = "Bridge";

    /// <summary>远端 Broker 地址。如 tcp://remote:1883</summary>
    public String? RemoteServer { get; set; }

    /// <summary>远端 Broker 客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>远端 Broker 用户名</summary>
    public String? UserName { get; set; }

    /// <summary>远端 Broker 密码</summary>
    public String? Password { get; set; }

    /// <summary>桥接规则列表</summary>
    public IList<MqttBridgeRule> Rules { get; set; } = [];

    /// <summary>本地消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>远端客户端</summary>
    private MqttClient? _remoteClient;

    /// <summary>是否已启动</summary>
    private Boolean _started;
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _remoteClient.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>启动桥接</summary>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        if (RemoteServer.IsNullOrEmpty()) throw new ArgumentNullException(nameof(RemoteServer));

        WriteLog("启动 MQTT 桥接 [{0}] -> {1}", Name, RemoteServer);

        var client = new MqttClient
        {
            Name = $"Bridge_{Name}",
            Server = RemoteServer,
            ClientId = ClientId ?? $"bridge_{Name}_{Rand.Next()}",
            UserName = UserName,
            Password = Password,
            Reconnect = true,
            CleanSession = false,
            Log = Log,
            Tracer = Tracer,
        };

        client.Received += OnRemoteReceived;

        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        // 订阅远端主题（用于 In 方向桥további接）
        foreach (var rule in Rules)
        {
            if (rule.Direction is BridgeDirection.In or BridgeDirection.Both)
            {
                await client.SubscribeAsync(rule.RemoteTopic, cancellationToken: cancellationToken).ConfigureAwait(false);
                WriteLog("桥接订阅远端主题: {0}", rule.RemoteTopic);
            }
        }

        _remoteClient = client;
        _started = true;

        WriteLog("MQTT 桥接 [{0}] 启动完成", Name);
    }

    /// <summary>停止桥接</summary>
    /// <returns></returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started) return;

        WriteLog("停止 MQTT 桥接 [{0}]", Name);

        if (_remoteClient != null)
        {
            await _remoteClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            _remoteClient.TryDispose();
            _remoteClient = null;
        }

        _started = false;
    }

    /// <summary>转发本地消息到远端（由 Exchange 调用）</summary>
    /// <param name="message">本地发布的消息</param>
    public void ForwardToRemote(PublishMessage message)
    {
        if (!_started || _remoteClient == null) return;

        foreach (var rule in Rules)
        {
            if (rule.Direction is not (BridgeDirection.Out or BridgeDirection.Both)) continue;
            if (!MqttTopicFilter.IsMatch(message.Topic, rule.LocalTopic)) continue;

            var remoteTopic = rule.TopicPrefix.IsNullOrEmpty() ? message.Topic : $"{rule.TopicPrefix}{message.Topic}";
            var qos = message.QoS > rule.MaxQoS ? rule.MaxQoS : message.QoS;

            var msg = new PublishMessage
            {
                Topic = remoteTopic,
                Payload = message.Payload,
                QoS = qos,
                Retain = message.Retain,
            };

            _ = _remoteClient.PublishAsync(msg);
            break;
        }
    }

    /// <summary>收到远端消息，转发到本地</summary>
    private void OnRemoteReceived(Object? sender, EventArgs<PublishMessage> e)
    {
        var message = e.Arg;
        var exchange = Exchange;
        if (exchange == null) return;

        foreach (var rule in Rules)
        {
            if (rule.Direction is not (BridgeDirection.In or BridgeDirection.Both)) continue;
            if (!MqttTopicFilter.IsMatch(message.Topic, rule.RemoteTopic)) continue;

            var localTopic = rule.TopicPrefix.IsNullOrEmpty() ? message.Topic : $"{rule.TopicPrefix}{message.Topic}";
            var qos = message.QoS > rule.MaxQoS ? rule.MaxQoS : message.QoS;

            var msg = new PublishMessage
            {
                Topic = localTopic,
                Payload = message.Payload,
                QoS = qos,
                Retain = message.Retain,
            };

            exchange.Publish(msg);
            break;
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
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttBridge]{format}", args);
    #endregion
}
