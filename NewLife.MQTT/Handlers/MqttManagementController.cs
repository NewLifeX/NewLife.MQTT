using NewLife.Data;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>HTTP 管理 API 控制器。对外提供 Broker 状态查询与运维接口</summary>
/// <remarks>
/// 通过 <see cref="MqttManagementServer"/> 挂载到指定端口（默认18883）。
/// 所有接口以 JSON 格式返回，可配合 Dashboard 或运维脚本使用。
/// <para>地址形如 <c>GET http://127.0.0.1:18883/Api/Stats</c>，参数从路由参数、查询串或JSON请求体绑定。</para>
/// </remarks>
public class MqttManagementController
{
    #region 属性
    private MqttManagementServer _server = null!;

    /// <summary>管理服务。由 <see cref="MqttManagementServer"/> 启动时注入</summary>
    public MqttManagementServer Server { get => _server; set => _server = value; }

    private MqttExchange Exchange => _server.Exchange;
    #endregion

    #region 接口
    /// <summary>连通性测试</summary>
    public String Echo(String msg) => msg;

    /// <summary>获取 Broker 运行统计</summary>
    /// <returns>
    /// 包含连接数、消息量、订阅数、保留消息数、启动时间、运行时长等指标。
    /// </returns>
    public Object Stats()
    {
        var s = Exchange.Stats;
        return new
        {
            s.ConnectedClients,
            s.MaxConnectedClients,
            s.TotalConnections,
            s.Subscriptions,
            s.Topics,
            s.RetainedMessages,
            s.MessagesReceived,
            s.MessagesSent,
            s.MessagesReceivedPerSecond,
            s.MessagesSentPerSecond,
            s.BytesReceived,
            s.BytesSent,
            StartTime = s.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            s.Uptime,
            s.Version,
        };
    }

    /// <summary>获取已连接客户端 ID 列表</summary>
    public IReadOnlyList<String> Clients() => Exchange.GetConnectedClients();

    /// <summary>获取主题订阅统计。key=主题，value=订阅者数量</summary>
    public IReadOnlyDictionary<String, Int32> Topics() => Exchange.GetTopicSubscriptions();

    /// <summary>获取保留消息主题列表</summary>
    public IReadOnlyList<String> Retained() => Exchange.GetRetainedTopics();

    /// <summary>断开指定客户端连接</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="reason">断开原因（可选）</param>
    /// <returns>是否成功断开</returns>
    public Boolean DisconnectClient(String clientId, String? reason = null)
    {
        if (clientId.IsNullOrEmpty()) return false;

        return Exchange.DisconnectClient(clientId, reason ?? "管理 API 断开");
    }

    /// <summary>清除指定主题的保留消息</summary>
    /// <param name="topic">主题名。为空时清除全部保留消息</param>
    /// <returns>被清除的保留消息数</returns>
    public Int32 ClearRetained(String? topic = null)
    {
        var exchange = Exchange;
        if (topic.IsNullOrEmpty())
        {
            var count = exchange.GetRetainedTopics().Count;
            foreach (var t in exchange.GetRetainedTopics().ToList())
            {
                exchange.Publish(new PublishMessage { Topic = t, Retain = true, Payload = ArrayPacket.Empty });
            }
            return count;
        }
        else
        {
            exchange.Publish(new PublishMessage { Topic = topic, Retain = true, Payload = ArrayPacket.Empty });
            return 1;
        }
    }

    /// <summary>通过管理 API 发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="payload">消息内容（Base64 编码）</param>
    /// <param name="qos">QoS，默认 0</param>
    /// <param name="retain">是否保留，默认 false</param>
    /// <returns>是否成功发布</returns>
    public Boolean PublishMessage(String topic, String? payload = null, Int32 qos = 0, Boolean retain = false)
    {
        if (topic.IsNullOrEmpty()) return false;

        var msg = new PublishMessage
        {
            Topic = topic,
            Payload = payload != null ? new ArrayPacket(Convert.FromBase64String(payload)) : ArrayPacket.Empty,
            QoS = (QualityOfService)qos,
            Retain = retain,
        };

        Exchange.Publish(msg);
        return true;
    }
    #endregion
}
