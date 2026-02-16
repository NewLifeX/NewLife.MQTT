using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Serialization;

namespace NewLife.MQTT.Handlers;

/// <summary>WebHook 事件类型</summary>
public enum WebHookEvent
{
    /// <summary>客户端连接</summary>
    ClientConnected,

    /// <summary>客户端断开</summary>
    ClientDisconnected,

    /// <summary>消息发布</summary>
    MessagePublish,

    /// <summary>消息投递</summary>
    MessageDelivered,

    /// <summary>客户端订阅</summary>
    ClientSubscribe,

    /// <summary>客户端取消订阅</summary>
    ClientUnsubscribe,
}

/// <summary>WebHook 事件数据</summary>
public class WebHookEventData
{
    /// <summary>事件类型</summary>
    public WebHookEvent Event { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

    /// <summary>主题</summary>
    public String? Topic { get; set; }

    /// <summary>消息内容（Base64编码）</summary>
    public String? Payload { get; set; }

    /// <summary>QoS</summary>
    public Int32 QoS { get; set; }

    /// <summary>是否保留消息</summary>
    public Boolean Retain { get; set; }

    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>断开原因</summary>
    public String? Reason { get; set; }
}

/// <summary>MQTT WebHook 处理器。将 MQTT 事件通过 HTTP POST 推送到外部系统</summary>
/// <remarks>
/// 支持配置多个 WebHook 端点，每个端点可以过滤感兴趣的事件类型。
/// 使用异步非阻塞方式发送，不影响 MQTT 消息处理性能。
/// </remarks>
public class MqttWebHook : DisposeBase
{
    #region 属性
    /// <summary>WebHook 端点列表</summary>
    public IList<WebHookEndpoint> Endpoints { get; set; } = [];

    /// <summary>重试次数。默认3次</summary>
    public Int32 MaxRetries { get; set; } = 3;

    /// <summary>超时时间（毫秒）。默认5000ms</summary>
    public Int32 Timeout { get; set; } = 5000;
    #endregion

    #region 方法
    /// <summary>触发 WebHook 事件</summary>
    /// <param name="data">事件数据</param>
    public void Fire(WebHookEventData data)
    {
        if (data == null) return;

        foreach (var endpoint in Endpoints)
        {
            if (endpoint.Events == null || endpoint.Events.Count == 0 || endpoint.Events.Contains(data.Event))
            {
                // 异步发送，不阻塞调用方
                _ = SendAsync(endpoint, data);
            }
        }
    }

    /// <summary>触发客户端连接事件</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="userName">用户名</param>
    public void OnClientConnected(String? clientId, String? userName)
    {
        Fire(new WebHookEventData
        {
            Event = WebHookEvent.ClientConnected,
            ClientId = clientId,
            UserName = userName,
        });
    }

    /// <summary>触发客户端断开事件</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="reason">断开原因</param>
    public void OnClientDisconnected(String? clientId, String? reason)
    {
        Fire(new WebHookEventData
        {
            Event = WebHookEvent.ClientDisconnected,
            ClientId = clientId,
            Reason = reason,
        });
    }

    /// <summary>触发消息发布事件</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="message">消息</param>
    public void OnMessagePublish(String? clientId, PublishMessage message)
    {
        Fire(new WebHookEventData
        {
            Event = WebHookEvent.MessagePublish,
            ClientId = clientId,
            Topic = message.Topic,
            Payload = message.Payload is Packet pk ? Convert.ToBase64String(pk.ToArray()) : null,
            QoS = (Int32)message.QoS,
            Retain = message.Retain,
        });
    }

    /// <summary>触发订阅事件</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topic">主题过滤器</param>
    /// <param name="qos">QoS</param>
    public void OnClientSubscribe(String? clientId, String topic, QualityOfService qos)
    {
        Fire(new WebHookEventData
        {
            Event = WebHookEvent.ClientSubscribe,
            ClientId = clientId,
            Topic = topic,
            QoS = (Int32)qos,
        });
    }

    /// <summary>异步发送 WebHook</summary>
    private async Task SendAsync(WebHookEndpoint endpoint, WebHookEventData data)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            try
            {
                await endpoint.SendAsync(data, Timeout).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                WriteLog("WebHook 发送失败 [{0}] 重试 {1}/{2}: {3}", endpoint.Url, i + 1, MaxRetries, ex.Message);
            }
        }
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttWebHook]{format}", args);
    #endregion
}

/// <summary>WebHook 端点配置</summary>
public class WebHookEndpoint
{
    /// <summary>端点 URL</summary>
    public String Url { get; set; } = null!;

    /// <summary>感兴趣的事件类型。为空或 null 表示所有事件</summary>
    public IList<WebHookEvent>? Events { get; set; }

    /// <summary>请求头。可用于认证等</summary>
    public IDictionary<String, String>? Headers { get; set; }

    /// <summary>发送事件数据到端点</summary>
    /// <param name="data">事件数据</param>
    /// <param name="timeout">超时毫秒</param>
    public Task SendAsync(WebHookEventData data, Int32 timeout)
    {
        var json = data.ToJson();
        var url = Url;

        return Task.Run(() =>
        {
            var request = System.Net.WebRequest.CreateHttp(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = timeout;
            if (Headers != null)
            {
                foreach (var header in Headers)
                    request.Headers[header.Key] = header.Value;
            }
            var body = json.GetBytes();
            request.ContentLength = body.Length;
            using var stream = request.GetRequestStream();
            stream.Write(body, 0, body.Length);
            using var response = request.GetResponse();
        });
    }
}
