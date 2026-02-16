namespace NewLife.MQTT.Handlers;

/// <summary>MQTT Broker 运行统计数据</summary>
/// <remarks>
/// 提供 Broker 运行时的核心指标，用于 $SYS 系统主题发布和管理 API 查询。
/// 需要 Interlocked 操作的计数器使用公共字段，保证线程安全。
/// </remarks>
public class MqttStats
{
    #region 连接统计
    /// <summary>当前在线连接数</summary>
    public Int32 ConnectedClients;

    /// <summary>累计连接总数（自启动以来）</summary>
    public Int64 TotalConnections;

    /// <summary>最大同时在线连接数</summary>
    public Int32 MaxConnectedClients;
    #endregion

    #region 消息统计
    /// <summary>累计接收消息数</summary>
    public Int64 MessagesReceived;

    /// <summary>累计发送消息数</summary>
    public Int64 MessagesSent;

    /// <summary>每秒接收消息数（滑动窗口）</summary>
    public Int32 MessagesReceivedPerSecond;

    /// <summary>每秒发送消息数（滑动窗口）</summary>
    public Int32 MessagesSentPerSecond;

    /// <summary>累计接收字节数</summary>
    public Int64 BytesReceived;

    /// <summary>累计发送字节数</summary>
    public Int64 BytesSent;
    #endregion

    #region 订阅统计
    /// <summary>当前订阅数</summary>
    public Int32 Subscriptions;

    /// <summary>当前主题数（有订阅者的主题）</summary>
    public Int32 Topics;

    /// <summary>保留消息数</summary>
    public Int32 RetainedMessages;
    #endregion

    #region 系统信息
    /// <summary>启动时间</summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>运行时长（秒）</summary>
    public Int64 Uptime => (Int64)(DateTime.Now - StartTime).TotalSeconds;

    /// <summary>Broker 版本</summary>
    public String Version { get; set; } = "NewLife.MQTT/2.3";
    #endregion

    #region 速率计算
    private Int64 _lastReceived;
    private Int64 _lastSent;
    private DateTime _lastCalcTime = DateTime.Now;

    /// <summary>计算消息速率</summary>
    public void CalculateRates()
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastCalcTime).TotalSeconds;
        if (elapsed < 0.5) return;

        MessagesReceivedPerSecond = (Int32)((MessagesReceived - _lastReceived) / elapsed);
        MessagesSentPerSecond = (Int32)((MessagesSent - _lastSent) / elapsed);

        _lastReceived = MessagesReceived;
        _lastSent = MessagesSent;
        _lastCalcTime = now;
    }
    #endregion
}
