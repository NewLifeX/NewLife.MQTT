using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>Raft共识引擎。管理订阅元数据的Leader选举、Log复制和Snapshot</summary>
/// <remarks>
/// 精简版Raft实现：仅Leader Election + Log Replication + Snapshot。
/// 不包含Membership Change（动态增删节点）。适用于3~7节点的小规模集群。
/// Log条目仅存储订阅变更（Subscribe/Unsubscribe），不包含消息本身。
/// 不足3节点时自动降级为RPC广播模式。
/// </remarks>
public class RaftCluster : DisposeBase
{
    #region 属性
    /// <summary>集群服务端</summary>
    public ClusterServer Cluster { get; set; } = null!;

    /// <summary>集群交换机。Raft Log提交后更新其_topics字典</summary>
    public ClusterExchange? ClusterExchange { get; set; }

    /// <summary>当前节点ID（格式：ip:port）</summary>
    public String NodeId { get; private set; } = null!;

    /// <summary>Raft节点状态</summary>
    public RaftState State { get; private set; } = RaftState.Follower;

    /// <summary>当前任期号</summary>
    public Int32 CurrentTerm { get; private set; }

    /// <summary>当前任期投票给了谁</summary>
    public String? VotedFor { get; private set; }

    /// <summary>Leader节点ID</summary>
    public String? LeaderId { get; private set; }

    /// <summary>Log条目列表</summary>
    private List<RaftLogEntry> _log = [];

    /// <summary>已提交的最大Log索引</summary>
    private Int32 _commitIndex;
    private Int32 _lastApplied;

    /// <summary>每个Follower的匹配状态（Leader维护）</summary>
    private ConcurrentDictionary<String, Int32> _nextIndex = new();
    private ConcurrentDictionary<String, Int32> _matchIndex = new();

    /// <summary>选举超时随机范围（毫秒）</summary>
    public Int32 ElectionTimeoutMin { get; set; } = 150;

    /// <summary>选举超时随机范围（毫秒）</summary>
    public Int32 ElectionTimeoutMax { get; set; } = 300;

    /// <summary>心跳间隔（毫秒）。Leader定时向Follower发送AppendEntries心跳</summary>
    public Int32 HeartbeatInterval { get; set; } = 100;

    private TimerX? _electionTimer;
    private TimerX? _heartbeatTimer;
    private readonly Object _lock = new();
    private Int32 _electionTimeout;
    /// <summary>快照最大Log索引（超过此值的Log可被压缩）</summary>
    public Int32 SnapshotInterval { get; set; } = 10000;

    /// <summary>上次快照的Log索引</summary>
    private Int32 _lastSnapshotIndex;
    private RaftSnapshot? _lastSnapshot;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public RaftCluster()
    {
        // 随机选举超时
        _electionTimeout = new Random().Next(ElectionTimeoutMin, ElectionTimeoutMax);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop();
    }
    #endregion

    #region 启动停止
    /// <summary>启动Raft引擎</summary>
    public void Start()
    {
        NodeId = Cluster.GetNodeInfo().EndPoint;

        var nodeCount = Cluster.Nodes.Count + 1; // 含自己

        if (nodeCount < 3)
        {
            Cluster.WriteLog("Raft: 节点数 {0} < 3，降级为RPC广播模式", nodeCount);
            return;
        }

        // 启动为Follower，等待选举超时
        BecomeFollower(CurrentTerm);

        Cluster.WriteLog("Raft: 启动完成 NodeId={0} State={1} Term={2}", NodeId, State, CurrentTerm);
    }

    /// <summary>停止Raft引擎</summary>
    public void Stop()
    {
        _electionTimer.TryDispose();
        _electionTimer = null;

        _heartbeatTimer.TryDispose();
        _heartbeatTimer = null;
    }
    #endregion

    #region 状态切换
    private void BecomeFollower(Int32 term)
    {
        lock (_lock)
        {
            State = RaftState.Follower;
            CurrentTerm = term;
            VotedFor = null;
        }

        _heartbeatTimer.TryDispose();
        _heartbeatTimer = null;

        ResetElectionTimer();
    }

    private void BecomeCandidate()
    {
        lock (_lock)
        {
            State = RaftState.Candidate;
            CurrentTerm++;
            VotedFor = NodeId;
            LeaderId = null;
        }

        Cluster.WriteLog("Raft: 成为 Candidate Term={0}", CurrentTerm);

        // 请求投票
        var votes = 1; // 自己的一票
        var lastLogIndex = _log.Count - 1;
        var lastLogTerm = _log.Count > 0 ? _log[^1].Term : 0;

        var tasks = new List<Task>();
        foreach (var item in Cluster.Nodes)
        {
            var node = item.Value;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await RequestVoteAsync(node, CurrentTerm, NodeId, lastLogIndex, lastLogTerm).ConfigureAwait(false);
                    if (result)
                    {
                        Interlocked.Increment(ref votes);
                    }
                }
                catch
                {
                    // 投票请求失败，忽略
                }
            }));
        }

        // 等待投票结果
        Task.WhenAll(tasks).ContinueWith(_ =>
        {
            var majority = (Cluster.Nodes.Count + 1) / 2 + 1;
            if (votes >= majority)
            {
                BecomeLeader();
            }
            else
            {
                // 选举失败，退回Follower
                BecomeFollower(CurrentTerm);
            }
        });
    }

    private void BecomeLeader()
    {
        lock (_lock)
        {
            State = RaftState.Leader;
            LeaderId = NodeId;
        }

        Cluster.WriteLog("Raft: 成为 Leader Term={0}", CurrentTerm);

        // 初始化Follower的nextIndex和matchIndex
        var lastLogIndex = _log.Count;
        foreach (var item in Cluster.Nodes)
        {
            _nextIndex[item.Key] = lastLogIndex;
            _matchIndex[item.Key] = 0;
        }

        // 启动心跳定时器
        _electionTimer.TryDispose();
        _electionTimer = null;

        _heartbeatTimer = new TimerX(SendHeartbeat, null, 50, HeartbeatInterval) { Async = true };

        // 成为Leader后立即发送一次心跳
        SendHeartbeat(null);
    }
    #endregion

    #region 选举
    private void ResetElectionTimer()
    {
        _electionTimer.TryDispose();
        _electionTimeout = new Random().Next(ElectionTimeoutMin, ElectionTimeoutMax);
        _electionTimer = new TimerX(s => BecomeCandidate(), null, _electionTimeout, 0) { Async = true };
    }

    private async Task<Boolean> RequestVoteAsync(ClusterNode node, Int32 term, String candidateId, Int32 lastLogIndex, Int32 lastLogTerm)
    {
        var request = new RaftVoteRequest
        {
            Term = term,
            CandidateId = candidateId,
            LastLogIndex = lastLogIndex,
            LastLogTerm = lastLogTerm,
        };

        var response = await node.Client.InvokeAsync<RaftVoteResponse>("Cluster/RequestVote", request).ConfigureAwait(false);

        if (response.Term > CurrentTerm)
        {
            BecomeFollower(response.Term);
            return false;
        }

        return response.VoteGranted;
    }

    /// <summary>接收投票请求（由ClusterController调用）</summary>
    public RaftVoteResponse HandleRequestVote(RaftVoteRequest request)
    {
        lock (_lock)
        {
            if (request.Term > CurrentTerm)
            {
                BecomeFollower(request.Term);
            }

            if (request.Term < CurrentTerm) return new RaftVoteResponse { Term = CurrentTerm, VoteGranted = false };

            // 检查是否已投票给其他节点
            if (VotedFor != null && VotedFor != request.CandidateId)
                return new RaftVoteResponse { Term = CurrentTerm, VoteGranted = false };

            // 检查候选人的Log是否至少和自己一样新
            var myLastLogIndex = _log.Count - 1;
            var myLastLogTerm = _log.Count > 0 ? _log[^1].Term : 0;

            if (request.LastLogTerm < myLastLogTerm ||
                (request.LastLogTerm == myLastLogTerm && request.LastLogIndex < myLastLogIndex))
                return new RaftVoteResponse { Term = CurrentTerm, VoteGranted = false };

            VotedFor = request.CandidateId;
            ResetElectionTimer();

            Cluster.WriteLog("Raft: 投票给 {0} Term={1}", request.CandidateId, request.Term);

            return new RaftVoteResponse { Term = CurrentTerm, VoteGranted = true };
        }
    }
    #endregion

    #region Log复制
    /// <summary>提交订阅变更到Raft Log</summary>
    /// <param name="entry">Log条目</param>
    public void Submit(RaftLogEntry entry)
    {
        if (State != RaftState.Leader)
        {
            // 非Leader，转发给Leader
            if (LeaderId != null && Cluster.Nodes.TryGetValue(LeaderId, out var leader))
            {
                _ = leader.Client.InvokeAsync<String>("Cluster/SubmitRaftLog", entry);
            }
            return;
        }

        lock (_lock)
        {
            entry.Term = CurrentTerm;
            entry.Index = _log.Count;
            _log.Add(entry);
        }

        // 并行复制到所有Follower
        foreach (var item in Cluster.Nodes)
        {
            var nodeId = item.Key;
            var node = item.Value;
            _ = AppendEntriesAsync(node, nodeId);
        }
    }

    private async Task AppendEntriesAsync(ClusterNode node, String nodeId)
    {
        try
        {
            var prevLogIndex = _nextIndex.GetOrAdd(nodeId, _log.Count) - 1;
            var prevLogTerm = prevLogIndex >= 0 && prevLogIndex < _log.Count ? _log[prevLogIndex].Term : 0;

            var entries = new List<RaftLogEntry>();
            for (var i = _nextIndex[nodeId]; i < _log.Count; i++)
            {
                entries.Add(_log[i]);
            }

            var request = new RaftAppendEntriesRequest
            {
                Term = CurrentTerm,
                LeaderId = NodeId,
                PrevLogIndex = prevLogIndex,
                PrevLogTerm = prevLogTerm,
                Entries = entries.ToArray(),
                LeaderCommit = _commitIndex,
            };

            var response = await node.Client.InvokeAsync<RaftAppendEntriesResponse>("Cluster/AppendEntries", request).ConfigureAwait(false);

            if (response.Term > CurrentTerm)
            {
                BecomeFollower(response.Term);
                return;
            }

            if (response.Success)
            {
                _nextIndex[nodeId] = prevLogIndex + 1 + entries.Count;
                _matchIndex[nodeId] = prevLogIndex + entries.Count;

                // 检查是否可以提交更多Log
                TryCommit();
            }
            else
            {
                // 日志不一致，后退nextIndex
                _nextIndex[nodeId] = Math.Max(0, _nextIndex[nodeId] - 1);
            }
        }
        catch
        {
            // 复制失败，下次心跳重试
        }
    }

    /// <summary>接收AppendEntries请求（由ClusterController调用）</summary>
    public RaftAppendEntriesResponse HandleAppendEntries(RaftAppendEntriesRequest request)
    {
        lock (_lock)
        {
            if (request.Term > CurrentTerm)
            {
                BecomeFollower(request.Term);
            }

            if (request.Term < CurrentTerm)
                return new RaftAppendEntriesResponse { Term = CurrentTerm, Success = false };

            // 收到有效的心跳/复制请求，重置选举超时
            LeaderId = request.LeaderId;
            ResetElectionTimer();

            // 检查prevLog是否匹配
            if (request.PrevLogIndex >= 0)
            {
                if (request.PrevLogIndex >= _log.Count ||
                    (request.PrevLogIndex >= 0 && _log[request.PrevLogIndex].Term != request.PrevLogTerm))
                {
                    return new RaftAppendEntriesResponse { Term = CurrentTerm, Success = false };
                }
            }

            // 追加新条目
            var entryIndex = request.PrevLogIndex + 1;
            foreach (var entry in request.Entries)
            {
                if (entryIndex < _log.Count)
                {
                    // 冲突：删除冲突条目及之后的所有条目
                    if (_log[entryIndex].Term != entry.Term)
                    {
                        _log.RemoveRange(entryIndex, _log.Count - entryIndex);
                        _log.Add(entry);
                    }
                }
                else
                {
                    _log.Add(entry);
                }
                entryIndex++;
            }

            // 更新commitIndex
            if (request.LeaderCommit > _commitIndex)
            {
                _commitIndex = Math.Min(request.LeaderCommit, _log.Count - 1);
                ApplyCommittedEntries();
            }

            return new RaftAppendEntriesResponse { Term = CurrentTerm, Success = true };
        }
    }

    private void TryCommit()
    {
        // 查找大多数节点已复制的最大Log Index
        for (var i = _commitIndex + 1; i < _log.Count; i++)
        {
            if (_log[i].Term != CurrentTerm) continue; // 只能提交当前Term的日志

            var replicationCount = 1; // Leader自己
            foreach (var item in Cluster.Nodes)
            {
                if (_matchIndex.TryGetValue(item.Key, out var match) && match >= i)
                {
                    replicationCount++;
                }
            }

            var majority = (Cluster.Nodes.Count + 1) / 2 + 1;
            if (replicationCount >= majority)
            {
                _commitIndex = i;
            }
        }

        ApplyCommittedEntries();
    }

    private void ApplyCommittedEntries()
    {
        while (_lastApplied < _commitIndex)
        {
            _lastApplied++;
            var entry = _log[_lastApplied];

            // 应用到状态机（更新ClusterExchange的_topics字典）
            ApplyEntry(entry);
        }

        // 定期创建快照
        if (_commitIndex - _lastSnapshotIndex >= SnapshotInterval && State == RaftState.Leader)
        {
            CreateSnapshot();
        }
    }

    private void ApplyEntry(RaftLogEntry entry)
    {
        var exchange = ClusterExchange;
        if (exchange == null) return;

        if (entry.Type == RaftLogEntryType.Subscribe)
        {
            var info = new SubscriptionInfo
            {
                Topic = entry.TopicFilter ?? String.Empty,
                Endpoint = entry.NodeEndpoint ?? String.Empty,
                ClientId = entry.ClientId,
                SessionId = entry.SessionId,
            };
            exchange.AddSubscription(info);
        }
        else if (entry.Type == RaftLogEntryType.Unsubscribe)
        {
            var info = new SubscriptionInfo
            {
                Topic = entry.TopicFilter ?? String.Empty,
                Endpoint = entry.NodeEndpoint ?? String.Empty,
                ClientId = entry.ClientId,
                SessionId = entry.SessionId,
            };
            exchange.RemoveSubscription(info);
        }
    }

    /// <summary>创建订阅快照</summary>
    public void CreateSnapshot()
    {
        var exchange = ClusterExchange;
        if (exchange == null) return;

        var topics = exchange.GetAllTopics();
        var snapshot = new RaftSnapshot
        {
            LastIncludedIndex = _commitIndex,
            LastIncludedTerm = _log[_commitIndex].Term,
            Topics = topics.ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => new SnapshotSubscription
            {
                Topic = s.Topic,
                Endpoint = s.Endpoint,
                ClientId = s.ClientId,
                SessionId = s.SessionId,
            }).ToArray()),
        };

        _lastSnapshot = snapshot;
        _lastSnapshotIndex = _commitIndex;

        // 清理旧Log（保留Snapshot之后的Log）
        var keepFrom = _commitIndex + 1;
        if (keepFrom < _log.Count)
        {
            _log.RemoveRange(0, keepFrom);
            _commitIndex = 0;
            _lastApplied = 0;
            _lastSnapshotIndex = 0;
        }
        else
        {
            _log.Clear();
            _commitIndex = 0;
            _lastApplied = 0;
            _lastSnapshotIndex = 0;
        }

        WriteLog("快照创建完成 Index={0} Topics={1}", snapshot.LastIncludedIndex, snapshot.Topics.Count);
    }

    /// <summary>安装快照（落后节点从Leader拉取）</summary>
    public void InstallSnapshot(RaftSnapshot snapshot)
    {
        lock (_lock)
        {
            _log.Clear();
            _commitIndex = snapshot.LastIncludedIndex;
            _lastApplied = snapshot.LastIncludedIndex;
            _lastSnapshot = snapshot;
            _lastSnapshotIndex = snapshot.LastIncludedIndex;
        }

        // 应用快照中的订阅关系
        var exchange = ClusterExchange;
        if (exchange != null)
        {
            exchange.ReplaceAllTopics(snapshot.Topics.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(s => new SubscriptionInfo
                {
                    Topic = s.Topic,
                    Endpoint = s.Endpoint,
                    ClientId = s.ClientId,
                    SessionId = s.SessionId,
                }).ToList()
            ));
        }

        WriteLog("快照安装完成 Index={0} Topics={1}", snapshot.LastIncludedIndex, snapshot.Topics.Count);
    }

    /// <summary>处理RPC InstallSnapshot请求</summary>
    public RaftAppendEntriesResponse HandleInstallSnapshot(RaftSnapshot snapshot)
    {
        InstallSnapshot(snapshot);
        return new RaftAppendEntriesResponse { Term = CurrentTerm, Success = true };
    }
    #endregion

    #region 心跳
    private void SendHeartbeat(Object? state)
    {
        foreach (var item in Cluster.Nodes)
        {
            _ = AppendEntriesAsync(item.Value, item.Key);
        }
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>写日志</summary>
    private void WriteLog(String format, params Object?[] args) => Cluster.WriteLog($"[Raft] {format}", args);
    #endregion
}

#region 数据模型
/// <summary>Raft节点状态</summary>
public enum RaftState
{
    /// <summary>跟随者</summary>
    Follower,

    /// <summary>候选者</summary>
    Candidate,

    /// <summary>领导者</summary>
    Leader,
}

/// <summary>Raft日志条目类型</summary>
public enum RaftLogEntryType
{
    /// <summary>订阅</summary>
    Subscribe,

    /// <summary>取消订阅</summary>
    Unsubscribe,
}

/// <summary>Raft日志条目</summary>
public class RaftLogEntry
{
    /// <summary>Log索引</summary>
    public Int32 Index { get; set; }

    /// <summary>任期号</summary>
    public Int32 Term { get; set; }

    /// <summary>条目类型</summary>
    public RaftLogEntryType Type { get; set; }

    /// <summary>主题过滤器</summary>
    public String? TopicFilter { get; set; }

    /// <summary>节点地址</summary>
    public String? NodeEndpoint { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>会话标识</summary>
    public Int32 SessionId { get; set; }
}

/// <summary>投票请求</summary>
public class RaftVoteRequest
{
    /// <summary>任期号</summary>
    public Int32 Term { get; set; }

    /// <summary>候选人ID</summary>
    public String CandidateId { get; set; } = null!;

    /// <summary>最后Log索引</summary>
    public Int32 LastLogIndex { get; set; }

    /// <summary>最后Log任期</summary>
    public Int32 LastLogTerm { get; set; }
}

/// <summary>投票响应</summary>
public class RaftVoteResponse
{
    /// <summary>当前任期号</summary>
    public Int32 Term { get; set; }

    /// <summary>是否同意投票</summary>
    public Boolean VoteGranted { get; set; }
}

/// <summary>AppendEntries请求</summary>
public class RaftAppendEntriesRequest
{
    /// <summary>任期号</summary>
    public Int32 Term { get; set; }

    /// <summary>Leader ID</summary>
    public String LeaderId { get; set; } = null!;

    /// <summary>前一条Log索引</summary>
    public Int32 PrevLogIndex { get; set; }

    /// <summary>前一条Log任期</summary>
    public Int32 PrevLogTerm { get; set; }

    /// <summary>Log条目列表</summary>
    public RaftLogEntry[] Entries { get; set; } = [];

    /// <summary>Leader已提交的Log索引</summary>
    public Int32 LeaderCommit { get; set; }
}

/// <summary>AppendEntries响应</summary>
public class RaftAppendEntriesResponse
{
    /// <summary>当前任期号</summary>
    public Int32 Term { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }
}

/// <summary>Raft快照</summary>
public class RaftSnapshot
{
    /// <summary>快照包含的最后Log索引</summary>
    public Int32 LastIncludedIndex { get; set; }

    /// <summary>快照包含的最后Log任期</summary>
    public Int32 LastIncludedTerm { get; set; }

    /// <summary>所有主题订阅关系</summary>
    public Dictionary<String, SnapshotSubscription[]> Topics { get; set; } = [];
}

/// <summary>快照中的订阅条目</summary>
public class SnapshotSubscription
{
    /// <summary>主题</summary>
    public String Topic { get; set; } = null!;

    /// <summary>节点地址</summary>
    public String Endpoint { get; set; } = null!;

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>会话标识</summary>
    public Int32 SessionId { get; set; }
}
#endregion
