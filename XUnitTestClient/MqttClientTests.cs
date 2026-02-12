using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using NewLife.UnitTest;
using Xunit;

namespace XUnitTestClient;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class MqttClientTests
{
    private static MqttServer _server;
    private static MqttClient _client;
    private static readonly Queue<String> _mq = new Queue<String>();

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

                XTrace.WriteLine("消费消息：[{0}] {1}", pm.Topic, msg);
            };

            _client = mc;
        }
    }

    [TestOrder(0)]
    [Fact]
    public void TestServer()
    {
        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);
        //services.AddSingleton<IMqttHandler, MqttHandler>();
        services.AddSingleton<IMqttHandler, MqttController>();

        _server = new MqttServer
        {
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };

        _server.Start();
    }

    [TestOrder(1)]
    [Fact]
    public async Task TestConnect()
    {
        // 连接
        var rs = await _client.ConnectAsync();
        Assert.NotNull(rs);
        //Assert.True(rs.SessionPresent);
        Assert.Equal(ConnectReturnCode.Accepted, rs.ReturnCode);
    }

    [TestOrder(2)]
    [Fact]
    public async Task TestPublish()
    {
        var msg = "学无先后达者为师" + Rand.NextString(8);
        var rs = await _client.PublishAsync("newlifeTopic", msg);
        Assert.Null(rs);

        //Assert.Equal(msg, _mq.Dequeue());
    }

    [TestOrder(3)]
    [Theory]
    [InlineData(QualityOfService.AtMostOnce)]
    [InlineData(QualityOfService.AtLeastOnce)]
    [InlineData(QualityOfService.ExactlyOnce)]
    public async Task TestPublishQos(QualityOfService qos)
    {
        var msg = "学无先后达者为师" + Rand.NextString(8);
        var rs = await _client.PublishAsync("QosTopic", msg, qos);
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

    [TestOrder(4)]
    [Fact]
    public async Task TestSubscribe()
    {
        var rs = await _client.SubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
        Assert.NotNull(rs);
        Assert.Equal(2, rs.GrantedQos.Count);
        Assert.Equal(QualityOfService.AtMostOnce, rs.GrantedQos[0]);
        Assert.Equal(QualityOfService.AtMostOnce, rs.GrantedQos[1]);
    }

    [TestOrder(5)]
    [Fact]
    public async Task TestUnsubscribe()
    {
        var rs = await _client.UnsubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
        Assert.NotNull(rs);
    }

    [TestOrder(7)]
    [Fact]
    public async Task TestPing()
    {
        var rs = await _client.PingAsync();
        Assert.NotNull(rs);
    }

    [TestOrder(10)]
    [Fact]
    public async Task TestDisconnect()
    {
        //await _client.ConnectAsync();

        await _client.DisconnectAsync();

        //_client.TryDispose();
        //_client = null;
    }

    [Fact]
    public void TestMqttVersion()
    {
        // 测试默认版本是3.1.1
        var client1 = new MqttClient
        {
            ClientId = "test_v311",
            Server = "tcp://127.0.0.1:1883"
        };
        Assert.Equal(MqttVersion.V311, client1.Version);

        // 测试设置MQTT 3.1
        var client2 = new MqttClient
        {
            ClientId = "test_v310",
            Server = "tcp://127.0.0.1:1883",
            Version = MqttVersion.V310
        };
        Assert.Equal(MqttVersion.V310, client2.Version);

        // 测试设置MQTT 5.0
        var client3 = new MqttClient
        {
            ClientId = "test_v500",
            Server = "tcp://127.0.0.1:1883",
            Version = MqttVersion.V500
        };
        Assert.Equal(MqttVersion.V500, client3.Version);
    }

    [Fact]
    public void TestConnectMessageProtocolLevel()
    {
        // 测试默认协议级别（3.1.1）
        var msg1 = new ConnectMessage
        {
            ClientId = "test_client_1"
        };
        Assert.Equal((Byte)0x04, msg1.ProtocolLevel);

        // 测试设置协议级别
        var msg2 = new ConnectMessage
        {
            ClientId = "test_client_2",
            ProtocolLevel = (Byte)MqttVersion.V500
        };
        Assert.Equal((Byte)0x05, msg2.ProtocolLevel);
    }
}