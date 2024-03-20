using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.MQTT.Clusters;
using NewLife.Net;
using NewLife.UnitTest;
using Xunit;

namespace XUnitTestClient.Clusters;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class ClusterServerTests
{
    ClusterServer _server;

    public ClusterServerTests()
    {
        _server = new ClusterServer
        {
            Port = 2883,
            Log = XTrace.Log
        };
    }

    //[TestOrder(1)]
    [Fact]
    public void StartTest()
    {
        var server = _server;
        Assert.NotNull(server);

        server.Start();

        Assert.NotNull(server.ServiceProvider);

        // 检测端口是否打开
        NetHelper.CheckPort(IPAddress.Any, NetType.Tcp, server.Port);
    }

    [Fact]
    public void StopTest()
    {
        var server = _server;
        Assert.NotNull(server);

        server.Stop("Stop");
    }
}
