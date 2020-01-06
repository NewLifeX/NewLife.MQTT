using System;
using NewLife.Log;
using NewLife.MQTT;
using Xunit;

namespace XUnitTestClient
{
    public class MqttSignTests
    {
        [Fact]
        public void Test()
        {
            var productKey = "a1X2bEn****";
            var deviceName = "example1";
            var deviceSecret = "ga7XA6KdlEeiPXQPpRbAjOZXwG8y****";
            //var timestamp = DateTime.UtcNow.ToLong();

            // 计算MQTT连接参数
            var sign = new MqttSign();
            sign.Calculate(productKey, deviceName, deviceSecret);

            Assert.Equal("example1&a1X2bEn****", sign.UserName);
            //Assert.Equal("B144BAE52CF8A5B5F2B37743D8440C85CDC05861A1BF87C564E4FB2CBF43FAFB", sign.Password);
            //Assert.Equal("a1X2bEn****.example1|timestamp=1578321210292,_v=newlife-mqtt-1.0,securemode=2,signmethod=hmacsha256|", sign.ClientId);
            Assert.NotEmpty(sign.Password);
            Assert.Equal(32, sign.Password.ToHex().Length);
            Assert.NotEmpty(sign.ClientId);

            var cs = sign.ClientId.Split('|', ',');
            Assert.Equal(6, cs.Length);
            Assert.Equal($"{productKey}.{deviceName}", cs[0]);
            Assert.Equal("_v=newlife-mqtt-1.0", cs[2]);
            Assert.Equal("securemode=2", cs[3]);
            Assert.Equal("signmethod=hmacsha256", cs[4]);
            Assert.Empty(cs[5]);
        }

        [Fact]
        public void Test2()
        {
            var productKey = "a1X2bEn****";
            var deviceName = "example1";
            var deviceSecret = "ga7XA6KdlEeiPXQPpRbAjOZXwG8y****";

            var client = new AliyunMqttClient(productKey, deviceName, deviceSecret);

            Assert.Equal("example1&a1X2bEn****", client.UserName);
            //Assert.Equal("B144BAE52CF8A5B5F2B37743D8440C85CDC05861A1BF87C564E4FB2CBF43FAFB", sign.Password);
            //Assert.Equal("a1X2bEn****.example1|timestamp=1578321210292,_v=newlife-mqtt-1.0,securemode=2,signmethod=hmacsha256|", sign.ClientId);
            Assert.NotEmpty(client.Password);
            Assert.Equal(32, client.Password.ToHex().Length);
            Assert.NotEmpty(client.ClientId);

            var cs = client.ClientId.Split('|', ',');
            Assert.Equal(6, cs.Length);
            Assert.Equal($"{productKey}.{deviceName}", cs[0]);
            Assert.Equal("_v=newlife-mqtt-1.0", cs[2]);
            Assert.Equal("securemode=2", cs[3]);
            Assert.Equal("signmethod=hmacsha256", cs[4]);
            Assert.Empty(cs[5]);
        }
    }
}