using System;
using System.Collections.Generic;
using NewLife;
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
    private MqttServer _server;
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
    public async void TestConnect()
    {
        // 连接
        var rs = await _client.ConnectAsync();
        Assert.NotNull(rs);
        //Assert.True(rs.SessionPresent);
        Assert.Equal(ConnectReturnCode.Accepted, rs.ReturnCode);
    }

    [TestOrder(2)]
    [Fact]
    public async void TestPublish()
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
    public async void TestPublishQos(QualityOfService qos)
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
    public async void TestSubscribe()
    {
        var rs = await _client.SubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
        Assert.NotNull(rs);
        Assert.Equal(2, rs.GrantedQos.Count);
        Assert.Equal(QualityOfService.AtMostOnce, rs.GrantedQos[0]);
        Assert.Equal(QualityOfService.AtMostOnce, rs.GrantedQos[1]);
    }

    [TestOrder(5)]
    [Fact]
    public async void TestUnsubscribe()
    {
        var rs = await _client.UnsubscribeAsync(new[] { "newlifeTopic", "QosTopic" });
        Assert.NotNull(rs);
    }

    [TestOrder(7)]
    [Fact]
    public async void TestPing()
    {
        var rs = await _client.PingAsync();
        Assert.NotNull(rs);
    }

    [TestOrder(10)]
    [Fact]
    public async void TestDisconnect()
    {
        //await _client.ConnectAsync();

        await _client.DisconnectAsync();

        //_client.TryDispose();
        //_client = null;
    }
}