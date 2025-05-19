using System.Net;
using System.Net.Sockets;

using NewLife.Net;

namespace NewLife.MQTT.WebSocket;

/// <summary>WebSocket服务器，继承自MqttServer以共享数据和逻辑</summary>
/// <remarks>
/// 该服务器可以同时监听多个端口，例如标准MQTT端口1883和WebSocket端口8083
/// 所有端口共享相同的数据和逻辑，由NetServer统一管理
/// </remarks>
public class WebSocketServer : MqttServer
{
    #region 属性

    /// <summary>WebSocket端口。默认8083</summary>
    public Int32 WebSocketPort { get; set; } = 8083;

    /// <summary>WebSocket服务器</summary>
    protected ISocketServer? WsServer { get; set; }

    /// <summary>是否打印WebSocket数据。默认true</summary>
    public Boolean LogWebSocketData { get; set; } = true;

    /// <summary>WebSocket路径。默认/mqtt</summary>
    public String WebSocketPath { get; set; } = "/mqtt";

    #endregion

    #region 构造函数

    /// <summary>实例化WebSocket服务器</summary>
    public WebSocketServer()
    {
        // 默认使用标准MQTT端口1883
        Port = 1883;
    }

    #endregion

    #region 方法

    /// <summary>确保建立服务器</summary>
    public override void EnsureCreateServer()
    {
        try
        {
            WriteLog("准备创建服务器，端口：{0}和{1}", Port, WebSocketPort);

            // 如果服务器集合为空，创建服务器
            if (Servers.Count <= 0)
            {
                // 创建标准MQTT端口服务器
                var mqttUri = Local;
                var family = AddressFamily;
                if (family <= AddressFamily.Unspecified && mqttUri.Host != "*" && !mqttUri.Address.IsAny())
                    family = mqttUri.Address.AddressFamily;
                var mqttServers = CreateServer(mqttUri.Address, mqttUri.Port, mqttUri.Type, family);
                foreach (var item in mqttServers)
                {
                    // 先添加服务器
                    AttachServer(item);

                    // 再设置服务器名称，包含端口号，这样可以覆盖AttachServer中的设置
                    item.Name = $"MQTT端口{Port}";
                }

                // 创建WebSocket端口服务器
                var wsServers = CreateServer(IPAddress.Any, WebSocketPort, NetType.Tcp, AddressFamily.Unspecified);
                foreach (var item in wsServers)
                {
                    // 保存WebSocket服务器引用
                    WsServer = item;

                    // 设置服务器名称
                    item.Name = $"WebSocket端口{WebSocketPort}";

                    // 添加编解码器
                    // 注意：我们需要先处理WebSocket协议，再处理MQTT协议

                    // 添加ProxyCodec
                    WriteLog("为WebSocket服务器添加ProxyCodec");
                    item.Add(new ProxyProtocol.ProxyCodec());

                    // 添加WebSocketCodec（放在MqttCodec之前）
                    if (LogWebSocketData)
                    {
                        WriteLog("为WebSocket服务器添加WebSocketCodec，路径：{0}", WebSocketPath);
                        item.Add(new WebSocketCodec(Log, WebSocketPath));
                    }
                    else
                    {
                        item.Add(new WebSocketCodec(null, WebSocketPath));
                    }

                    // 添加MqttCodec
                    WriteLog("为WebSocket服务器添加MqttCodec");
                    item.Add(new MqttCodec());

                    // 设置服务器属性
                    item.Log = Log;

                    // 添加到服务器集合
                    Servers.Add(item);
                }

                WriteLog("已创建服务器，监听端口：{0}和{1}", Port, WebSocketPort);
            }
        }
        catch (Exception ex)
        {
            WriteLog("创建服务器失败：{0}", ex);
        }
    }

    /// <summary>启动</summary>
    protected override void OnStart()
    {
        // 调用基类的启动方法，初始化MqttServer并启动所有端口
        base.OnStart();

        WriteLog("WebSocket服务器已启动，监听端口：{0}和{1}", Port, WebSocketPort);
    }

    /// <summary>停止</summary>
    /// <param name="reason"></param>
    protected override void OnStop(String? reason)
    {
        // 停止WebSocket服务器
        if (WsServer != null)
        {
            WsServer.Stop(reason);
            Servers.Remove(WsServer);
            WsServer = null;
        }

        // 调用基类的停止方法
        base.OnStop(reason);
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"Mqtt{Port}/WebSocket{WebSocketPort}";

    #endregion
}