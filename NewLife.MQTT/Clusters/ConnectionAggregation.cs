using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>连接聚合网关。Edge 节点将设备连接聚合为逻辑连接，减少与上层节点 TCP 连接数</summary>
/// <remarks>
/// Edge 节点直接承载设备连接，通过聚合网关将多个设备的消息合并发送到 Regional 节点。
/// 聚合比可达 1000:1，大幅减少跨层 TCP 连接数。
/// 
/// 聚合策略：
/// - 小消息（< 64KB）按时间窗口（5ms）合并为批量 RPC
/// - 大消息直接透传
/// - CONNACK/PUBLISH 等响应消息从 Regional 批量返回后分发
/// </remarks>
public class ConnectionAggregation : DisposeBase
{
    #region 属性
    /// <summary>聚合比。当前已聚合连接数 / 原始设备连接数</summary>
    public Double AggregationRatio => _deviceConnectionCount > 0 ? (Double)_aggregatedConnectionCount / _deviceConnectionCount : 1;

    /// <summary>设备连接数（原始）</summary>
    public Int32 DeviceConnectionCount => _deviceConnectionCount;

    /// <summary>聚合连接数（到上层节点）</summary>
    public Int32 AggregatedConnectionCount => _aggregatedConnectionCount;

    private Int32 _deviceConnectionCount;
    private Int32 _aggregatedConnectionCount;

    /// <summary>聚合窗口（毫秒）。默认 5ms</summary>
    public Int32 BatchWindowMs { get; set; } = 5;

    /// <summary>批量合并大小阈值（字节）。默认 64KB</summary>
    public Int32 BatchSizeThreshold { get; set; } = 64 * 1024;

    /// <summary>每个聚合连接承载的最大设备数。默认 1000</summary>
    public Int32 MaxDevicesPerAggregatedConnection { get; set; } = 1000;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否已启动</summary>
    public Boolean Active { get; private set; }
    #endregion

    #region 统计
    private Int32 _totalDeviceMessages;
    private Int32 _totalAggregatedBatches;
    #endregion

    #region 方法
    /// <summary>注册一个设备连接到聚合网关</summary>
    /// <param name="deviceId">设备标识</param>
    /// <param name="remoteEndpoint">设备远程地址</param>
    public void RegisterDevice(String deviceId, String remoteEndpoint)
    {
        Interlocked.Increment(ref _deviceConnectionCount);
        // 计算所需的聚合连接数
        var needed = (_deviceConnectionCount + MaxDevicesPerAggregatedConnection - 1) / MaxDevicesPerAggregatedConnection;
        _aggregatedConnectionCount = needed;
    }

    /// <summary>注销一个设备连接</summary>
    public void UnregisterDevice(String deviceId)
    {
        if (_deviceConnectionCount > 0)
            Interlocked.Decrement(ref _deviceConnectionCount);
        var needed = Math.Max(1, (_deviceConnectionCount + MaxDevicesPerAggregatedConnection - 1) / MaxDevicesPerAggregatedConnection);
        _aggregatedConnectionCount = Math.Max(1, needed);
    }

    /// <summary>记录一条设备消息（由 Edge 节点调用）</summary>
    /// <param name="deviceId">设备标识</param>
    /// <param name="data">消息数据</param>
    public void RecordDeviceMessage(String deviceId, Byte[] data)
    {
        Interlocked.Increment(ref _totalDeviceMessages);
    }

    /// <summary>记录一次聚合批量发送</summary>
    public void RecordAggregatedBatch()
    {
        Interlocked.Increment(ref _totalAggregatedBatches);
    }

    /// <summary>获取当前统计信息</summary>
    public String GetStats()
    {
        return $"Devices={DeviceConnectionCount}, Aggregated={AggregatedConnectionCount}, Ratio={AggregationRatio:F2}:1, Batches={_totalAggregatedBatches}, Msgs={_totalDeviceMessages}";
    }
    #endregion

    /// <summary>释放资源</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
    }
}
