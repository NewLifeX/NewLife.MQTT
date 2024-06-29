using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Serialization;

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
/// </remarks>
public class MqttHandler : IMqttHandler, ITracerFeature, ILogFeature
{
    /// <summary>网络会话</summary>
    public INetSession Session { get; set; } = null!;

    /// <summary>消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>集群消息交换机</summary>
    public ClusterExchange? ClusterExchange { get; set; }

    /// <summary>编码器。决定对象存储序列化格式</summary>
    public IPacketEncoder Encoder { get; set; } = null!;

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
            MqttType.Subscribe => OnSubscribe((message as SubscribeMessage)!),
            MqttType.UnSubscribe => OnUnsubscribe((message as UnsubscribeMessage)!),
            MqttType.PingReq => OnPing((message as PingRequest)!),
            MqttType.Disconnect => OnDisconnect((message as DisconnectMessage)!),
            _ => null,
        };
        return rs;
    }

    /// <summary>客户端连接时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual ConnAck? OnConnect(ConnectMessage message)
    {
        Exchange?.Add(Session.ID, this);

        return new() { ReturnCode = ConnectReturnCode.Accepted };
    }

    /// <summary>客户端断开时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttMessage? OnDisconnect(DisconnectMessage message)
    {
        Exchange?.Remove(Session.ID);

        return null;
    }

    /// <summary>收到心跳时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PingResponse? OnPing(PingRequest message) => new();

    /// <summary>收到发布消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual MqttIdMessage? OnPublish(PublishMessage message)
    {
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

    /// <summary>收到发布消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubComp OnPublishRelease(PubRel message) => message.CreateComplete();

    /// <summary>收到发布已接收消息时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual PubRel OnPublishReceive(PubRec message) => message.CreateRelease();

    /// <summary>收到订阅请求时</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    protected virtual SubAck OnSubscribe(SubscribeMessage message)
    {
        if (message.Requests == null || message.Requests.Count == 0)
            return new SubAck { Id = message.Id };

        var exchange = Exchange;
        if (exchange != null)
        {
            foreach (var item in message.Requests)
            {
                exchange.Subscribe(Session.ID, item.TopicFilter, item.QualityOfService);
            }
        }

        // 集群订阅1，接收订阅请求
        var exchange2 = ClusterExchange;
        exchange2?.Subscribe(Session, message);

        return new()
        {
            GrantedQos = message.Requests.Select(x => x.QualityOfService).ToList(),
            Id = message.Id,
            //QoS = message.QoS
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
    public async Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        var pk = data as Packet;
        if (pk == null && data != null) pk = Encoder.Encode(data);
        if (pk == null) throw new ArgumentNullException(nameof(data));

        var message = new PublishMessage
        {
            Topic = topic,
            Payload = pk,
            QoS = qos,
        };

        return await PublishAsync(message);
    }

    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <param name="allowExchange">允许消息交换</param>
    /// <returns></returns>
    public async Task<MqttIdMessage?> PublishAsync(String topic, Object data, Boolean allowExchange, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        var pk = data as Packet;
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

        return await PublishAsync(message);
    }

    /// <summary>发布消息</summary>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public async Task<MqttIdMessage?> PublishAsync(PublishMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        var rs = (await SendAsync(message, message.QoS != QualityOfService.AtMostOnce)) as MqttIdMessage;

        if (rs is PubRec)
        {
            var rel = new PubRel();
            var cmp = (await SendAsync(rel, true)) as PubComp;
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

            var rs = await client.SendMessageAsync(msg);

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
    public virtual void Close(String reason) => Exchange?.Remove(Session.ID);
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