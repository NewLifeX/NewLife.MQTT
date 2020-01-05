using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Serialization;
using NewLife.Threading;

namespace NewLife.MQTT
{
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
        public NetUri Server { get; set; }

        /// <summary>客户端标识。必填项</summary>
        public String ClientId { get; set; }

        /// <summary>用户名</summary>
        public String UserName { get; set; }

        /// <summary>密码</summary>
        public String Password { get; set; }

        private ISocketClient _Client;
        #endregion

        #region 构造
        /// <summary>实例化Mqtt客户端</summary>
        public MqttClient() => Name = GetType().Name.TrimEnd("Client");

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            _timerPing.TryDispose();
            _Client.TryDispose();
        }
        #endregion

        #region 核心方法
        void Init()
        {
            var client = _Client;
            if (client != null && client.Active && !client.Disposed) return;
            lock (this)
            {
                client = _Client;
                if (client != null && client.Active && !client.Disposed) return;
                _Client = null;

                var uri = Server;
                WriteLog("正在连接[{0}]", uri);

                if (uri.Type == NetType.Unknown) uri.Type = NetType.Tcp;

                client = uri.CreateRemote();
                client.Log = Log;
                client.Timeout = Timeout;
                client.Add(new MqttCodec());

                // 关闭Tcp延迟以合并小包的算法，降低延迟
                if (client is TcpSession tcp) tcp.NoDelay = true;

                client.Received += Client_Received;
                _Client = client;

                // 打开心跳定时器
                var p = KeepAlive * 1000 / 2;
                if (p > 0)
                {
                    if (_timerPing == null) _timerPing = new TimerX(DoPing, null, p, p) { Async = true };
                }
            }
        }

        private Int32 g_id;
        /// <summary>发送命令</summary>
        /// <param name="msg">消息</param>
        /// <param name="waitForResponse">是否等待响应</param>
        /// <returns></returns>
        protected virtual async Task<MqttMessage> SendAsync(MqttMessage msg, Boolean waitForResponse = true)
        {
            if (msg is MqttIdMessage idm && idm.Id == 0 && (msg.Type != MqttType.Publish || msg.QoS > 0)) idm.Id = (UInt16)Interlocked.Increment(ref g_id);

#if DEBUG
            WriteLog("=> {0}", msg);
#endif

            Init();
            var client = _Client;
            try
            {
                // 断开消息没有响应
                if (!waitForResponse)
                {
                    client.SendMessage(msg);
#if NET4
                    return await TaskEx.FromResult((MqttMessage)null);
#else
                    return await Task.FromResult((MqttMessage)null);
#endif
                }

                var rs = await client.SendMessageAsync(msg);

#if DEBUG
                WriteLog("<= {0}", rs as MqttMessage);
#endif

                return rs as MqttMessage;
            }
            catch
            {
                // 销毁，下次使用另一个地址
                client.TryDispose();

                throw;
            }
        }
        #endregion

        #region 接收数据
        private void Client_Received(Object sender, ReceivedEventArgs e)
        {
            var cmd = e.Message as MqttMessage;
            if (cmd == null || cmd.Reply) return;

            var rs = OnReceive(cmd);
            if (rs != null)
            {
                var ss = sender as ISocketRemote;
                ss.SendMessage(rs);
            }
        }

        /// <summary>收到命令时</summary>
        public event EventHandler<EventArgs<PublishMessage>> Received;

        /// <summary>收到命令</summary>
        /// <param name="msg"></param>
        protected virtual MqttMessage OnReceive(MqttMessage msg)
        {
#if DEBUG
            WriteLog("收到：{0}", msg);
#endif

            if (Received == null) return msg;

            if (!(msg is PublishMessage pm)) return msg;

            var e = new EventArgs<PublishMessage>(pm);
            Received.Invoke(this, e);

            return e.Arg;
        }
        #endregion

        #region 连接
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
            return rs;
        }

        /// <summary>断开连接</summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            if (ClientId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ClientId));

            var message = new DisconnectMessage();

            await SendAsync(message, false);
        }
        #endregion

        #region 发布
        /// <summary>发布消息</summary>
        /// <param name="topic">主题</param>
        /// <param name="data">消息数据</param>
        /// <param name="qos">服务质量</param>
        /// <returns></returns>
        public async Task<MqttIdMessage> PublicAsync(String topic, Object data, QualityOfService qos = QualityOfService.AtMostOnce)
        {
            var pk = data as Packet;
            if (pk == null && data != null) pk = Serialize(data);

            var message = new PublishMessage
            {
                TopicName = topic,
                Payload = pk,
                QoS = qos,
            };

            return await PublicAsync(message);
        }

        /// <summary>发布消息</summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<MqttIdMessage> PublicAsync(PublishMessage message)
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

        /// <summary>把对象序列化为数据，字节数组和字符串以外的复杂类型，走Json序列化</summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Packet Serialize(Object data)
        {
            if (data is Byte[] buf) return buf;
            if (data is String str) return str.GetBytes();

            return data.ToJson().GetBytes();
        }
        #endregion

        #region 订阅
        /// <summary>订阅主题</summary>
        /// <param name="topicFilters">主题过滤器</param>
        /// <param name="qos">服务质量</param>
        /// <returns></returns>
        public async Task<SubAck> SubscribeAsync(String[] topicFilters, QualityOfService qos = QualityOfService.AtMostOnce)
        {
            var subscriptions = topicFilters.Select(e => new Subscription(e, qos)).ToList();

            return await SubscribeAsync(subscriptions);
        }

        /// <summary>订阅主题</summary>
        /// <param name="subscriptions"></param>
        /// <returns></returns>
        public async Task<SubAck> SubscribeAsync(IList<Subscription> subscriptions)
        {
            var message = new SubscribeMessage
            {
                Requests = subscriptions,
            };

            var rs = (await SendAsync(message)) as SubAck;

            return rs;
        }

        /// <summary>取消订阅主题</summary>
        /// <param name="topicFilters">主题过滤器</param>
        /// <returns></returns>
        public async Task<UnsubAck> UnsubscribeAsync(params String[] topicFilters)
        {
            var message = new UnsubscribeMessage
            {
                TopicFilters = topicFilters,
            };

            var rs = (await SendAsync(message)) as UnsubAck;

            return rs;
        }
        #endregion

        #region 心跳
        /// <summary>心跳</summary>
        /// <returns></returns>
        public async Task<PingResponse> PingAsync()
        {
            var message = new PingRequest();

            var rs = (await SendAsync(message)) as PingResponse;
            return rs;
        }

        private TimerX _timerPing;
        private async void DoPing(Object state) => await PingAsync();
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; } = Logger.Null;

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info($"[{Name}]{format}", args);
        #endregion
    }
}