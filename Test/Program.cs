﻿using System;
using System.Reflection;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;

namespace Test
{
    internal class Program
    {
        private static void Main(String[] args)
        {
            MachineInfo.RegisterAsync();
            XTrace.UseConsole();

            Console.Write("输出要执行的测试方法序号：");
            var idx = Console.ReadLine().ToInt();

            try
            {
                //Test1();
                var mi = typeof(Program).GetMethod("Test" + idx, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(null, null);
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

        private static void Test2()
        {
            var mi = MachineInfo.Current;

        }

        private static MqttClient _mc;

        private static async void Test3()
        {
            _mc = new MqttClient
            {
                Log = XTrace.Log,
                LogMessage = true,
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
