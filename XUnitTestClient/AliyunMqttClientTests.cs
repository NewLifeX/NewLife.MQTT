using System;
using System.Collections.Generic;
using System.Text;
using NewLife.Log;
using NewLife.MQTT;
using Xunit;

namespace XUnitTestClient
{
    public class AliyunMqttClientTests
    {
        [Fact]
        public async void Test3()
        {
            var client = new AliyunMqttClient("a18RQ72tLHD", "dev1", "6oSl3CjHKM13J50DVVWNF3WbWWJjhAUf");
            client.Log = XTrace.Log;
            client.Server = $"tcp://{client.ProductKey}.iot-as-mqtt.cn-shanghai.aliyuncs.com:443";

            await client.ConnectAsync();
            await client.SyncTime();
            await client.PostProperty(new { NickName = "xxx" });
        }
    }
}
