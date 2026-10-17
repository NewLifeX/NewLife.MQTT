using System;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT.Clusters;
using Xunit;

namespace XUnitTest.Clusters;

/// <summary>Raft 共识引擎单元测试。覆盖状态转换、投票逻辑、Log 复制和提交</summary>
public class RaftClusterTests
{
    #region 辅助：创建最小化 Raft 环境
    private static RaftCluster CreateRaft(Int32 nodeCount = 3)
    {
        // 独立容器：避免并行测试类同时修改全局 ObjectContainer.Current
        var services = new ObjectContainer();
        services.AddTransient<ClusterController>();

        var cluster = new ClusterServer
        {
            Port = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
        };
        cluster.Start();

        // 添加远程节点（仅占位，不实际连接）
        for (var i = 1; i < nodeCount; i++)
        {
            var node = new ClusterNode
            {
                EndPoint = $"127.0.0.{i + 1}:{9000 + i}",
            };
            cluster.Nodes.TryAdd(node.EndPoint, node);
        }

        var raft = new RaftCluster
        {
            Cluster = cluster,
        };
        raft.Start();

        return raft;
    }

    private static void DestroyRaft(RaftCluster raft)
    {
        raft.Stop();
        raft.Dispose();
        raft.Cluster.Dispose();
    }
    #endregion

    #region 状态转换
    [Fact(DisplayName = "Raft 初始状态为 Follower")]
    public void InitialState_IsFollower()
    {
        using var raft = CreateRaft(3);
        try
        {
            Assert.Equal(RaftState.Follower, raft.State);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "Raft 不足 3 节点时降级为 RPC 广播模式")]
    public void LessThan3Nodes_FallbackToRpc()
    {
        using var raft = CreateRaft(2);
        try
        {
            Assert.Equal(RaftState.Follower, raft.State);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }
    #endregion

    #region 投票逻辑
    [Fact(DisplayName = "HandleRequestVote：同一任期不重复投票")]
    public void HandleRequestVote_DontVoteTwice()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var request1 = new RaftVoteRequest
            {
                Term = 1,
                CandidateId = "127.0.0.2:9002",
                LastLogIndex = 0,
                LastLogTerm = 0,
            };
            var response1 = raft.HandleRequestVote(request1);
            Assert.True(response1.VoteGranted);

            var request2 = new RaftVoteRequest
            {
                Term = 1,
                CandidateId = "127.0.0.3:9003",
                LastLogIndex = 0,
                LastLogTerm = 0,
            };
            var response2 = raft.HandleRequestVote(request2);
            Assert.False(response2.VoteGranted);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleRequestVote：低任期请求被拒绝")]
    public void HandleRequestVote_LowerTerm_Rejected()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var requestHigh = new RaftVoteRequest
            {
                Term = 5,
                CandidateId = "127.0.0.2:9002",
                LastLogIndex = 0,
                LastLogTerm = 0,
            };
            raft.HandleRequestVote(requestHigh);
            Assert.Equal(5, raft.CurrentTerm);

            var requestLow = new RaftVoteRequest
            {
                Term = 3,
                CandidateId = "127.0.0.3:9003",
                LastLogIndex = 0,
                LastLogTerm = 0,
            };
            var response = raft.HandleRequestVote(requestLow);
            Assert.False(response.VoteGranted);
            Assert.Equal(5, raft.CurrentTerm);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleRequestVote：候选人 Log 落后时拒绝投票")]
    public void HandleRequestVote_StaleCandidate_Rejected()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var entries = new[]
            {
                new RaftLogEntry { Term = 1, Index = 0, Type = RaftLogEntryType.Subscribe, TopicFilter = "test/topic" },
            };
            var appendRequest = new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = entries,
                LeaderCommit = -1,
            };
            raft.HandleAppendEntries(appendRequest);

            var request = new RaftVoteRequest
            {
                Term = 2,
                CandidateId = "127.0.0.3:9003",
                LastLogIndex = -1,
                LastLogTerm = 0,
            };
            var response = raft.HandleRequestVote(request);
            Assert.False(response.VoteGranted);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleRequestVote：候选人 Log 更新时同意投票")]
    public void HandleRequestVote_UpToDateCandidate_Granted()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var request = new RaftVoteRequest
            {
                Term = 1,
                CandidateId = "127.0.0.2:9002",
                LastLogIndex = 0,
                LastLogTerm = 0,
            };
            var response = raft.HandleRequestVote(request);

            Assert.True(response.VoteGranted);
            Assert.Equal("127.0.0.2:9002", raft.VotedFor);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }
    #endregion

    #region Log 复制
    [Fact(DisplayName = "HandleAppendEntries：空条目重置选举超时（心跳）")]
    public void HandleAppendEntries_Heartbeat_ResetsElectionTimer()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var request = new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = [],
                LeaderCommit = -1,
            };
            var response = raft.HandleAppendEntries(request);

            Assert.True(response.Success);
            Assert.Equal("127.0.0.2:9002", raft.LeaderId);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleAppendEntries：追加新 Log 条目")]
    public void HandleAppendEntries_AppendNewLogs()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var entries = new[]
            {
                new RaftLogEntry { Term = 1, Index = 0, Type = RaftLogEntryType.Subscribe, TopicFilter = "sensor/temp" },
                new RaftLogEntry { Term = 1, Index = 1, Type = RaftLogEntryType.Subscribe, TopicFilter = "sensor/humidity" },
            };
            var request = new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = entries,
                LeaderCommit = -1,
            };
            var response = raft.HandleAppendEntries(request);

            Assert.True(response.Success);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleAppendEntries：PrevLog 不匹配时拒绝")]
    public void HandleAppendEntries_PrevLogMismatch_Rejected()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var entries1 = new[]
            {
                new RaftLogEntry { Term = 1, Index = 0, Type = RaftLogEntryType.Subscribe, TopicFilter = "test/a" },
            };
            raft.HandleAppendEntries(new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = entries1,
                LeaderCommit = -1,
            });

            var entries2 = new[]
            {
                new RaftLogEntry { Term = 2, Index = 5, Type = RaftLogEntryType.Subscribe, TopicFilter = "test/b" },
            };
            var response = raft.HandleAppendEntries(new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = 5,
                PrevLogTerm = 1,
                Entries = entries2,
                LeaderCommit = -1,
            });

            Assert.False(response.Success);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }

    [Fact(DisplayName = "HandleAppendEntries：高任期请求触发退位")]
    public void HandleAppendEntries_HigherTerm_StepDown()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();
            var oldTerm = raft.CurrentTerm;

            var request = new RaftAppendEntriesRequest
            {
                Term = oldTerm + 10,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = [],
                LeaderCommit = -1,
            };
            var response = raft.HandleAppendEntries(request);

            Assert.True(response.Success);
            Assert.Equal(RaftState.Follower, raft.State);
            Assert.Equal(oldTerm + 10, raft.CurrentTerm);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }
    #endregion

    #region 日志冲突处理
    [Fact(DisplayName = "HandleAppendEntries：任期冲突时删除冲突条目")]
    public void HandleAppendEntries_TermConflict_Overwrites()
    {
        using var raft = CreateRaft(3);
        try
        {
            raft.Start();

            var entries1 = new[]
            {
                new RaftLogEntry { Term = 1, Index = 0, Type = RaftLogEntryType.Subscribe, TopicFilter = "old/topic" },
            };
            raft.HandleAppendEntries(new RaftAppendEntriesRequest
            {
                Term = 1,
                LeaderId = "127.0.0.2:9002",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = entries1,
                LeaderCommit = -1,
            });

            var entries2 = new[]
            {
                new RaftLogEntry { Term = 2, Index = 0, Type = RaftLogEntryType.Subscribe, TopicFilter = "new/topic" },
            };
            var response = raft.HandleAppendEntries(new RaftAppendEntriesRequest
            {
                Term = 2,
                LeaderId = "127.0.0.3:9003",
                PrevLogIndex = -1,
                PrevLogTerm = 0,
                Entries = entries2,
                LeaderCommit = -1,
            });

            Assert.True(response.Success);
        }
        finally
        {
            DestroyRaft(raft);
        }
    }
    #endregion

    #region 提交与快照
    [Fact(DisplayName = "Raft 默认属性正确")]
    public void DefaultProperties()
    {
        using var raft = new RaftCluster();
        Assert.Equal(150, raft.ElectionTimeoutMin);
        Assert.Equal(300, raft.ElectionTimeoutMax);
        Assert.Equal(100, raft.HeartbeatInterval);
        Assert.Equal(10000, raft.SnapshotInterval);
        Assert.Equal(RaftState.Follower, raft.State);
    }
    #endregion
}
