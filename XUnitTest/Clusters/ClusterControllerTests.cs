using System;
using System.Linq;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using NewLife.Reflection;
using NewLife.Remoting;
using Xunit;

namespace XUnitTestClient.Clusters;

public class ClusterControllerTests
{
    static ClusterServer _server;
    static IServiceProvider _provider;
    public ClusterControllerTests()
    {
        var services = ObjectContainer.Current;
        //services.AddSingleton<ClusterServer>();
        services.AddTransient<ClusterController>();

        _provider = ObjectContainer.Provider;
        _server = new ClusterServer { ServiceProvider = _provider };
    }

    [Fact]
    public void EchoTest()
    {
        var controller = new ClusterController();
        var msg = "Hello";
        var rs = controller.Echo(msg);
        Assert.Equal(msg, rs);

        controller = _provider.GetRequiredService<ClusterController>();

        rs = controller.Echo(msg);
        Assert.Equal(msg, rs);
    }

    [Fact]
    public void JoinTest()
    {
        //var server = _provider.GetRequiredService<ClusterServer>();
        var server = _server;
        var controller = _provider.GetRequiredService<ClusterController>();

        //var context = new ControllerContext { Session = new ApiNetSession() };
        //controller.OnActionExecuting(context);
        controller.SetValue("_cluster", server);

        {
            var info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            var rs = controller.Join(info);
            Assert.NotEmpty(rs.EndPoint);

            Assert.Equal(1, server.Nodes.Count);

            var node = server.Nodes.FirstOrDefault().Value;
            Assert.Equal(1, node.Times);

            info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            rs = controller.Join(info);
            Assert.NotEmpty(rs.EndPoint);

            Assert.Equal(1, server.Nodes.Count);

            node = server.Nodes.FirstOrDefault().Value;
            Assert.Equal(1, node.Times);

            info = new NodeInfo { EndPoint = "127.0.0.1:22883" };
            rs = controller.Join(info);
            Assert.NotEmpty(rs.EndPoint);

            Assert.Equal(2, server.Nodes.Count);

            node = server.Nodes.Last().Value;
            Assert.Equal(1, node.Times);

            info = new NodeInfo { EndPoint = "127.0.0.1:22883" };
            rs = controller.Ping(info);
            Assert.NotEmpty(rs.EndPoint);

            Assert.Equal(2, server.Nodes.Count);

            node = server.Nodes.Last().Value;
            Assert.Equal(2, node.Times);
        }

        {
            var info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            controller.Leave(info);

            Assert.Equal(1, server.Nodes.Count);
            Assert.Equal("127.0.0.1:22883", server.Nodes.FirstOrDefault().Value.EndPoint);
        }
    }
}
