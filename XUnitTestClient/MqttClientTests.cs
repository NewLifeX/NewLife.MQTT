using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;
using Xunit.Extensions.Ordering;

namespace XUnitTestClient
{
    [TestCaseOrderer("Xunit.Extensions.Ordering.TestCaseOrderer", "Xunit.Extensions.Ordering")]
    public class MqttClientTests
    {
        private static MqttClient _client;
        private static Queue<String> _mq = new Queue<string>();

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

            if (_client == null)
            {
                mc.Received += (s, e) =>
                {
                    var pm = e.Arg;
                    var msg = pm.Payload.ToStr();
                    _mq.Enqueue(msg);

                    XTrace.WriteLine("消费消息：[{0}] {1}", pm.TopicName, msg);
                };

                _client = mc;
            }
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
            var msg = "学无先后达者为师" + Rand.NextString(8);
            var rs = await _client.PublicAsync("newlifeTopic", msg);
            Assert.Null(rs);

            //Assert.Equal(msg, _mq.Dequeue());
        }

        [Theory(Timeout = 3_000), Order(4)]
        [InlineData(QualityOfService.AtMostOnce)]
        [InlineData(QualityOfService.AtLeastOnce)]
        [InlineData(QualityOfService.ExactlyOnce)]
        public async void TestPublicQos(QualityOfService qos)
        {
            var msg = "学无先后达者为师" + Rand.NextString(8);
            var rs = await _client.PublicAsync("QosTopic", msg, qos);
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

        [Fact(Timeout = 3_000), Order(2)]
        public async void TestSubscribe()
        {
            var rs = await _client.SubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
            Assert.NotNull(rs);
            Assert.Equal(2, rs.ReturnCodes.Count);
            Assert.Equal(QualityOfService.AtMostOnce, rs.ReturnCodes[0]);
            Assert.Equal(QualityOfService.AtMostOnce, rs.ReturnCodes[1]);
        }

        [Fact(Timeout = 3_000), Order(10)]
        public async void TestUnsubscribe()
        {
            var rs = await _client.UnsubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
            Assert.NotNull(rs);
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