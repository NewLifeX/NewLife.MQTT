using System.Linq;
using System.Net;
using System.Threading;
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
        services.AddTransient<ClusterController>();

        _server ??= new ClusterServer
        {
            Port = 2883,
            ServiceProvider = services.BuildServiceProvider(),

            Log = XTrace.Log
        };
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

        var pong = await node.Ping(myNode);
        Assert.NotEmpty(pong.EndPoint);

        await node.Leave(myNode);
        Assert.Empty(server.Nodes);
    }

    [Fact]
    public void CreateCluster()
    {
#if DEBUG
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
        }
#else
        var servers = new ClusterServer[1000];
        for (var i = 0; i < servers.Length; i++)
        {
            servers[i] = new ClusterServer
            {
                Port = 20000 + i + 1,
                ServiceProvider = _server.ServiceProvider,
            };
            servers[i].Start();
        }
#endif

        _server.Nodes.Clear();

        // 加入集群
        for (var i = 0; i < servers.Length; i++)
        {
            _server.AddNode(servers[i].GetNodeInfo().EndPoint);
        }

        Assert.Equal(servers.Length, _server.Nodes.Count);

        //Thread.Sleep(1000);

        XTrace.WriteLine("集群节点数：{0}", _server.Nodes.Count);

        var ms = servers.Length * 1000 / 100;
        if (ms < 1000) ms = 1000;
        Thread.Sleep(ms);

        // 验证集群
        for (var i = 0; i < servers.Length; i++)
        {
            Assert.Equal(1, servers[i].Nodes.Count);
        }
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
