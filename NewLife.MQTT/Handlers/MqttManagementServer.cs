using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT HTTP 管理服务器</summary>
/// <remarks>
/// 在独立端口上启动 HTTP 服务，暴露 Broker 运行状态与管理接口。
/// 通过 <see cref="MqttServer"/> 的 <c>ManagementPort</c> 属性启用，默认端口 18883。
/// <para>可用 API：stats / clients / topics / retained</para>
/// <para>示例：http://127.0.0.1:18883/Api/Stats</para>
/// </remarks>
public class MqttManagementServer : DisposeBase, IServer, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>服务名称</summary>
    public String Name { get; set; } = "MqttManagement";

    /// <summary>监听端口。默认18883。设为0由系统自动分配空闲端口</summary>
    public Int32 Port { get; set; } = 18883;

    /// <summary>管理接口路径前缀。默认 /Api</summary>
    public String ApiPath { get; set; } = "/Api";

    /// <summary>消息交换机（必填）</summary>
    public MqttExchange Exchange { get; set; } = null!;

    /// <summary>是否启用 Web 可视化看板。启用后在 <see cref="DashboardPort"/> 提供 HTML 页面。默认 false</summary>
    public Boolean EnableDashboard { get; set; }

    /// <summary>管理 API 访问令牌。设置后所有管理接口（stats/clients/topics/retained/控制操作）需携带请求头 <c>x-token</c>、<c>Authorization: Bearer</c> 或查询参数 <c>token</c>。为空表示不鉴权（默认，向后兼容）</summary>
    public String? Token { get; set; }

    /// <summary>看板端口。默认 0 = Port + 1（即 18884）</summary>
    public Int32 DashboardPort { get; set; }

    /// <summary>看板服务实例（启用后可用）</summary>
    public MqttDashboard? Dashboard { get; set; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private HttpServer? _httpServer;
    private ObjectContainer? _container;
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop(disposing ? "Dispose" : "GC");
    }
    #endregion

    #region 启动停止
    /// <summary>启动管理服务</summary>
    public void Start()
    {
        if (Exchange == null) throw new InvalidOperationException("未配置消息交换机 Exchange");

        // 控制器单例经容器解析，构造时注入本管理服务
        var container = new ObjectContainer();
        container.Register(typeof(MqttManagementController), null, new MqttManagementController { Server = this });

        var server = new HttpServer
        {
            Name = Name,
            Port = Port,
            ServiceProvider = container.BuildServiceProvider(),
            Tracer = Tracer,
            Log = Log,
        };

        // 接口地址与旧版 Remoting 管理端保持一致，如 /Api/Stats、/Api/Clients
        var prefix = ApiPath.EnsureStart("/").EnsureEnd("/");
        var handler = new MqttManagementHandler(this)
        {
            Inner = new ControllerHandler { ControllerType = typeof(MqttManagementController), Path = prefix },
        };
        server.Map(prefix + "*", handler);
        server.Start();

        Port = server.Port;
        _httpServer = server;
        _container = container;

        WriteLog("HTTP 管理 API 已启动，端口 {0}，可用接口：stats / clients / topics / retained", Port);

        // 启动 Dashboard
        if (EnableDashboard)
        {
            var dash = Dashboard ?? new MqttDashboard();
            dash.ManagementServer = this;
            if (DashboardPort > 0) dash.Port = DashboardPort;
            dash.Log = Log;
            dash.Start();
            Dashboard = dash;
        }
    }

    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    public void Stop(String? reason)
    {
        Dashboard?.Stop(reason);
        Dashboard = null;

        _httpServer.TryDispose();
        _httpServer = null;

        _container.TryDispose();
        _container = null;
    }
    #endregion

    #region 日志
    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}

/// <summary>管理 API 处理器。先校验访问令牌，再交给控制器路由</summary>
internal sealed class MqttManagementHandler : IHttpHandler
{
    private readonly MqttManagementServer _server;

    /// <summary>实例化</summary>
    /// <param name="server">管理服务</param>
    public MqttManagementHandler(MqttManagementServer server) => _server = server;

    /// <summary>内层处理器。令牌校验通过后执行</summary>
    public IHttpHandler? Inner { get; set; }

    /// <summary>处理请求</summary>
    /// <param name="context">Http 上下文</param>
    /// <returns>处理任务</returns>
    public async Task ProcessRequestAsync(IHttpContext context)
    {
        var token = _server.Token;
        if (!token.IsNullOrEmpty() && HttpTokenHelper.GetToken(context) != token) throw new ApiException(ApiCode.Unauthorized, "无效的管理令牌");

        if (Inner != null) await Inner.ProcessRequestAsync(context).ConfigureAwait(false);
    }
}
