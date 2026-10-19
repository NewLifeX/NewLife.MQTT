using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>批量合并转发管道。将发往同一节点的多条小消息合并为单个批量RPC请求</summary>
/// <remarks>
/// 收集发往同一目标节点的消息，按时间窗口（默认5ms）或大小阈值（默认64KB）触发批量发送。
/// 大消息（>64KB）不等待直接发送，避免增加延迟。
/// </remarks>
public class BatchForwardPipe
{
    #region 属性
    /// <summary>集群服务引用</summary>
    public ClusterServer? Cluster { get; set; }

    /// <summary>最大合并延迟（毫秒）。默认5ms</summary>
    public Int32 MaxDelayMs { get; set; } = 5;

    /// <summary>最大合并大小（字节）。默认64KB</summary>
    public Int32 MaxBatchSize { get; set; } = 64 * 1024;

    /// <summary>大消息阈值（字节）。超过此大小直接发送不等待合并。默认64KB</summary>
    public Int32 LargeMessageThreshold { get; set; } = 64 * 1024;

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; } = true;

    private readonly ConcurrentDictionary<String, BatchBuffer> _buffers = new();
    private TimerX? _flushTimer;
    #endregion

    #region 构造
    /// <summary>启动批量转发管道</summary>
    public void Start()
    {
        if (_flushTimer != null) return;

        _flushTimer = new TimerX(DoFlush, null, MaxDelayMs, MaxDelayMs)
        {
            Async = true
        };

        WriteLog("BatchForwardPipe started; merge window {0} ms / {1} KB", MaxDelayMs, MaxBatchSize / 1024);
    }

    /// <summary>停止批量转发管道</summary>
    public void Stop()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;

        // 刷新所有剩余缓冲
        FlushAll();

        _buffers.Clear();
        WriteLog("BatchForwardPipe stopped");
    }
    #endregion

    #region 方法
    /// <summary>发布消息到批量管道。小消息合并，大消息直接发送</summary>
    /// <param name="node">目标节点</param>
    /// <param name="info">发布信息</param>
    /// <returns>true=已入队批量管道，false=已直接发送（大消息）</returns>
    public Boolean Enqueue(ClusterNode node, PublishInfo info)
    {
        if (!Enable || info.Message?.Payload == null) return false;

        var messageSize = info.Message.Payload.Length;
        var endpoint = node.EndPoint;

        // 大消息直接发送，不等待合并
        if (messageSize > LargeMessageThreshold)
        {
            _ = node.Publish(info);
            return false;
        }

        var buffer = _buffers.GetOrAdd(endpoint, _ => new BatchBuffer(node, MaxBatchSize));
        var flushNow = false;
        List<PublishInfo>? pending = null;

        lock (buffer)
        {
            buffer.Messages.Add(info);
            buffer.TotalSize += messageSize;

            if (buffer.TotalSize >= MaxBatchSize)
            {
                pending = buffer.Messages.ToList();
                buffer.Messages.Clear();
                buffer.TotalSize = 0;
                flushNow = true;
            }
        }

        if (flushNow && pending != null)
        {
            FlushNode(endpoint, node, pending);
        }

        return !flushNow;
    }

    private void DoFlush(Object? state)
    {
        FlushAll();
    }

    private void FlushAll()
    {
        foreach (var kv in _buffers)
        {
            var endpoint = kv.Key;
            var buffer = kv.Value;

            List<PublishInfo>? pending = null;

            lock (buffer)
            {
                if (buffer.Messages.Count == 0) continue;

                pending = buffer.Messages.ToList();
                buffer.Messages.Clear();
                buffer.TotalSize = 0;
            }

            if (pending != null && pending.Count > 0)
            {
                FlushNode(endpoint, buffer.Node, pending);
            }
        }
    }

    private void FlushNode(String endpoint, ClusterNode node, List<PublishInfo> infos)
    {
        if (infos.Count == 1)
        {
            // 单条消息直接发送
            _ = node.Publish(infos[0]);
        }
        else
        {
            // 多条消息合并批量发送
            var batchInfo = new PublishInfo
            {
                Messages = infos.Select(i => i.Message).ToList(),
                RemoteEndpoint = infos[0].RemoteEndpoint,
                TTL = infos[0].TTL,
                NodePath = infos[0].NodePath,
            };

            _ = node.PublishBatch(batchInfo);
        }
    }
    #endregion

    #region 内部类
    private class BatchBuffer
    {
        public ClusterNode Node { get; }
        public List<PublishInfo> Messages { get; } = [];
        public Int32 TotalSize { get; set; }

        public BatchBuffer(ClusterNode node, Int32 maxBatchSize)
        {
            Node = node;
        }
    }
    #endregion

    #region 日志
    private ILog Log => XTrace.Log;

    /// <summary>写日志</summary>
    private void WriteLog(String format, params Object?[] args) => Log.InfoPrefixed("[BatchPipe] ", format, args);
    #endregion
}
