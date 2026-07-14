using System;
using System.ComponentModel;
using NewLife.Configuration;

namespace NewLife.MQTTServer;

[Config("MqttServer")]
public class MqttSetting : Config<MqttSetting>
{
    /// <summary>调试开关。默认 false</summary>
    [Description("调试开关。默认 false")]
    public Boolean Debug { get; set; }

    /// <summary>端口。默认 1883</summary>
    [Description("端口。默认 1883")]
    public Int32 Port { get; set; } = 1883;

    /// <summary>集群端口。默认 0，当前节点用于接收其他集群节点连接的端口，建议2883</summary>
    [Description("集群端口。默认 0，当前节点用于接收其他集群节点连接的端口，建议2883")]
    public Int32 ClusterPort { get; set; }

    /// <summary>集群节点。节点地址，逗号分隔，格式：ip1:port1,ip2:port2。这里的端口应对应节点的ClusterPort，可以包含自己</summary>
    [Description("集群节点。节点地址，逗号分隔，格式：ip1:port1,ip2:port2。这里的端口应对应节点的ClusterPort，可以包含自己")]
    public String ClusterNodes { get; set; } = "";

    /// <summary>是否启用WebSocket。默认 true</summary>
    [Description("是否启用WebSocket。默认 true")]
    public Boolean EnableWebSocket { get; set; } = true;

    /// <summary>WebSocket端口。默认 8083</summary>
    [Description("WebSocket端口。默认 8083")]
    public Int32 WebSocketPort { get; set; } = 8083;

    /// <summary>WebSocket路径。默认 /mqtt</summary>
    [Description("WebSocket路径。默认 /mqtt")]
    public String WebSocketPath { get; set; } = "/mqtt";
}