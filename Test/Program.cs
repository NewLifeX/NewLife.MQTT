using System;
using System.Reflection;
using NewLife;
using NewLife.Log;
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
                Test2();
                //var mi = typeof(Program).GetMethod("Test" + idx, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                //if (mi != null) mi.Invoke(null, null);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            Console.WriteLine("OK");
            Console.Read();
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
            var server = new MqttServer
            {
                Log = XTrace.Log,
                SessionLog = XTrace.Log,
            };
            server.AddHandler(new MyHandler());
            server.Start();

            _server = server;

            var client = new MqttClient
            {
                Server = "tcp://127.0.0.1:1883",
                Log = XTrace.Log
            };

            await client.ConnectAsync();

            for (var i = 0; i < 10; i++)
            {
                var qos = (QualityOfService)(i % 3);

                await client.PublishAsync("test", new { name = "p" + i, value = Rand.Next() }, qos);
            }

            await client.DisconnectAsync();
        }

        class MyHandler
        {
            [MqttType(MqttType.Connect)]
            public MqttMessage OnConnect(INetSession session, MqttMessage message)
            {
                var conn = message as ConnectMessage;

                return new ConnAck { ReturnCode = ConnectReturnCode.Accepted };
            }
        }

        private static MqttClient _mc;

        private static async void Test3()
        {
            _mc = new MqttClient
            {
                Log = XTrace.Log,
                Server = "tcp://129.211.129.92:1883",
                //UserName = "admin",
                //Password = "admin",
                ClientId = Guid.NewGuid() + "",
            };

            await _mc.ConnectAsync();

            var rt = await _mc.SubscribeAsync("/test/#", (e) =>
            {
                XTrace.WriteLine("sub:" + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
            });

            Console.Read();
        }
    }
}
