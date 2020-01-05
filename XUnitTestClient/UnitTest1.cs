using System;
using NewLife.Log;
using NewLife.MQTT;
using Xunit;

namespace XUnitTestClient
{
    public class UnitTest1
    {
        [Fact]
        public async void Test1()
        {
            var mc = new MqttClient
            {
                Log = XTrace.Log,
                Server = "tcp://127.0.0.1:1883",
            };

            await mc.ConnectAsync();
        }
    }
}