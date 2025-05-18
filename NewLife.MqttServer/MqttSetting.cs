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

    /// <summary>集群端口。默认 0</summary>
    [Description("集群端口。默认 0")]
    public Int32 ClusterPort { get; set; }

    /// <summary>集群节点。其它节点地址，逗号分隔</summary>
    [Description("集群节点。其它节点地址，逗号分隔")]
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