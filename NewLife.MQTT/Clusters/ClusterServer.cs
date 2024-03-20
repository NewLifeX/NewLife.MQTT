using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;

namespace NewLife.MQTT.Clusters;

/// <summary>集群服务器</summary>
public class ClusterServer : IServer, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>集群端口。默认2883</summary>
    public Int32 Port { get; set; } = 2883;

    /// <summary>集群节点集合</summary>
    public IList<ClusterNode> Nodes { get; set; } = [];

    public IServiceProvider? ServiceProvider { get; set; }

    private ApiServer? _server;
    #endregion

    #region 构造
    #endregion

    #region 方法
    public void Start()
    {
        ServiceProvider ??= ObjectContainer.Provider;

        var server = new ApiServer
        {
            Port = Port,
            ServiceProvider = ServiceProvider,

            Tracer = Tracer,
            Log = Log,
        };

        server.Register<ClusterController>();

        server.Start();

        _server = server;
    }

    public void Stop(String reason)
    {
        _server.TryDispose();
        _server = null;
    }
    #endregion

    #region 日志
    public ITracer? Tracer { get; set; }

    public ILog Log { get; set; } = Logger.Null;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
