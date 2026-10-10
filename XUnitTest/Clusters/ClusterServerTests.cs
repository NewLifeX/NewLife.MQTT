using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using NewLife.Net;
using NewLife.UnitTest;
using Xunit;

namespace XUnitTest.Clusters;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class ClusterServerTests
{
    static ClusterServer _server;

    public ClusterServerTests()
    {
        var services = ObjectContainer.Current;
        services.AddTransient<ClusterController>();

        if (_server == null)
        {
            _server = new ClusterServer
            {
                Port = 2883,
                ServiceProvider = services.BuildServiceProvider(),

                Log = XTrace.Log
            };
            _server.Start();
        }
    }

    [TestOrder(1)]
    [Fact]
    public void StartTest()
    {
        XTrace.WriteLine(nameof(StartTest));

        var server = _server;
        Assert.NotNull(server);
        Assert.NotNull(server.ServiceProvider);

        // 检测端口是否打开
        NetHelper.CheckPort(IPAddress.Any, NetType.Tcp, server.Port);
    }

    [TestOrder(2)]
    [Fact]
    public async Task JoinTest()
    {
        XTrace.WriteLine(nameof(JoinTest));

        var server = _server;

        var ip = NetHelper.MyIP();
        var myNode = new NodeInfo { EndPoint = "127.0.0.2:2883" };

        using var node = new ClusterNode { EndPoint = $"{ip}:{server.Port}" };

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

    [TestOrder(3)]
    [Fact]
    public void CreateCluster()
    {
        // 小规模随机端口：Port=0 由系统分配空闲端口，避免固定端口段在 Release/CI 下必然端口冲突
        var servers = new ClusterServer[4];
        for (var i = 0; i < servers.Length; i++)
        {
            servers[i] = new ClusterServer
            {
                Name = $"TestCluster{i}",
                Port = 0,
                ServiceProvider = _server.ServiceProvider,

                Log = XTrace.Log
            };
            servers[i].Start();
        }

        _server.Nodes.Clear();

        // 加入集群
        for (var i = 0; i < servers.Length; i++)
        {
            _server.AddNode(servers[i].GetNodeInfo().EndPoint);
        }

        Assert.Equal(servers.Length, _server.Nodes.Count);

        XTrace.WriteLine("主集群节点数：{0}", _server.Nodes.Count);

        // 等待各节点通过心跳同步，最长等待 5 秒
        var deadline = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < deadline)
        {
            var allReady = true;
            for (var i = 0; i < servers.Length; i++)
            {
                if (servers[i].Nodes.Count < 1)
                {
                    allReady = false;
                    break;
                }
            }
            if (allReady) break;

            Thread.Sleep(200);
        }

        // 若心跳同步失败，主动发送 Ping 确保节点发现
        for (var i = 0; i < servers.Length; i++)
        {
            if (servers[i].Nodes.Count < 1)
            {
                foreach (var item in _server.Nodes)
                {
                    try
                    {
                        item.Value.Ping(_server.GetNodeInfo()).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // 忽略单个 Ping 失败
                    }
                }
                break;
            }
        }

        // 再等待 Ping 处理完成
        Thread.Sleep(500);

        // 验证集群
        for (var i = 0; i < servers.Length; i++)
        {
            Assert.Equal(1, servers[i].Nodes.Count);
        }

        // 测试结束后停止所有临时服务器，避免端口被后续测试占用
        foreach (var s in servers) s.Stop("TestDone");
    }

    [TestOrder(4)]
    [Fact]
    public void StopTest()
    {
        XTrace.WriteLine(nameof(StopTest));

        var server = _server;
        Assert.NotNull(server);

        server.Stop("Stop");
    }
}
