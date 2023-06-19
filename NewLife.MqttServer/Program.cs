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
var star = new StarFactory();

// 配置
var set = MqttSetting.Current;

// 注册MQTT Broker的指令处理器
services.AddSingleton<DefaultManagedMqttClient, DefaultManagedMqttClient>();
services.AddSingleton<IMqttHandler, MqttController>();

// 注册后台任务 IHostedService
var host = services.BuildHost();
// 服务器
var svr = new MqttServer()
{
    Port = set.Port,
    ServiceProvider = services.BuildServiceProvider(),

    Tracer = star.Tracer,
    Log = XTrace.Log,
};

if (set.Debug) svr.SessionLog = XTrace.Log;

svr.Start();

//host.Add<Worker>();

// 异步阻塞，友好退出
await host.RunAsync();