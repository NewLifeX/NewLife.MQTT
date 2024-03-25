using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Clusters;
using NewLife.Net;
using Xunit;

namespace XUnitTestClient.Clusters;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class MqttClusterTests
{
    static MqttServer _server;

    public MqttClusterTests()
    {
        var services = ObjectContainer.Current;
        services.AddTransient<ClusterController>();

        _server ??= new MqttServer
        {
            Port = 61883,
            ServiceProvider = services.BuildServiceProvider(),

            Log = XTrace.Log
        };
    }

    [Fact]
    public void StartTest()
    {
        XTrace.WriteLine(nameof(StartTest));

        var server = _server;
        Assert.NotNull(server);

        var ip = NetHelper.MyIP();
        var endpoints = new List<String>();
        var servers = new ClusterServer[4];
        for (var i = 0; i < servers.Length; i++)
        {
            servers[i] = new ClusterServer
            {
                Port = 2883 + ((i + 2) * 10000),
                ServiceProvider = _server.ServiceProvider,

                Log = XTrace.Log
            };
            servers[i].Start();
            //endpoints.Add($"127.0.0.6:{servers[i].Port}");
            endpoints.Add($"{ip}:{servers[i].Port}");
        }

        server.ClusterNodes = [.. endpoints];
        server.Cluster = new ClusterServer
        {
            Port = 62883,
        };

        server.Start();

        var cluster = server.Cluster;
        Assert.NotNull(cluster);

        XTrace.WriteLine("集群节点数：{0}", cluster.Nodes.Count);

        var ms = servers.Length * 1000 / 100;
        if (ms < 1000) ms = 1000;
        Thread.Sleep(ms);

        Assert.Equal(servers.Length, cluster.Nodes.Count);

        // 检测端口是否打开
        NetHelper.CheckPort(IPAddress.Any, NetType.Tcp, server.Port);
        NetHelper.CheckPort(IPAddress.Any, NetType.Tcp, cluster.Port);
    }

    [Fact]
    public void StopTest()
    {
        XTrace.WriteLine(nameof(StopTest));

        var server = _server;
        Assert.NotNull(server);

        server.Stop("Stop");
    }
}
