using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Security;
using NewLife.Threading;

namespace NewLife.MQTT
{
    /// <summary>MQTT客户端</summary>
    public class MqttClient : DisposeBase
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>超时。默认3000ms</summary>
        public Int32 Timeout { get; set; } = 3_000;

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
        /// <param name="msg"></param>
        /// <returns></returns>
        protected virtual async Task<MqttMessage> SendAsync(MqttMessage msg)
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
                if (msg.Type == MqttType.Disconnect)
                {
                    client.SendMessage(msg);
#if NET4
                    return await TaskEx.FromResult(msg);
#else
                    return await Task.FromResult(msg);
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
        public event EventHandler<EventArgs<MqttMessage>> Received;

        /// <summary>收到命令</summary>
        /// <param name="msg"></param>
        protected virtual MqttMessage OnReceive(MqttMessage msg)
        {
#if DEBUG
            WriteLog("收到：{0}", msg);
#endif

            if (Received == null) return null;

            var e = new EventArgs<MqttMessage>(msg);
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

            await SendAsync(message);
        }
        #endregion

        #region 发布
        public async Task<PubAck> PublicAsync(String topic, Packet pk)
        {
            var message = new PublishMessage
            {
                TopicName = topic,
                Payload = pk,
            };

            return await PublicAsync(message);
        }

        public async Task<PubAck> PublicAsync(PublishMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var rs = (await SendAsync(message)) as PubAck;
            return rs;
        }
        #endregion

        #region 订阅
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
        private async void DoPing(Object state)
        {
            await PingAsync();
        }
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