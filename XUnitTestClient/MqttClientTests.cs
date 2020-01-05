using System;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using Xunit;
using Xunit.Extensions.Ordering;

namespace XUnitTestClient
{
    [TestCaseOrderer("Xunit.Extensions.Ordering.TestCaseOrderer", "Xunit.Extensions.Ordering")]
    public class MqttClientTests
    {
        private static MqttClient _client;
        public MqttClientTests()
        {
            var mc = new MqttClient
            {
                Log = XTrace.Log,
                Server = "tcp://127.0.0.1:1883",

                ClientId = Environment.MachineName,
                UserName = "stone",
                Password = "Pass@word",
            };

            if (_client == null) _client = mc;
        }

        [Fact, Order(1)]
        public async void TestConnect()
        {
            // 连接
            var rs = await _client.ConnectAsync();
            Assert.NotNull(rs);
            //Assert.True(rs.SessionPresent);
            Assert.Equal(ConnectReturnCode.Accepted, rs.ReturnCode);
        }

        [Fact(Timeout = 3_000), Order(3)]
        public async void TestPublic()
        {
            var rs = await _client.PublicAsync("newlifeTopic", "学无先后达者为师".GetBytes());
            Assert.Null(rs);
        }

        [Theory(Timeout = 3_000), Order(4)]
        [InlineData(QualityOfService.AtMostOnce)]
        [InlineData(QualityOfService.AtLeastOnce)]
        [InlineData(QualityOfService.ExactlyOnce)]
        public async void TestPublicQos(QualityOfService qos)
        {
            var rs = await _client.PublicAsync("QosTopic", "学无先后达者为师", qos);
            switch (qos)
            {
                case QualityOfService.AtMostOnce:
                    Assert.Null(rs);
                    break;
                case QualityOfService.AtLeastOnce:
                    var ack = rs as PubAck;
                    Assert.NotNull(ack);
                    Assert.NotEqual(0, rs.Id);
                    break;
                case QualityOfService.ExactlyOnce:
                    //var rec = rs as PubRec;
                    //Assert.NotNull(rec);
                    var cmp = rs as PubComp;
                    Assert.NotNull(cmp);
                    Assert.NotEqual(0, rs.Id);
                    break;
            }
        }

        [Fact(Timeout = 3_000), Order(12)]
        public async void TestPing()
        {
            var rs = await _client.PingAsync();
            Assert.NotNull(rs);
        }

        [Fact, Order(16)]
        public async void TestDisconnect()
        {
            //await _client.ConnectAsync();

            await _client.DisconnectAsync();

            _client.TryDispose();
            _client = null;
        }
    }
}