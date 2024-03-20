using System;
using System.Linq;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTestClient.Clusters;

public class ClusterControllerTests
{
    static IServiceProvider _provider;
    public ClusterControllerTests()
    {
        var services = ObjectContainer.Current;
        services.AddSingleton<ClusterServer>();
        services.AddTransient<ClusterController>();

        _provider = ObjectContainer.Provider;
    }

    [Fact]
    public void EchoTest()
    {
        var controller = new ClusterController(null);
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
        var server = _provider.GetRequiredService<ClusterServer>();

        var controller = _provider.GetRequiredService<ClusterController>();
        {
            var info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            controller.Join(info);

            Assert.Equal(1, server.Nodes.Count);

            info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            controller.Join(info);

            Assert.Equal(1, server.Nodes.Count);

            info = new NodeInfo { EndPoint = "127.0.0.1:22883" };
            controller.Join(info);

            Assert.Equal(2, server.Nodes.Count);
        }

        {
            var info = new NodeInfo { EndPoint = "127.0.0.1:12883" };
            controller.Leave(info);

            Assert.Equal(1, server.Nodes.Count);
            Assert.Equal("127.0.0.1:22883", server.Nodes.FirstOrDefault().Value.EndPoint);
        }
    }
}
