using System;
using NewLife;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MqttServer;
using NewLife.MQTTServer;
using Stardust;

// 启用控制台日志，拦截所有异常
XTrace.UseConsole();

// 初始化对象容器，提供注入能力
var services = ObjectContainer.Current;
services.AddSingleton(XTrace.Log);

// 配置星尘。自动读取配置文件 config/star.config 中的服务器地址
var star = services.AddStardust();

// 配置
var set = MqttSetting.Current;

var parser = new CommandParser { IgnoreCase = true };
var cfg = parser.Parse(args);
if (cfg.TryGetValue("port", out var port)) set.Port = port.ToInt();
if (cfg.TryGetValue("clusterPort", out var clusterPort)) set.ClusterPort = clusterPort.ToInt();
if (cfg.TryGetValue("clusterNodes", out var clusterNodes)) set.ClusterNodes = clusterNodes;

set.Save();

// 注册MQTT Broker的指令处理器
services.AddSingleton<DefaultManagedMqttClient, DefaultManagedMqttClient>();
services.AddTransient<IMqttHandler, MqttController>();
//services.AddTransient<IMqttHandler, MqttHandler>();
services.AddSingleton<IMqttExchange, MqttExchange>();

// 注册后台任务 IHostedService
var host = services.BuildHost();
// 服务器
var svr = new MqttServer()
{
    Port = set.Port,
    ClusterPort = set.ClusterPort,
    ClusterNodes = set.ClusterNodes.Split(",", ";"),
    ServiceProvider = services.BuildServiceProvider(),

    Tracer = star.Tracer,
    Log = XTrace.Log,
};

if (set.Debug) svr.SessionLog = XTrace.Log;

svr.Start();

if (Runtime.Windows)
    Console.Title = svr + "";

if (star.Service != null)
{
    _ = star.RegisterAsync("MqttServer", $"tcp://*:{svr.Port}");

    if (svr.Cluster != null)
        _ = star.RegisterAsync("MqttCluster", $"tcp://*:{svr.ClusterPort}");
}

Host.RegisterExit((s, e) => svr.Stop(s + ""));

// 异步阻塞，友好退出
await host.RunAsync();