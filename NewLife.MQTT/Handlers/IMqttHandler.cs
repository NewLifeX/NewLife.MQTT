using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT处理器</summary>
/// <returns></returns>
public interface IMqttHandler
{
    /// <summary>处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    MqttMessage? Process(MqttMessage message);

    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <returns></returns>
    Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce);

    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <param name="allowExchange">允许消息交换</param>
    /// <returns></returns>
    Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce);

    /// <summary>发布消息</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    Task<MqttIdMessage?> PublishAsync(PublishMessage message);

    /// <summary>关闭连接。网络连接被关闭时触发</summary>
    /// <param name="reason"></param>
    void Close(String reason);
}

/// <summary>MQTT处理器基类</summary>
/// <remarks>
/// 基类中各方法的默认实现主要是为了返回默认值。
/// 集成了 MQTT 5.0 会话能力、ACL 权限控制、消息重发和持久会话。
/// </remarks>
public class MqttHandler : IMqttHandler, ITracerFeature, ILogFeature
{
    /// <summary>网络会话</summary>
    public MqttSession Session { get; set; } = null!;

    /// <summary>消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>集群消息交换机</summary>
    public ClusterExchange? ClusterExchange { get; set; }

    /// <summary>编码器。决定对象存储序列化格式</summary>
    public IPacketEncoder Encoder { get; set; } = null!;

    /// <summary>认证器。可插拔的 ACL 权限控制</summary>
    public IMqttAuthenticator? Authenticator { get; set; }

    /// <summary>客户端标识</summary>
    private String? _clientId;

    /// <summary>遗嘱消息。客户端异常断开时需要发布</summary>
    private PublishMessage? _willMessage;

    /// <summary>是否正常断开。正常断开时清除遗嘱</summary>
    private Boolean _normalDisconnect;

    /// <summary>CleanSession 标志</summary>
    private Boolean _cleanSession = true;

    /// <summary>MQTT 协议版本</summary>
    private Byte _protocolLevel;

    /// <summary>MQTT 5.0 会话能力</summary>
    private MqttSessionCapabilities? _capabilities;

    /// <summary>Inflight 消息管理器（服务端消息重发）</summary>
    private InflightManager? _inflightManager;

    /// <summary>当前会话的订阅关系</summary>
    private readonly Dictionary<String, QualityOfService> _subscriptions = [];

    #region 接收消息
    /// <summary>处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public virtual MqttMessage? Process(MqttMessage message)
    {
        var rs = message.Type switch
        {
            MqttType.Connect => OnConnect((message as ConnectMessage)!),
            MqttType.Publish => OnPublish((message as PublishMessage)!),
            MqttType.PubRel => OnPublishRelease((message as PubRel)!),
            MqttType.PubRec => OnPublishReceive((message as PubRec)!),
            MqttType.PubAck => OnPublishAck((message as PubAck)!),
            MqttType.PubComp => OnPublishComplete((message as PubComp)!),
            MqttType.Subscribe => OnSubscribe((message as SubscribeMessage)!),
            MqttType.UnSubscribe => OnUnsubscribe((message as UnsubscribeMessage)!),
            MqttType.PingReq => OnPing((message as PingRequest)!),
            MqttType.Disconnect => OnDisconnect((message as DisconnectMessage)!),
            MqttType.Auth => OnAuth((message as AuthMessage)!),
            _ => null,
        };
        return rs;
    }

    /// <summary>客户端连接时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual ConnAck? OnConnect(ConnectMessage message)
    {
        _clientId = message.ClientId;
        _cleanSession = message.CleanSession;
        _protocolLevel = message.ProtocolLevel;

        // ACL 认证
        if (Authenticator != null)
        {
            var code = Authenticator.Authenticate(message.ClientId, message.Username, message.Password);
            if (code != ConnectReturnCode.Accepted)
                return new ConnAck { ReturnCode = code };
        }

        var exchange = Exchange;
        exchange?.Add(Session.ID, this);

        // 注册客户端标识映射（用于统计查询）
        if (exchange is MqttExchange mqttEx && !_clientId.IsNullOrEmpty())
            mqttEx.RegisterClientId(Session.ID, _clientId);

        // 保存遗嘱消息，用于异常断开时发布
        if (message.HasWill && !message.WillTopicName.IsNullOrEmpty())
        {
            _willMessage = new PublishMessage
            {
                Topic = message.WillTopicName,
                Payload = message.WillMessage != null ? (NewLife.Data.Packet)message.WillMessage : null,
                QoS = message.WillQualityOfService,
                Retain = message.WillRetain,
            };
        }

        // 初始化 Inflight 管理器（服务端消息重发）
        _inflightManager = new InflightManager(msg =>
        {
            Session?.SendMessage(msg);
            return Task.FromResult(0);
        });

        // MQTT 5.0 会话能力
        ConnAck ack;
        var sessionPresent = false;
        if (_protocolLevel >= 5)
        {
            _capabilities = new MqttSessionCapabilities();
            _capabilities.ApplyConnectProperties(message.Properties);

            // 构建 CONNACK 属性
            ack = new ConnAck
            {
                ReturnCode = ConnectReturnCode.Accepted,
                Properties = _capabilities.BuildConnAckProperties(),
            };
        }
        else
        {
            ack = new ConnAck { ReturnCode = ConnectReturnCode.Accepted };
        }

        // 处理 CleanSession / 持久会话
        if (!_cleanSession && exchange != null && !_clientId.IsNullOrEmpty())
        {
            // CleanSession=0，尝试恢复旧会话
            sessionPresent = exchange.RestorePersistentSession(_clientId, Session.ID);
        }
        else if (_cleanSession && exchange != null && !_clientId.IsNullOrEmpty())
        {
            // CleanSession=1，清除旧会话
            exchange.ClearPersistentSession(_clientId);
        }

        ack.SessionPresent = sessionPresent;

        return ack;
    }

    /// <summary>客户端断开时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage? OnDisconnect(DisconnectMessage message)
    {
        // 正常断开时标记为正常，Close 时不发布遗嘱消息
        _normalDisconnect = true;
        _willMessage = null;

        // CleanSession=0 时保存持久会话
        if (!_cleanSession && Exchange != null && !_clientId.IsNullOrEmpty())
            Exchange.SavePersistentSession(_clientId, Session.ID, _subscriptions);

        Exchange?.Remove(Session.ID);

        _inflightManager.TryDispose();

        // DISCONNECT 报文不需要响应，协议规定服务端收到后直接关闭连接
        return null;
    }

    /// <summary>收到心跳时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PingResponse? OnPing(PingRequest message) => new();

    /// <summary>收到增强认证消息时。MQTT 5.0</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage? OnAuth(AuthMessage message)
    {
        // 默认实现返回认证成功
        return new AuthMessage { ReasonCode = 0x00 };
    }

    /// <summary>收到发布消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttIdMessage? OnPublish(PublishMessage message)
    {
        // ACL 发布权限检查
        if (Authenticator != null && !Authenticator.AuthorizePublish(_clientId, message.Topic))
            return message.QoS == QualityOfService.AtLeastOnce ? new PubAck { Id = message.Id, ReasonCode = 0x87 } : null;

        // MQTT 5.0 主题别名解析
        if (_capabilities != null && !_capabilities.ResolveTopicAlias(message))
        {
            // 主题别名解析失败，协议错误
            return message.QoS == QualityOfService.AtLeastOnce ? new PubAck { Id = message.Id, ReasonCode = 0x82 } : null;
        }

        // 使用带 publisherSessionId 的重载以支持 NoLocal
        if (Exchange is MqttExchange mqttEx)
            mqttEx.Publish(message, Session.ID);
        else
            Exchange?.Publish(message);

        // 集群发布1，收到客户端发布消息
        ClusterExchange?.Publish(Session, message);

        return message.QoS switch
        {
            QualityOfService.AtMostOnce => null,
            QualityOfService.AtLeastOnce => message.CreateAck(),
            QualityOfService.ExactlyOnce => message.CreateReceive(),
            _ => null,
        };
    }

    /// <summary>收到发布释放消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubComp OnPublishRelease(PubRel message) => message.CreateComplete();

    /// <summary>收到发布已接收消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubRel OnPublishReceive(PubRec message) => message.CreateRelease();

    /// <summary>收到发布确认消息时（QoS 1 确认）</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage? OnPublishAck(PubAck message)
    {
        // 从 Inflight 队列中移除已确认的消息
        _inflightManager?.Acknowledge(message.Id);
        if (_capabilities != null) _capabilities.InflightCount--;
        return null;
    }

    /// <summary>收到发布完成消息时（QoS 2 第四步确认）</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage? OnPublishComplete(PubComp message)
    {
        // 从 Inflight 队列中移除已确认的消息
        _inflightManager?.Acknowledge(message.Id);
        if (_capabilities != null) _capabilities.InflightCount--;
        return null;
    }

    /// <summary>收到订阅请求时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual SubAck OnSubscribe(SubscribeMessage message)
    {
        if (message.Requests == null || message.Requests.Count == 0)
            return new SubAck { Id = message.Id };

        var grantedQos = new List<QualityOfService>();
        var exchange = Exchange;

        foreach (var item in message.Requests)
        {
            // ACL 订阅权限检查
            if (Authenticator != null && !Authenticator.AuthorizeSubscribe(_clientId, item.TopicFilter))
            {
                // 0x87 = Not Authorized，但 SubAck 中用 QoS 表示，128(0x80) 表示失败
                grantedQos.Add((QualityOfService)0x80);
                continue;
            }

            // 传递 MQTT 5.0 订阅选项
            if (exchange is MqttExchange mqttEx)
                mqttEx.Subscribe(Session.ID, item.TopicFilter, item.QualityOfService, item.NoLocal, item.RetainAsPublished, item.RetainHandling);
            else
                exchange?.Subscribe(Session.ID, item.TopicFilter, item.QualityOfService);

            _subscriptions[item.TopicFilter] = item.QualityOfService;
            grantedQos.Add(item.QualityOfService);
        }

        // 集群订阅1，接收订阅请求
        var exchange2 = ClusterExchange;
        exchange2?.Subscribe(Session, message);

        return new()
        {
            GrantedQos = grantedQos,
            Id = message.Id,
        };
    }

    /// <summary>收到取消订阅时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual UnsubAck OnUnsubscribe(UnsubscribeMessage message)
    {
        var exchange = Exchange;
        if (exchange != null && message.TopicFilters != null)
        {
            foreach (var item in message.TopicFilters)
            {
                exchange.Unsubscribe(Session.ID, item);
                _subscriptions.Remove(item);
            }
        }

        // 集群退订1
        var exchange2 = ClusterExchange;
        exchange2?.Unsubscribe(Session, message);

        return message.CreateAck();
    }
    #endregion

    #region 发送消息
    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <returns></returns>
    public Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        var pk = data as IPacket;
        if (pk == null && data != null) pk = Encoder.Encode(data);
        if (pk == null) throw new ArgumentNullException(nameof(data));

        var message = new PublishMessage
        {
            Topic = topic,
            Payload = pk,
            QoS = qos,
        };

        return PublishAsync(message);
    }

    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <param name="allowExchange">允许消息交换</param>
    /// <returns></returns>
    public Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        var pk = data as IPacket;
        if (pk == null && data != null) pk = Encoder.Encode(data);
        if (pk == null) throw new ArgumentNullException(nameof(data));

        var message = new PublishMessage
        {
            Topic = topic,
            Payload = pk,
            QoS = qos,
        };

        // 注意此处代码不要删除，是用来做消息转发给设备端之外的其他端使用的。
        if (allowExchange)
        {
            Exchange?.Publish(message);
            ClusterExchange?.Publish(Session, message);
        }

        return PublishAsync(message);
    }

    /// <summary>发布消息</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public async Task<MqttIdMessage?> PublishAsync(PublishMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        // MQTT 5.0 流控检查
        if (_capabilities != null && message.QoS > QualityOfService.AtMostOnce)
        {
            if (!_capabilities.CanSendQosMessage()) return null;
            _capabilities.InflightCount++;
        }

        // MQTT 5.0 主题别名分配
        _capabilities?.AssignTopicAlias(message);

        // QoS>0 的消息加入 Inflight 队列用于超时重发
        if (message.QoS > QualityOfService.AtMostOnce && _inflightManager != null && message.Id > 0)
            _inflightManager.Add(message.Id, message);

        var rs = (await SendAsync(message, message.QoS != QualityOfService.AtMostOnce).ConfigureAwait(false)) as MqttIdMessage;

        if (rs is PubRec rec)
        {
            var rel = new PubRel { Id = rec.Id };
            var cmp = (await SendAsync(rel, true).ConfigureAwait(false)) as PubComp;
            return cmp;
        }

        return rs;
    }

    private Int32 g_id;
    /// <summary>发送命令</summary>
    /// <param name="msg">消息</param>
    /// <param name="waitForResponse">是否等待响应</param>
    /// <returns></returns>
    protected virtual async Task<MqttMessage?> SendAsync(MqttMessage msg, Boolean waitForResponse = true)
    {
        if (msg is MqttIdMessage idm && idm.Id == 0 && (msg.Type != MqttType.Publish || msg.QoS > 0))
            idm.Id = (UInt16)Interlocked.Increment(ref g_id);

        // 如果MQTT连接已断开，则不再发送
        if (Session.Disposed) return null;

        // 性能埋点
        using var span = Tracer?.NewSpan($"mqtt:{msg.Type}:Send", msg);

        if (Log != null && Log.Level <= LogLevel.Debug)
        {
            if (msg is PublishMessage pm)
                WriteLog("=> {0} {1}", msg, pm.Payload?.ToStr());
            else
                WriteLog("=> {0}", msg);
        }

        var client = Session;
        try
        {
            // 断开消息没有响应
            if (!waitForResponse)
            {
                client.SendMessage(msg);
                return null;
            }

            var rs = await client.SendMessageAsync(msg).ConfigureAwait(false);

            if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("<= {0}", rs as MqttMessage);

            return rs as MqttMessage;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, msg);

            throw;
        }
    }
    #endregion

    #region 辅助
    /// <summary>关闭连接。网络连接被关闭时触发</summary>
    /// <param name="reason"></param>
    public virtual void Close(String reason)
    {
        // 异常断开时发布遗嘱消息
        if (!_normalDisconnect && _willMessage != null)
        {
            Exchange?.Publish(_willMessage);
            ClusterExchange?.Publish(Session, _willMessage);
            _willMessage = null;
        }

        // CleanSession=0 时保存持久会话
        if (!_cleanSession && Exchange != null && !_clientId.IsNullOrEmpty())
            Exchange.SavePersistentSession(_clientId, Session.ID, _subscriptions);

        _inflightManager.TryDispose();

        Exchange?.Remove(Session.ID);
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = null!;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttServer]{format}", args);
    #endregion
}