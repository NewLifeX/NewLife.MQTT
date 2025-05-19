using NewLife;
using NewLife.Log;
using NewLife.MQTT.WebSocket;
using NewLife.Net;

// 启用控制台日志，拦截所有异常
XTrace.UseConsole();

// 设置日志级别为调试，以便查看所有数据
XTrace.Log.Level = LogLevel.Debug;

// 检查端口是否被占用
CheckPort(1883);
CheckPort(8083);

// 创建WebSocket服务器
var server = new WebSocketServer
{
    Port = 1883,                // 标准MQTT端口
    WebSocketPort = 8083,       // WebSocket端口
    Log = XTrace.Log,           // 日志
    SessionLog = XTrace.Log,    // 会话日志
    SocketLog = XTrace.Log,     // Socket日志
    LogSend = true,             // 记录发送数据
    LogReceive = true,          // 记录接收数据
    LogWebSocketData = true     // 记录WebSocket数据
};

// 启动服务器
server.Start();

Console.WriteLine("WebSocket服务器已启动，按任意键退出...");
Console.WriteLine($"MQTT端口: {server.Port}");
Console.WriteLine($"WebSocket端口: {server.WebSocketPort}");
Console.WriteLine("两个端口共享相同的数据和逻辑，由NetServer统一管理");
Console.WriteLine($"服务器数: {server.Servers.Count}");
Console.WriteLine($"会话数: {server.SessionCount}");
Console.WriteLine();
Console.WriteLine("WebSocket over MQTT功能已启用，所有数据将被打印");
Console.WriteLine("可以使用WebSocket客户端连接到ws://localhost:8083/mqtt");
Console.WriteLine("或使用MQTT客户端连接到tcp://localhost:1883");
Console.WriteLine($"WebSocket路径: {server.WebSocketPath}");

// 等待用户按键退出
Console.ReadKey();

// 停止服务器
server.Stop("用户退出");

// 检查端口是否被占用
static void CheckPort(int port)
{
    try
    {
        var uri = new NetUri(NetType.Tcp, "*", port);
        if (uri.CheckPort())
        {
            Console.WriteLine($"端口 {port} 已被占用");
        }
        else
        {
            Console.WriteLine($"端口 {port} 可用");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"检查端口 {port} 时出错: {ex.Message}");
    }
}
