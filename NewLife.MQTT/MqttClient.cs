using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Serialization;
using NewLife.Threading;

namespace NewLife.MQTT;

/// <summary>MQTT客户端</summary>
public class MqttClient : DisposeBase
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>超时。默认15000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>链接超时。一半时间发起心跳，默认600秒</summary>
    public Int32 KeepAlive { get; set; } = 600;

    /// <summary>服务器地址</summary>
    public String? Server { get; set; }

    /// <summary>是否进行SSL连接</summary>
    [Obsolete("=>SslProtocol")]
    public Boolean UseSSL { get => SslProtocol != SslProtocols.None; set => SslProtocol = SslProtocols.Tls12; }

    /// <summary>SSL协议。默认None，一般使用Tls12来启用TLS</summary>
    public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

    /// <summary>X509证书。用于SSL连接时验证证书指纹，可以直接加载pem证书文件，未指定时不验证证书</summary>
    /// <remarks>
    /// 可以使用pfx证书文件，也可以使用pem证书文件。
    /// 服务端必须指定证书，客户端可以不指定，除非服务端请求客户端证书。
    /// </remarks>
    /// <example>
    /// var cert = new X509Certificate2("file", "pass");
    /// </example>
    public X509Certificate? Certificate { get; set; }

    /// <summary>客户端标识。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

    /// <summary>密码</summary>
    public String? Password { get; set; }

    /// <summary>
    /// 清除会话，默认true
    /// </summary>
    public Boolean CleanSession { get; set; } = true;

    /// <summary>编码器。决定对象存储序列化格式，默认json</summary>
    public IPacketEncoder Encoder { get; set; } = new DefaultPacketEncoder();

    /// <summary>
    /// 断开后是否自动重连
    /// </summary>
    public Boolean Reconnect { get; set; } = true;

    /// <summary>
    /// 连接成功后赋值为true
    /// </summary>
    private Boolean _isConnected = false;

    /// <summary>
    /// 是否处于连接状态
    /// </summary>
    public Boolean IsConnected => _isConnected == true && _Client != null && _Client.Active && !_Client.Disposed;

    /// <summary>性能跟踪</summary>
    public ITracer? Tracer { get; set; }

    private ISocketClient? _Client;

    private Int32 _taskCanceledCount;
    #endregion

    #region 构造
    /// <summary>实例化Mqtt客户端</summary>
    public MqttClient()
    {
        Name = GetType().Name.TrimEnd("Client");

        try
        {
            ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
        }
        catch { }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timerPing.TryDispose();
        _Client.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>使用连接字符串初始化</summary>
    /// <param name="config"></param>
    public virtual void Init(String config)
    {
        if (config.IsNullOrEmpty()) return;

        var dic =
            config.Contains(',') && !config.Contains(';') ?
            config.SplitAsDictionary("=", ",", true) :
            config.SplitAsDictionary("=", ";", true);
        if (dic.Count > 0)
        {
            Server = dic["Server"]?.Trim();
            UserName = dic["UserName"]?.Trim();
            Password = dic["Password"]?.Trim();
            ClientId = dic["ClientId"]?.Trim();

            if (dic.TryGetValue("Timeout", out var str)) Timeout = str.ToInt();
        }
    }
    #endregion

    #region 核心方法
    private void Init()
    {
        var client = _Client;
        if (client != null && client.Active && !client.Disposed) return;
        lock (this)
        {
            client = _Client;
            if (client != null && client.Active && !client.Disposed) return;
            _Client = null;

            if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));

            var uri = new NetUri(Server);
            if (uri.Type == NetType.Unknown) uri.Type = NetType.Tcp;
            if (uri.Port == 0) uri.Port = 1883;
            WriteLog("正在连接[{0}]", uri);

            client = uri.CreateRemote();

            client.Log = Log;
            client.Timeout = Timeout;
            client.Add(new MqttCodec());

            // 关闭Tcp延迟以合并小包的算法，降低延迟
            if (client is TcpSession tcp)
            {
                tcp.NoDelay = true;
                //tcp.DisconnectWhenEmptyData = false;
            }

            if (Certificate != null)
            {
                if (client is not TcpSession tcp2)
                    throw new ArgumentException("使用SSl连接，地址需设置为tcp://开头");

                tcp2.SslProtocol = SslProtocol;
                tcp2.Certificate = Certificate;
            }

            client.Received += Client_Received;
            client.Closed += Client_Closed;
            client.Error += Client_Error;
            client.Open();

            _Client = client;

            // 打开心跳定时器
            var p = KeepAlive * 1000 / 2;
            if (p > 0)
            {
                _timerPing ??= new TimerX(DoPing, null, 5_000, p) { Async = true };
            }
        }
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

        // 性能埋点
        using var span = Tracer?.NewSpan($"mqtt:{Name}:{msg.Type}", msg);

        if (Log != null && Log.Level <= LogLevel.Debug)
        {
            if (msg is PublishMessage pm)
                WriteLog("=> {0} {1}", msg, pm.Payload?.ToStr());
            else
                WriteLog("=> {0}", msg);
        }

        Init();

        var client = _Client ?? throw new ArgumentNullException(nameof(_Client));
        try
        {
            // 断开消息没有响应
            if (!waitForResponse)
            {
                client.SendMessage(msg);
                //return await Task.FromResult((MqttMessage)null);
                return null;
            }

            var rs = await client.SendMessageAsync(msg);

            // 重置
            _taskCanceledCount = 0;

            if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("<= {0}", rs as MqttMessage);

            return rs as MqttMessage;
        }
        catch (TaskCanceledException ex)
        {
            span?.SetError(ex, msg);

            if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("发送超时，任务取消=> {0}", msg as MqttMessage);

            // 超过三次，销毁连接
            if (_taskCanceledCount++ > 3)
            {
                if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("发送超时超过三次，销毁，下次使用另一个地址");

                // 销毁，下次使用另一个地址
                client.TryDispose();
            }

            throw;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, msg);

            if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("销毁，下次使用另一个地址");

            // 销毁，下次使用另一个地址
            client.TryDispose();

            throw;
        }
    }
    #endregion

    #region 接收数据
    private readonly IDictionary<String, Subscription> _subs = new Dictionary<String, Subscription>();

    private void Client_Received(Object sender, ReceivedEventArgs e)
    {
        if (e.Message is not MqttMessage msg || msg.Reply) return;

        if (Log != null && Log.Level <= LogLevel.Debug)
        {
            if (msg is PublishMessage pm)
                WriteLog("<= {0} {1}", msg, pm.Payload?.ToStr());
            else
                WriteLog("<= {0}", msg);
        }

        // 性能埋点
        using var span = Tracer?.NewSpan($"mqtt:{Name}:{msg.Type}", msg);
        try
        {
            var rs = OnReceive(msg);
            if (rs != null)
            {
                var ss = sender as ISocketRemote;

                if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("=> {0}", rs);

                ss!.SendMessage(rs);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            throw;
        }
    }

    /// <summary>收到命令时</summary>
    public event EventHandler<EventArgs<PublishMessage>>? Received;

    /// <summary>收到命令</summary>
    /// <param name="msg"></param>
    protected virtual MqttMessage? OnReceive(MqttMessage msg)
    {
        if (msg is PubRel pr) return new PubComp { Id = pr.Id };
        if (msg is not PublishMessage pm) return null;

        // 模糊匹配
        foreach (var item in _subs)
        {
            var sub = item.Value;
            if (sub?.Callback != null && MqttTopicFilter.IsMatch(pm.Topic, sub.TopicFilter))
            {
                sub.Callback(pm);
            }
        }

        if (Received != null)
        {
            var e = new EventArgs<PublishMessage>(pm);
            Received.Invoke(this, e);
        }

        // 根据 Qos 决定是否返回，返回何种消息
        switch (msg.QoS)
        {
            case QualityOfService.AtMostOnce:
                return null;
            case QualityOfService.AtLeastOnce:
                return new PubAck { Id = pm.Id };
            case QualityOfService.ExactlyOnce:
                return new PubRec { Id = pm.Id };
            default:
                break;
        }
        return null;
    }
    #endregion

    #region 连接
    /// <summary>
    /// 断开连接时
    /// </summary>
    public event EventHandler<EventArgs>? Disconnected;

    /// <summary>
    /// 连接成功时
    /// </summary>
    public event EventHandler<EventArgs>? Connected;

    /// <summary>连接服务端</summary>
    /// <returns></returns>
    public async Task<ConnAck> ConnectAsync()
    {
        if (ClientId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ClientId));

        var message = new ConnectMessage
        {
            ClientId = ClientId,
            Username = UserName,
            Password = Password,
            CleanSession = CleanSession,
        };

        return await ConnectAsync(message);
    }

    /// <summary>连接服务端</summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task<ConnAck> ConnectAsync(ConnectMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        // 填充客户端Id
        if (message.ClientId.IsNullOrEmpty())
        {
            if (ClientId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ClientId));

            message.ClientId = ClientId;
        }

        // 心跳
        if (KeepAlive > 0 && message.KeepAliveInSeconds == 0) message.KeepAliveInSeconds = (UInt16)KeepAlive;

        var rs = (await SendAsync(message)) as ConnAck;

        // 判断响应，是否成功连接
        if (rs!.ReturnCode != ConnectReturnCode.Accepted)
        {
            var errMsg = $"连接失败，参数：{message.ToJson()}，原因：{rs.ReturnCode}";
            WriteLog(errMsg);
            throw new Exception(errMsg);
        }

        _isConnected = true;

        var e = new EventArgs();
        Connected?.Invoke(this, e);

        // 断线重连后，重新订阅已订阅消息
        if (_subs.Count > 0)
        {
            var message2 = new SubscribeMessage
            {
                Requests = _subs.Values.ToArray(),
            };

            var rs2 = (await SendAsync(message2)) as SubAck;
            if (rs2 == null) _subs.Clear();
        }

        return rs;
    }

    /// <summary>断开连接</summary>
    /// <returns></returns>
    public async Task DisconnectAsync()
    {
        var message = new DisconnectMessage();

        await SendAsync(message, false);

        var e = new EventArgs();
        Disconnected?.Invoke(this, e);
    }

    private void Client_Error(Object sender, ExceptionEventArgs e)
    {
        //throw new NotImplementedException();
    }

    private void Client_Closed(Object sender, EventArgs e)
    {
        _isConnected = false;

        WriteLog("断开连接");
        Disconnected?.Invoke(this, e);

        if (!Reconnect) return;
        WriteLog("尝试重新连接");
        ConnectAsync().GetAwaiter();
    }
    #endregion

    #region 发布
    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="data">消息数据</param>
    /// <param name="qos">服务质量</param>
    /// <returns></returns>
    public async Task<MqttIdMessage?> PublishAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce)
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

        return await PublishAsync(message);
    }

    /// <summary>发布消息</summary>
    /// <param name="message"></param>
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
    #endregion

    #region 订阅
    /// <summary>订阅主题</summary>
    /// <param name="topicFilter">主题过滤器</param>
    /// <param name="callback">收到该主题消息时的回调</param>
    /// <returns></returns>
    public async Task<SubAck?> SubscribeAsync(String topicFilter, Action<PublishMessage>? callback = null)
    {
        var subscription = new Subscription(topicFilter, QualityOfService.AtMostOnce);

        return await SubscribeAsync([subscription], callback);
    }

    /// <summary>订阅主题</summary>
    /// <param name="topicFilters">主题过滤器</param>
    /// <param name="qos">服务质量</param>
    /// <returns></returns>
    public async Task<SubAck?> SubscribeAsync(String[] topicFilters, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        var subscriptions = topicFilters.Select(e => new Subscription(e, qos)).ToList();

        return await SubscribeAsync(subscriptions);
    }

    /// <summary>订阅主题</summary>
    /// <param name="subscriptions">订阅集合</param>
    /// <param name="callback">收到该主题消息时的回调</param>
    /// <returns></returns>
    public async Task<SubAck?> SubscribeAsync(IList<Subscription> subscriptions, Action<PublishMessage>? callback = null)
    {
        // 已订阅，不重复
        subscriptions = subscriptions.Where(e => !_subs.ContainsKey(e.TopicFilter)).ToList();
        if (subscriptions.Count == 0) return null;

        var message = new SubscribeMessage
        {
            Requests = subscriptions,
        };

        var rs = (await SendAsync(message)) as SubAck;
        if (rs != null)
        {
            foreach (var item in subscriptions)
            {
                item.Callback = callback;
                _subs[item.TopicFilter] = item;
            }
        }

        return rs;
    }

    /// <summary>取消订阅主题</summary>
    /// <param name="topicFilters">主题过滤器</param>
    /// <returns></returns>
    public async Task<UnsubAck?> UnsubscribeAsync(params String[] topicFilters)
    {
        var message = new UnsubscribeMessage
        {
            TopicFilters = topicFilters,
        };

        var rs = (await SendAsync(message)) as UnsubAck;
        if (rs != null)
        {

            foreach (var item in topicFilters)
            {
                _subs.Remove(item);
            }
        }

        return rs;
    }
    #endregion

    #region 心跳
    /// <summary>心跳</summary>
    /// <returns></returns>
    public async Task<PingResponse?> PingAsync()
    {
        if (!IsConnected)
        {
            WriteLog("未连接成功，不发送ping报文"); return null;
        }

        var message = new PingRequest();

        var rs = (await SendAsync(message)) as PingResponse;
        return rs;
    }

    private TimerX? _timerPing;
    private void DoPing(Object state) => PingAsync().Wait();
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}