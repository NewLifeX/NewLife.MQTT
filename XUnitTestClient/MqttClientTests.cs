using System;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient
{
    public class MqttClientTests
    {
        [Fact]
        public async void TestConnect()
        {
            var mc = new MqttClient
            {
                Log = XTrace.Log,
                Server = "tcp://127.0.0.1:1883",

                ClientId = Environment.MachineName,
                UserName = "stone",
                Password = "Pass@word",
            };

            // 连接
            var rs = await mc.ConnectAsync();
            Assert.NotNull(rs);
            Assert.True(rs.SessionPresent);
            Assert.Equal(ConnectReturnCode.Accepted, rs.ReturnCode);

            // 断开
            //var rs2 = await mc.ConnectAsync();
        }
    }
}