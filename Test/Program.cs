using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;

namespace Test;

internal class Program
{
    private static void Main(String[] args)
    {
        //MachineInfo.RegisterAsync();
        XTrace.UseConsole();

        XTrace.Log.Level = LogLevel.Debug;

        //Console.Write("输出要执行的测试方法序号：");
        //var idx = Console.ReadLine().ToInt();

        try
        {
            TestServer();
            TestClient();
            //var mi = typeof(Program).GetMethod("Test" + idx, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            //if (mi != null) mi.Invoke(null, null);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        Console.WriteLine("Ok");
        while (true)
        {
            Console.ReadLine();
        }
    }

    private static void Test1()
    {
        var sub = new String[] { "/test/#", "/test/+/test/test", "/test/+/#" };

        var pub = "/test/test/test/test";
        foreach (var item in sub)
        {
            XTrace.WriteLine(MqttTopicFilter.IsMatch(pub, item) + "");
        }
        var sub1 = new String[] { "test/#", "/test/sss/test/test", "/test//#" };

        foreach (var item in sub1)
        {
            XTrace.WriteLine(MqttTopicFilter.IsMatch(pub, item) + "");
        }
    }

    private static MqttServer _server;

    private static void TestServer()
    {
        var services = ObjectContainer.Current;
        services.AddSingleton<ILog>(XTrace.Log);
        services.AddTransient<IMqttHandler, MyHandler>();
        services.AddSingleton<IMqttExchange, MqttExchange>();

        var server = new MqttServer
        {
            Port = 1883,
            ServiceProvider = services.BuildServiceProvider(),

            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        server.Start();

        _server = server;
    }

    private class MyHandler : MqttHandler
    {
        private readonly ILog _log;

        public MyHandler(ILog log) => _log = log;

        protected override ConnAck OnConnect(ConnectMessage message)
        {
            _log.Info("客户端[{0}]连接 user={1} pass={2} clientId={3}", Session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

            return base.OnConnect(message);
        }

        protected override MqttMessage OnDisconnect(DisconnectMessage message)
        {
            _log.Info("客户端[{0}]断开", Session.Remote);

            return base.OnDisconnect(message);
        }

        protected override MqttIdMessage OnPublish(PublishMessage message)
        {
            _log.Info("客户端[{0}]发布[{1}:qos={2}]: {3}", Session.Remote, message.Topic, (Int32)message.QoS, message.Payload.ToStr());

            return base.OnPublish(message);
        }
    }

    private static MqttClient _mc;

    /// <summary>
    /// 测试完整发布订阅
    /// </summary>
    private static async void TestClient()
    {
        var client = new MqttClient
        {
            Log = XTrace.Log,
            Server = "tcp://127.0.0.1:1883",
            //UserName = "admin",
            //Password = "admin",
            ClientId = Guid.NewGuid() + "",
        };

        await client.ConnectAsync();

        // 订阅“/test”主题
        var rt = await client.SubscribeAsync("/test", (e) =>
        {
            XTrace.WriteLine("收到消息:" + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
        });

        // 每2秒向“/test”主题发布一条消息
        while (true)
        {
            try
            {
                var msg = "学无先后达者为师" + Rand.NextString(8);
                await client.PublishAsync("/test", msg);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            await Task.Delay(2000);
        }

        _mc = client;
    }
}