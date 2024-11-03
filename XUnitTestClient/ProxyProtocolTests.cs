using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.ProxyProtocol;
using Xunit;

namespace XUnitTestClient;

public class ProxyProtocolTests
{
    [Fact]
    public void Test1()
    {
        var str = "PROXY TCP4 10.10.10.10 192.168.0.1 12345 80\r\n";
        var msg = new ProxyMessage();
        var rs = msg.Read(str.GetBytes());

        Assert.True(rs > 0);
        Assert.Equal(str.Length, rs);
        Assert.Equal("TCP4", msg.Protocol);
        Assert.Equal("10.10.10.10", msg.ClientIP);
        Assert.Equal("192.168.0.1", msg.ProxyIP);
        Assert.Equal(12345, msg.ClientPort);
        Assert.Equal(80, msg.ProxyPort);
    }

    [Fact]
    public async Task TestProxyProtocol()
    {
        XTrace.WriteLine("测试ProxyProtocol");

        var services = ObjectContainer.Current;
        services.AddSingleton(XTrace.Log);
        services.AddSingleton<IMqttHandler, MqttController>();

        using var server = new MqttServer
        {
            Port = 2883,
            ServiceProvider = services.BuildServiceProvider(),

            Log = XTrace.Log,
            SessionLog = XTrace.Log,
            //SocketLog = XTrace.Log,
        };

        server.Start();

        using var client = new MqttClient
        {
            Log = XTrace.Log,
            Server = "tcp://127.0.0.1:" + server.Port,

            ClientId = Environment.MachineName,
            UserName = "stone",
            Password = "Pass@word",

            // 注入编码器，用于测试
            EnableProxyProtocol = true,
        };

        client.Received += (s, e) =>
        {
            var pm = e.Arg;
            var msg = pm.Payload.ToStr();

            XTrace.WriteLine("接收：[{0}] {1}", pm.Topic, msg);
        };

        await client.ConnectAsync();

        await client.SubscribeAsync("/test", pm =>
        {
            XTrace.WriteLine("消费消息：[{0}] {1}", pm.Topic, pm.Payload.ToStr());
        });

        await Task.Delay(1_000);

        await client.PublishAsync("/test", "Hello MQTT");

        await Task.Delay(3_000);
        XTrace.WriteLine("finish");
    }
}
