#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

/// <summary>分片路由端到端测试：16 节点 × 1024 分片路由正确性验证（CLUS-9 已知测试缺口）</summary>
/// <remarks>
/// 真实启动 16 个 ClusterServer 并经 AddNode 组网，逐节点校验：
/// 所有分片恰好 3 个互异副本、路由目标均在集群内、主题分片索引跨节点一致、节点进出时路由环同步收敛。
/// </remarks>
[Collection("IntegrationTests")]
public class ShardRoutingE2ETests
{
    private const Int32 NodeCount = 16;

    private static ClusterServer[] CreateCluster()
    {
        var servers = new ClusterServer[NodeCount];
        for (var i = 0; i < NodeCount; i++)
        {
            servers[i] = new ClusterServer
            {
                Name = $"shard{i}",
                Port = 0,
                TopicRouter = new TopicRouter(),
                Log = XTrace.Log,
            };
            servers[i].Start();
        }

        // 全互联：每个节点把其余 15 个节点加入集群
        var endpoints = servers.Select(e => e.GetNodeInfo().EndPoint).ToArray();
        for (var i = 0; i < NodeCount; i++)
        {
            for (var j = 0; j < NodeCount; j++)
            {
                if (i != j) servers[i].AddNode(endpoints[j]);
            }
        }

        return servers;
    }

    private static void StopCluster(ClusterServer[] servers)
    {
        foreach (var node in servers)
        {
            if (node == null) continue;

            node.Stop("测试结束");
            node.TryDispose();
        }
    }

    [Fact(DisplayName = "分片路由E2E：16节点组网后 1024 分片全覆盖、每分片三副本、目标合法")]
    public void ShardRouting_16Nodes_CoverageAndReplicas()
    {
        var servers = CreateCluster();
        try
        {
            var endpoints = servers.Select(e => e.GetNodeInfo().EndPoint).ToArray();
            var peerSet = endpoints.ToHashSet();

            // 每个节点都应看到 15 个对端，且路由环含全部 15 个对端
            foreach (var node in servers)
            {
                Assert.Equal(NodeCount - 1, node.Nodes.Count);
                Assert.Equal(NodeCount - 1, node.TopicRouter!.HashRing.NodeCount);
            }

            // 1024 分片全覆盖：每个分片恰好 3 个互异副本，且均属于集群
            foreach (var node in servers)
            {
                var ring = node.TopicRouter!.HashRing;
                for (var shard = 0; shard < ring.ShardCount; shard++)
                {
                    var owners = ring.GetShardNodes(shard);
                    Assert.Equal(ring.ReplicaCount, owners.Count);
                    Assert.Equal(owners.Count, owners.Distinct().Count());
                    Assert.All(owners, o => Assert.Contains(o, peerSet));
                }
            }
        }
        finally
        {
            StopCluster(servers);
        }
    }

    [Fact(DisplayName = "分片路由E2E：主题分片索引跨节点一致、目标数恒为副本数、分片分布不倾斜")]
    public void ShardRouting_Topics_ConsistentAndBalanced()
    {
        var servers = CreateCluster();
        try
        {
            var first = servers[0].TopicRouter!;
            var shardCount = first.HashRing.ShardCount;
            var replicaCount = first.HashRing.ReplicaCount;

            // 主题分片索引跨节点一致（仅由主题哈希决定），路由目标数恒为副本数
            for (var i = 0; i < 256; i++)
            {
                var topic = $"shard/topic/{i}";
                var shard = first.GetShardIndex(topic);
                Assert.InRange(shard, 0, shardCount - 1);

                foreach (var node in servers)
                {
                    Assert.Equal(shard, node.TopicRouter!.GetShardIndex(topic));

                    var targets = node.TopicRouter.GetTargetNodes(topic);
                    Assert.Equal(replicaCount, targets.Count);
                    Assert.Equal(targets.Count, targets.Distinct().Count());
                }
            }

            // 分片分布：路由环只含"转发目标"，即该节点自身除外的其余节点（15 个），
            // 每分片 3 副本均摊到 15 个对端，期望每节点 204~205 个分片，允许 50%~150% 波动
            var counts = new Dictionary<String, Int32>();
            for (var shard = 0; shard < shardCount; shard++)
            {
                foreach (var owner in first.HashRing.GetShardNodes(shard))
                {
                    counts[owner] = counts.TryGetValue(owner, out var c) ? c + 1 : 1;
                }
            }

            var expected = shardCount * replicaCount / (NodeCount - 1);
            Assert.Equal(NodeCount - 1, counts.Count);
            foreach (var item in counts)
                Assert.InRange(item.Value, expected / 2, expected * 3 / 2);
        }
        finally
        {
            StopCluster(servers);
        }
    }

    [Fact(DisplayName = "分片路由E2E：节点经 Ping 加入 / Leave 离开时路由环同步收敛")]
    public async Task ShardRouting_MembershipChange_SyncsRing()
    {
        using var hub = new ClusterServer
        {
            Name = "shardHub",
            Port = 0,
            TopicRouter = new TopicRouter(),
            Log = XTrace.Log,
        };
        hub.Start();

        Assert.Equal(0, hub.TopicRouter!.HashRing.NodeCount);

        using var client = new ClusterNode { EndPoint = $"127.0.0.1:{hub.Port}" };
        var info = new NodeInfo { EndPoint = "127.0.0.1:59901" };

        // 心跳注册的新节点同步进分片路由环
        await client.Ping(info);
        Assert.Contains(info.EndPoint, hub.Nodes.Keys);
        Assert.Equal(1, hub.TopicRouter.HashRing.NodeCount);

        // 节点离开同步出分片路由环
        await client.Leave(info);
        Assert.DoesNotContain(info.EndPoint, hub.Nodes.Keys);
        Assert.Equal(0, hub.TopicRouter.HashRing.NodeCount);
    }
}
