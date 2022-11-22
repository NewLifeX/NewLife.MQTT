using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MQTT;

/// <summary>MQTT服务端</summary>
public class MqttServer : NetServer<MqttSession>
{
    /// <summary>服务提供者</summary>
    public IServiceProvider Provider { get; set; }

    /// <summary>实例化MQTT服务器</summary>
    public MqttServer() => Port = 1883;

    /// <summary>启动</summary>
    protected override void OnStart()
    {
        Add(new MqttCodec());

        base.OnStart();
    }

    ///// <summary>处理请求</summary>
    ///// <param name="session"></param>
    ///// <param name="message"></param>
    ///// <returns></returns>
    ///// <exception cref="NotSupportedException"></exception>
    //public virtual MqttMessage Process(INetSession session, MqttMessage message)
    //{
    //    var handler = Provider.GetRequiredService<IMqttHandler>();
    //    if (handler == null) throw new NotSupportedException("未注册指令处理器");

    //    return handler.Process(session, message);
    //}
}

/// <summary>会话</summary>
public class MqttSession : NetSession<MqttServer>
{
    /// <summary>指令处理器</summary>
    public IMqttHandler Handler { get; set; }

    /// <summary>接收指令</summary>
    /// <param name="e"></param>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        var debug = XTrace.Log.Level <= LogLevel.Debug;
        var msg = e.Message as MqttMessage;

        if (debug) WriteLog("<={0}", msg);
        if (msg != null)
        {
            Handler ??= Host.Provider.GetRequiredService<IMqttHandler>();
            if (Handler == null) throw new NotSupportedException("未注册指令处理器");

            MqttMessage result = null;
            using var span = Host.Tracer?.NewSpan($"mqtt:{msg.Type}", msg);
            try
            {
                // 执行处理器
                result = Handler.Process(this, msg);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);

                var hex = e.Packet?.ToHex(1024);
                span?.SetError(ex, hex);

                XTrace.WriteLine(hex);
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