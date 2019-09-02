using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;

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

        /// <summary>服务器地址</summary>
        public NetUri Server { get; set; }

        private ISocketClient _Client;
        #endregion

        #region 构造

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void OnDispose(Boolean disposing)
        {
            base.OnDispose(disposing);

            _Client.TryDispose();
        }
        #endregion

        #region 方法
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
            }
        }

        private Int32 g_id;
        /// <summary>发送命令</summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        protected virtual async Task<MqttMessage> SendAsync(MqttMessage msg)
        {
            if (msg is MqttIdMessage idm && idm.Id == 0) idm.Id = (UInt16)Interlocked.Increment(ref g_id);

#if DEBUG
            WriteLog("=> {0}", msg);
#endif

            Init();
            var client = _Client;
            try
            {
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
        #endregion

        #region 连接
        public async Task ConnectAsync()
        {
            var msg = new ConnectMessage
            {

            };

            var rs = (await SendAsync(msg)) as ConnAck;
        }
        #endregion

        #region 发布
        #endregion

        #region 订阅
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