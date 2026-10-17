#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

/// <summary>集群订阅 RPC 测试。覆盖订阅/退订经 HTTP RPC 的数组参数绑定与远端登记/移除（FIX-CLUS-1：参数绑定把 JSON 数组解码为对象列表后无法直接强转）</summary>
public class ClusterRpcSubscribeTests
{
    [Fact(DisplayName = "集群RPC：订阅/退订数组参数绑定并在远端交换机登记/移除")]
    public async Task Subscribe_Unsubscribe_ArrayBinding()
    {
        using var server = new ClusterServer { Name = "rpc_sub", Port = 0, Log = XTrace.Log };
        using var exchange = new ClusterExchange { Cluster = server };
        server.ClusterExchange = exchange;
        server.Start();

        using var node = new ClusterNode { EndPoint = $"127.0.0.1:{server.Port}" };
        var infos = new[]
        {
            new SubscriptionInfo { Topic = "rpc/alpha", ClientId = "client-1", Endpoint = "127.0.0.1:10001" },
        };

        // 订阅：远端交换机登记该主题
        var sub = await node.Subscribe(infos);
        Assert.Equal("OK", sub);
        Assert.True(exchange.GetAllSubscriptions().ContainsKey("rpc/alpha"), "订阅关系未在远端交换机登记");

        // 退订：远端交换机移除该主题
        var unsub = await node.Unsubscribe(infos);
        Assert.Equal("OK", unsub);
        Assert.False(exchange.GetAllSubscriptions().ContainsKey("rpc/alpha"), "退订后订阅关系仍存在");
    }
}
