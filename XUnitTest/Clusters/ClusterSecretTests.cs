#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

/// <summary>集群共享密钥测试。覆盖带密钥组建、密钥热更新同步与密钥不匹配拒绝</summary>
[Collection("IntegrationTests")]
public class ClusterSecretTests
{
    private static ClusterServer CreateServer(String? secret) => new()
    {
        Port = 0,
        ClusterSecret = secret,
        ServiceProvider = ObjectContainer.Current.BuildServiceProvider(),
        Log = XTrace.Log,
    };

    /// <summary>轮询等待条件成立</summary>
    private static async Task<Boolean> WaitFor(Func<Boolean> condition, Int32 milliseconds = 8000)
    {
        for (var i = 0; i < milliseconds / 100; i++)
        {
            if (condition()) return true;

            await Task.Delay(100);
        }

        return condition();
    }

    [Fact(DisplayName = "集群密钥：带密钥的双节点双向加入并互通")]
    public async Task Cluster_WithSecret_BothSidesJoin()
    {
        using var a = CreateServer("secret-A");
        a.Start();
        using var b = CreateServer("secret-A");
        b.Start();

        // 节点本地配置必须携带密钥，否则心跳握手会被对端令牌校验拒绝
        Assert.True(b.AddNode(a.GetNodeInfo().EndPoint));

        var joined = await WaitFor(() => a.Nodes.Count == 1 && b.Nodes.Count == 1);
        Assert.True(joined, $"双向加入失败：a={a.Nodes.Count}，b={b.Nodes.Count}");

        // 加入后 RPC 仍可正常调用
        var pong = await b.Nodes.Values.First().Ping(b.GetNodeInfo());
        Assert.NotNull(pong);
        Assert.Equal("secret-A", pong.ClusterSecret);
    }

    [Fact(DisplayName = "集群密钥：密钥轮换后同步到对端且仍互通")]
    public async Task Cluster_SecretRotation_SyncedToPeers()
    {
        using var a = CreateServer("secret-old");
        a.Start();
        using var b = CreateServer("secret-old");
        b.Start();

        Assert.True(b.AddNode(a.GetNodeInfo().EndPoint));
        Assert.True(await WaitFor(() => a.Nodes.Count == 1 && b.Nodes.Count == 1), "轮换前双向加入失败");

        // A 侧热更新密钥，应通过集群接口同步给 B
        a.ClusterSecret = "secret-new";

        Assert.True(await WaitFor(() => b.ClusterSecret == "secret-new"), "对端未同步新密钥");

        // 轮换后请求携带新令牌，双方令牌校验通过
        var pong = await b.Nodes.Values.First().Ping(b.GetNodeInfo());
        Assert.NotNull(pong);
        Assert.Equal("secret-new", pong.ClusterSecret);
    }

    [Fact(DisplayName = "集群密钥：密钥不匹配的节点无法加入")]
    public async Task Cluster_WrongSecret_Rejected()
    {
        using var a = CreateServer("secret-A");
        a.Start();
        using var c = CreateServer("secret-B");
        c.Start();

        var endpoint = c.GetNodeInfo().EndPoint;
        Assert.True(c.AddNode(a.GetNodeInfo().EndPoint));

        await Task.Delay(3000);

        Assert.False(a.Nodes.ContainsKey(endpoint), "密钥不匹配的节点不应加入集群");
    }

    [Fact(DisplayName = "集群密钥：轮换过渡期内旧密钥仍被接受")]
    public async Task Cluster_SecretRotation_OldSecretAcceptedInWindow()
    {
        using var a = CreateServer("secret-old");
        a.Start();

        // 尚未收到更新的节点继续使用旧密钥
        using var legacy = new ClusterNode { EndPoint = a.GetNodeInfo().EndPoint, Secret = "secret-old" };
        Assert.Equal("ping", await legacy.Echo("ping"));

        a.ClusterSecret = "secret-new";

        // 过渡窗口内旧密钥仍然有效，避免各节点更新进度不一致时相互拒绝
        Assert.Equal("ping", await legacy.Echo("ping"));
    }
}
