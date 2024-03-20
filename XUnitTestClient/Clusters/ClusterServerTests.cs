﻿using System.Linq;
using System.Net;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using NewLife.Net;
using Xunit;

namespace XUnitTestClient.Clusters;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class ClusterServerTests
{
    static ClusterServer _server;

    public ClusterServerTests()
    {
        var services = ObjectContainer.Current;
        //services.AddSingleton<ClusterServer>();
        services.AddTransient<ClusterController>();

        if (_server == null)
        {
            _server = new ClusterServer
            {
                Port = 2883,
                ServiceProvider = services.BuildServiceProvider(),

                Log = XTrace.Log
            };

            services.AddSingleton(_server);
        }
    }

    //[TestOrder(1)]
    [Fact]
    public void StartTest()
    {
        XTrace.WriteLine(nameof(StartTest));

        var server = _server;
        Assert.NotNull(server);

        server.Start();

        Assert.NotNull(server.ServiceProvider);

        // 检测端口是否打开
        NetHelper.CheckPort(IPAddress.Any, NetType.Tcp, server.Port);
    }

    [Fact]
    public async void JoinTest()
    {
        XTrace.WriteLine(nameof(JoinTest));

        var server = _server;

        var ip = NetHelper.MyIP();
        var myNode = new NodeInfo { EndPoint = "127.0.0.2:2883" };

        var node = new ClusterNode { EndPoint = $"{ip}:{server.Port}" };

        var txt = "Hello";
        var rs = await node.Echo(txt);
        Assert.Equal(txt, rs);

        await node.Join(myNode);
        Assert.Single(server.Nodes);

        var remoteNode = server.Nodes.Values.First();
        Assert.Equal(myNode.EndPoint, remoteNode.EndPoint);

        await node.Leave(myNode);
        Assert.Empty(server.Nodes);
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