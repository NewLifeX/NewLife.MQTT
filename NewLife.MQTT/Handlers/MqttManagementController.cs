using NewLife.Data;
using NewLife.Remoting;

namespace NewLife.MQTT.Handlers;

/// <summary>HTTP 管理 API 控制器。对外提供 Broker 状态查询接口</summary>
/// <remarks>
/// 通过 <see cref="MqttManagementServer"/> 挂载到指定端口（默认18883）。
/// 所有接口以 JSON 格式返回，可配合 Dashboard 或运维脚本使用。
/// </remarks>
public class MqttManagementController : IApi, IActionFilter
{
    #region 属性
    /// <summary>连接会话</summary>
    public IApiSession Session { get; set; } = null!;

    private MqttManagementServer _server = null!;

    private MqttExchange Exchange => _server.Exchange;
    #endregion

    #region 构造
    /// <summary>执行前：从宿主中取出管理服务器引用</summary>
    /// <param name="filterContext"></param>
    public void OnActionExecuting(ControllerContext filterContext)
    {
        Session = filterContext.Session!;
        if (Session.Host is IExtend ext)
            _server = (ext["Management"] as MqttManagementServer)!;
    }

    /// <summary>执行后</summary>
    public void OnActionExecuted(ControllerContext filterContext) { }
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
    #endregion
}
