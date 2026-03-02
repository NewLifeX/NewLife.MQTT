using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT HTTP 管理服务器</summary>
/// <remarks>
/// 在独立端口上启动 HTTP/RPC 服务，暴露 Broker 运行状态与管理接口。
/// 通过 <see cref="MqttServer"/> 的 <c>ManagementPort</c> 属性启用，默认端口 18883。
/// <para>可用 API：stats / clients / topics / retained</para>
/// <para>示例：http://127.0.0.1:18883/Api/Stats</para>
/// </remarks>
public class MqttManagementServer : DisposeBase, IServer, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>服务名称</summary>
    public String Name { get; set; } = "MqttManagement";

    /// <summary>监听端口。默认18883</summary>
    public Int32 Port { get; set; } = 18883;

    /// <summary>消息交换机（必填）</summary>
    public MqttExchange Exchange { get; set; } = null!;

    /// <summary>是否启用 Web 可视化看板。启用后在 <see cref="DashboardPort"/> 提供 HTML 页面。默认 false</summary>
    public Boolean EnableDashboard { get; set; }

    /// <summary>看板端口。默认 0 = Port + 1（即 18884）</summary>
    public Int32 DashboardPort { get; set; }

    /// <summary>看板服务实例（启用后可用）</summary>
    public MqttDashboard? Dashboard { get; set; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private ApiServer? _apiServer;
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

        var uri = new NetUri(NetType.Http, "*", Port);
        var server = new ApiServer(uri)
        {
            Name = Name,
            Tracer = Tracer,
            Log = Log,
        };

        // 将管理服务器引用传给控制器
        server["Management"] = this;
        server.Register<MqttManagementController>();

        server.Start();
        _apiServer = server;

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

        _apiServer.TryDispose();
        _apiServer = null;
    }
    #endregion

    #region 日志
    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
