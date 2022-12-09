using System.Text;
using System.Text.Unicode;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Security;

namespace Test
{
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
                Test3();
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
                XTrace.WriteLine(MqttTopicFilter.Matches(pub, item) + "");
            }
            var sub1 = new String[] { "test/#", "/test/sss/test/test", "/test//#" };

            foreach (var item in sub1)
            {
                XTrace.WriteLine(MqttTopicFilter.Matches(pub, item) + "");
            }
        }

        private static MqttServer _server;

        private static async void Test2()
        {
            var ioc = ObjectContainer.Current;
            ioc.AddSingleton<ILog>(XTrace.Log);
            ioc.AddTransient<IMqttHandler, MyHandler>();

            var server = new MqttServer
            {
                Provider = ioc.BuildServiceProvider(),

                Log = XTrace.Log,
                SessionLog = XTrace.Log,
            };
            //server.AddHandler(new MyHandler());
            server.Start();

            _server = server;

            //var client = new MqttClient
            //{
            //    Server = "tcp://127.0.0.1:1883",
            //    Log = XTrace.Log
            //};

            //await client.ConnectAsync();

            //for (var i = 0; i < 10; i++)
            //{
            //    var qos = (QualityOfService)(i % 3);

            //    await client.PublishAsync("test", new { name = "p" + i, value = Rand.Next() }, qos);

            //    await Task.Delay(1000);
            //}

            //await client.DisconnectAsync();
        }

        private class MyHandler : MqttHandler
        {
            private readonly ILog _log;

            public MyHandler(ILog log) => _log = log;

            protected override ConnAck OnConnect(INetSession session, ConnectMessage message)
            {
                _log.Info("客户端[{0}]连接 user={0} pass={1} clientId={2}", session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

                return base.OnConnect(session, message);
            }

            protected override MqttMessage OnDisconnect(INetSession session, DisconnectMessage message)
            {
                _log.Info("客户端[{0}]断开", session.Remote);

                return base.OnDisconnect(session, message);
            }

            protected override MqttIdMessage OnPublish(INetSession session, PublishMessage message)
            {
                _log.Info("发布[{0}:qos={1}]: {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());

                return base.OnPublish(session, message);
            }
        }

        private static MqttClient _mc;

        /// <summary>
        /// 测试完整发布订阅
        /// </summary>
        private static async void Test3()
        {
            _mc = new MqttClient
            {
                Log = XTrace.Log,
                Server = "tcp://127.0.0.1:1883",
                //UserName = "admin",
                //Password = "admin",
                ClientId = Guid.NewGuid() + "",
            };

            await _mc.ConnectAsync();
            //订阅“/test”主题
            var rt = await _mc.SubscribeAsync("/test", (e) =>
            {
                XTrace.WriteLine("收到消息:" + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
            });
            while (true)
            {
                //每2秒向“/test”主题发布一条消息
                try
                {
                    var msg = "学无先后达者为师" + Rand.NextString(8);
                    await _mc.PublishAsync("/test", msg);
                }
                catch (Exception ex)
                {
                }
                await Task.Delay(2000);
            }
        }
    }
}