using System.Collections.Concurrent;
using System.Net;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;
using NewLife.Threading;

namespace NewLife.MQTT.Quic;

/// <summary>基于 UDP 的 MQTT 可靠传输层</summary>
/// <remarks>
/// 借鉴 QUIC 核心思想（低延迟、无队头阻塞），基于 NewLife.Core 的 UDP 和二进制读写能力，
/// 实现 MQTT 专用的轻量级可靠 UDP 传输，不依赖操作系统 QUIC/msquic 支持。
/// <para>
/// 帧格式（4 字节头）：
/// - [0]    FrameType（1 字节）：0x01=Data, 0x02=Ack, 0x03=Connect, 0x04=ConnAck, 0x05=Close
/// - [1-2]  SequenceId（2 字节）：序列号（大端序）
/// - [3]    Flags（1 字节）：保留标志位
/// - [4..]  Payload：MQTT 报文数据
/// </para>
/// <para>
/// 可靠性机制：
/// - 每个数据帧携带 SequenceId，接收方回复 Ack 帧
/// - 发送方维护待确认队列，超时自动重发
/// - 接收方通过 SequenceId 去重
/// </para>
/// </remarks>
public class MqttUdpClient : DisposeBase
{
    #region 常量
    const Byte FrameData = 0x01;
    const Byte FrameAck = 0x02;
    const Byte FrameConnect = 0x03;
    const Byte FrameConnAck = 0x04;
    const Byte FrameClose = 0x05;
    const Int32 FrameHeaderSize = 4;
    #endregion

    #region 属性
    /// <summary>服务端地址</summary>
    public String? Server { get; set; }

    /// <summary>服务端端口。默认14567</summary>
    public Int32 Port { get; set; } = 14567;

    /// <summary>超时时间（毫秒）。默认15000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>重发超时时间（毫秒）。默认3000ms</summary>
    public Int32 RetryTimeout { get; set; } = 3_000;

    /// <summary>最大重发次数。默认5次</summary>
    public Int32 MaxRetries { get; set; } = 5;

    /// <summary>是否已连接</summary>
    public Boolean IsConnected { get; private set; }

    /// <summary>接收到消息时触发</summary>
    public event EventHandler<MqttMessage>? MessageReceived;

    private ISocketClient? _udp;
    private Int32 _nextSeqId;
    private readonly ConcurrentDictionary<UInt16, PendingFrame> _pendingAcks = new();
    private readonly ConcurrentDictionary<UInt16, DateTime> _receivedSeqs = new();
    private TimerX? _retryTimer;
    private TimerX? _cleanupTimer;
    #endregion

    #region 方法
    /// <summary>检查是否支持。基于 NewLife.Core UDP，始终支持</summary>
    public static Boolean IsSupported => true;

    /// <summary>连接到 UDP 服务端</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));

        WriteLog("正在通过 UDP 可靠传输连接 {0}:{1}", Server, Port);

        var uri = new NetUri(NetType.Udp, Server, Port);
        var client = uri.CreateRemote();
        client.Timeout = Timeout;
        client.Received += OnUdpReceived;
        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

        _udp = client;

        // 发送连接帧
        var connectFrame = BuildFrame(FrameConnect, 0, null);
        client.Send(connectFrame);

        // 等待 ConnAck
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        var tcs = new TaskCompletionSource<Boolean>();
        _connAckTcs = tcs;

        try
        {
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _connAckTcs = null;
            cts.Dispose();
        }

        IsConnected = true;

        // 启动重发定时器
        _retryTimer ??= new TimerX(CheckRetry, null, 1000, 1000);
        // 启动去重清理定时器（清理30秒前的序列号记录）
        _cleanupTimer ??= new TimerX(CleanupReceivedSeqs, null, 30_000, 30_000);

        WriteLog("UDP 可靠传输连接已建立");
    }

    private TaskCompletionSource<Boolean>? _connAckTcs;

    /// <summary>发送 MQTT 消息（可靠传输）</summary>
    /// <param name="message">MQTT 消息</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendAsync(MqttMessage message, CancellationToken cancellationToken = default)
    {
        if (_udp == null || !IsConnected)
            throw new InvalidOperationException("未建立 UDP 连接");

        var data = message.ToArray();
        var seqId = (UInt16)Interlocked.Increment(ref _nextSeqId);
        var frame = BuildFrame(FrameData, seqId, data);

        // 加入待确认队列
        _pendingAcks[seqId] = new PendingFrame
        {
            SeqId = seqId,
            Data = frame,
            SendTime = DateTime.Now,
        };

        _udp.Send(frame);

        return Task.FromResult(0);
    }

    /// <summary>关闭连接</summary>
    /// <returns></returns>
    public Task CloseAsync()
    {
        if (_udp != null && IsConnected)
        {
            var closeFrame = BuildFrame(FrameClose, 0, null);
            _udp.Send(closeFrame);
        }

        IsConnected = false;
        return Task.FromResult(0);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        IsConnected = false;
        _retryTimer.TryDispose();
        _cleanupTimer.TryDispose();
        _udp.TryDispose();
        _pendingAcks.Clear();
    }
    #endregion

    #region 帧处理
    /// <summary>构建传输帧</summary>
    private static Byte[] BuildFrame(Byte frameType, UInt16 seqId, Byte[]? payload)
    {
        var len = FrameHeaderSize + (payload?.Length ?? 0);
        var buf = new Byte[len];
        buf[0] = frameType;
        buf[1] = (Byte)(seqId >> 8);
        buf[2] = (Byte)(seqId & 0xFF);
        buf[3] = 0; // flags

        if (payload != null && payload.Length > 0)
            Buffer.BlockCopy(payload, 0, buf, FrameHeaderSize, payload.Length);

        return buf;
    }

    /// <summary>解析帧头</summary>
    private static Boolean ParseFrameHeader(IPacket pk, out Byte frameType, out UInt16 seqId, out ArraySegment<Byte> payload)
    {
        frameType = 0;
        seqId = 0;
        payload = default;

        var data = pk.GetSpan();
        if (data.Length < FrameHeaderSize) return false;

        frameType = data[0];
        seqId = (UInt16)((data[1] << 8) | data[2]);

        if (data.Length > FrameHeaderSize)
        {
            var arr = pk.ToArray();
            payload = new ArraySegment<Byte>(arr, FrameHeaderSize, arr.Length - FrameHeaderSize);
        }

        return true;
    }

    /// <summary>收到 UDP 数据</summary>
    private void OnUdpReceived(Object sender, ReceivedEventArgs e)
    {
        if (e.Packet == null) return;
        if (!ParseFrameHeader(e.Packet, out var frameType, out var seqId, out var payload)) return;

        switch (frameType)
        {
            case FrameConnAck:
                _connAckTcs?.TrySetResult(true);
                break;

            case FrameAck:
                // 收到确认，移除待确认队列
                _pendingAcks.TryRemove(seqId, out _);
                break;

            case FrameData:
                // 回复 Ack
                if (_udp != null)
                {
                    var ackFrame = BuildFrame(FrameAck, seqId, null);
                    _udp.Send(ackFrame);
                }

                // 去重
                if (!_receivedSeqs.TryAdd(seqId, DateTime.Now)) break;

                // 解析 MQTT 消息
                if (payload.Count > 0)
                {
                    try
                    {
                        var pk = new ArrayPacket(payload.Array!, payload.Offset, payload.Count);
                        var factory = new MqttFactory();
                        var msg = factory.ReadMessage(pk);
                        if (msg != null) MessageReceived?.Invoke(this, msg);
                    }
                    catch { }
                }
                break;

            case FrameClose:
                IsConnected = false;
                break;
        }
    }

    /// <summary>检查超时重发</summary>
    private void CheckRetry(Object? state)
    {
        var now = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(RetryTimeout);

        foreach (var item in _pendingAcks)
        {
            var pending = item.Value;
            if (now - pending.SendTime < timeout) continue;

            if (pending.RetryCount >= MaxRetries)
            {
                _pendingAcks.TryRemove(item.Key, out _);
                continue;
            }

            pending.RetryCount++;
            pending.SendTime = now;

            try
            {
                _udp?.Send(pending.Data);
            }
            catch { }
        }
    }

    /// <summary>清理过期的去重记录</summary>
    private void CleanupReceivedSeqs(Object? state)
    {
        var expiry = DateTime.Now.AddSeconds(-30);
        foreach (var item in _receivedSeqs)
        {
            if (item.Value < expiry)
                _receivedSeqs.TryRemove(item.Key, out _);
        }
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttUdp]{format}", args);
    #endregion
}

/// <summary>基于 UDP 的 MQTT 可靠传输监听器</summary>
/// <remarks>
/// 服务端组件，基于 NewLife.Core 的 UDP 网络能力接收 MQTT 客户端的可靠 UDP 连接。
/// 不依赖操作系统 QUIC 支持，跨平台可用（net45+）。
/// </remarks>
public class MqttUdpListener : DisposeBase
{
    #region 常量
    const Byte FrameData = 0x01;
    const Byte FrameAck = 0x02;
    const Byte FrameConnect = 0x03;
    const Byte FrameConnAck = 0x04;
    const Byte FrameClose = 0x05;
    const Int32 FrameHeaderSize = 4;
    #endregion

    #region 属性
    /// <summary>监听端口。默认14567</summary>
    public Int32 Port { get; set; } = 14567;

    /// <summary>是否正在监听</summary>
    public Boolean IsListening { get; private set; }

    /// <summary>接收到新连接时触发</summary>
    public event EventHandler<UdpMqttConnectionEventArgs>? ConnectionReceived;

    /// <summary>接收到消息时触发</summary>
    public event EventHandler<UdpMqttMessageEventArgs>? MessageReceived;

    private NetServer? _server;

    /// <summary>活跃连接。key=远程端点字符串</summary>
    private readonly ConcurrentDictionary<String, UdpMqttPeer> _peers = new();
    private TimerX? _cleanupTimer;
    #endregion

    #region 方法
    /// <summary>检查是否支持。基于 NewLife.Core UDP，始终支持</summary>
    public static Boolean IsSupported => true;

    /// <summary>启动监听</summary>
    public void Start()
    {
        WriteLog("启动 MQTT UDP 可靠传输监听，端口 {0}", Port);

        var server = new NetServer { Port = Port, ProtocolType = NetType.Udp, Log = Log };
        server.Received += OnServerReceived;
        server.Start();

        _server = server;
        IsListening = true;

        // 定时清理不活跃连接
        _cleanupTimer ??= new TimerX(CleanupPeers, null, 30_000, 30_000);

        WriteLog("MQTT UDP 可靠传输监听已启动");
    }

    /// <summary>停止监听</summary>
    public void Stop()
    {
        IsListening = false;

        _cleanupTimer.TryDispose();
        _cleanupTimer = null;

        _server.TryDispose();
        _server = null;

        _peers.Clear();

        WriteLog("MQTT UDP 可靠传输监听已停止");
    }

    /// <summary>收到 UDP 数据</summary>
    private void OnServerReceived(Object sender, ReceivedEventArgs e)
    {
        if (e.Packet == null) return;
        if (sender is not INetSession session) return;

        var remote = session.Remote;
        var remoteKey = remote?.ToString() ?? "";

        if (!ParseFrameHeader(e.Packet, out var frameType, out var seqId, out var payload)) return;

        switch (frameType)
        {
            case FrameConnect:
                // 新连接
                var peer = new UdpMqttPeer { RemoteKey = remoteKey, Session = session, LastActiveTime = DateTime.Now };
                _peers[remoteKey] = peer;

                // 回复 ConnAck
                var connAckFrame = BuildFrame(FrameConnAck, 0, null);
                session.Session?.Send(new ArrayPacket(connAckFrame));

                ConnectionReceived?.Invoke(this, new UdpMqttConnectionEventArgs(remoteKey, peer));
                break;

            case FrameData:
                if (!_peers.TryGetValue(remoteKey, out var dataPeer)) break;
                dataPeer.LastActiveTime = DateTime.Now;

                // 回复 Ack
                var ackFrame = BuildFrame(FrameAck, seqId, null);
                session.Session?.Send(new ArrayPacket(ackFrame));

                // 去重
                if (!dataPeer.ReceivedSeqs.TryAdd(seqId, DateTime.Now)) break;

                // 解析 MQTT 消息
                if (payload.Count > 0)
                {
                    try
                    {
                        var pk = new ArrayPacket(payload.Array!, payload.Offset, payload.Count);
                        var factory = new MqttFactory();
                        var msg = factory.ReadMessage(pk);
                        if (msg != null)
                            MessageReceived?.Invoke(this, new UdpMqttMessageEventArgs(remoteKey, msg));
                    }
                    catch { }
                }
                break;

            case FrameClose:
                _peers.TryRemove(remoteKey, out _);
                break;
        }
    }

    /// <summary>发送消息到指定客户端</summary>
    /// <param name="remoteKey">远程端点标识</param>
    /// <param name="message">MQTT 消息</param>
    public void Send(String remoteKey, MqttMessage message)
    {
        if (!_peers.TryGetValue(remoteKey, out var peer)) return;

        var data = message.ToArray();
        var seqId = peer.NextSeqId();
        var frame = BuildFrame(FrameData, seqId, data);

        peer.Session?.Session?.Send(new ArrayPacket(frame));
    }

    /// <summary>清理不活跃连接</summary>
    private void CleanupPeers(Object? state)
    {
        var expiry = DateTime.Now.AddMinutes(-5);
        foreach (var item in _peers)
        {
            if (item.Value.LastActiveTime < expiry)
                _peers.TryRemove(item.Key, out _);
        }

        // 清理每个 peer 的去重记录
        var seqExpiry = DateTime.Now.AddSeconds(-30);
        foreach (var peer in _peers.Values)
        {
            foreach (var seq in peer.ReceivedSeqs)
            {
                if (seq.Value < seqExpiry)
                    peer.ReceivedSeqs.TryRemove(seq.Key, out _);
            }
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建传输帧</summary>
    private static Byte[] BuildFrame(Byte frameType, UInt16 seqId, Byte[]? payload)
    {
        var len = FrameHeaderSize + (payload?.Length ?? 0);
        var buf = new Byte[len];
        buf[0] = frameType;
        buf[1] = (Byte)(seqId >> 8);
        buf[2] = (Byte)(seqId & 0xFF);
        buf[3] = 0;

        if (payload != null && payload.Length > 0)
            Buffer.BlockCopy(payload, 0, buf, FrameHeaderSize, payload.Length);

        return buf;
    }

    /// <summary>解析帧头</summary>
    private static Boolean ParseFrameHeader(IPacket pk, out Byte frameType, out UInt16 seqId, out ArraySegment<Byte> payload)
    {
        frameType = 0;
        seqId = 0;
        payload = default;

        var data = pk.GetSpan();
        if (data.Length < FrameHeaderSize) return false;

        frameType = data[0];
        seqId = (UInt16)((data[1] << 8) | data[2]);

        if (data.Length > FrameHeaderSize)
        {
            var arr = pk.ToArray();
            payload = new ArraySegment<Byte>(arr, FrameHeaderSize, arr.Length - FrameHeaderSize);
        }

        return true;
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttUdpListener]{format}", args);
    #endregion

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
}

/// <summary>UDP MQTT 对端信息</summary>
public class UdpMqttPeer
{
    /// <summary>远程端点标识</summary>
    public String RemoteKey { get; set; } = "";

    /// <summary>网络会话</summary>
    public INetSession? Session { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActiveTime { get; set; }

    /// <summary>接收序列号去重表</summary>
    public ConcurrentDictionary<UInt16, DateTime> ReceivedSeqs { get; } = new();

    private Int32 _seqId;

    /// <summary>生成下一个序列号</summary>
    /// <returns></returns>
    public UInt16 NextSeqId() => (UInt16)Interlocked.Increment(ref _seqId);
}

/// <summary>待确认帧</summary>
class PendingFrame
{
    /// <summary>序列号</summary>
    public UInt16 SeqId { get; set; }

    /// <summary>帧数据</summary>
    public Byte[] Data { get; set; } = null!;

    /// <summary>发送时间</summary>
    public DateTime SendTime { get; set; }

    /// <summary>重试次数</summary>
    public Int32 RetryCount { get; set; }
}

/// <summary>UDP MQTT 连接事件参数</summary>
public class UdpMqttConnectionEventArgs : EventArgs
{
    /// <summary>远程端点标识</summary>
    public String RemoteKey { get; }

    /// <summary>对端信息</summary>
    public UdpMqttPeer Peer { get; }

    /// <summary>实例化</summary>
    /// <param name="remoteKey">远程端点标识</param>
    /// <param name="peer">对端信息</param>
    public UdpMqttConnectionEventArgs(String remoteKey, UdpMqttPeer peer)
    {
        RemoteKey = remoteKey;
        Peer = peer;
    }
}

/// <summary>UDP MQTT 消息事件参数</summary>
public class UdpMqttMessageEventArgs : EventArgs
{
    /// <summary>远程端点标识</summary>
    public String RemoteKey { get; }

    /// <summary>MQTT 消息</summary>
    public MqttMessage Message { get; }

    /// <summary>实例化</summary>
    /// <param name="remoteKey">远程端点标识</param>
    /// <param name="message">MQTT 消息</param>
    public UdpMqttMessageEventArgs(String remoteKey, MqttMessage message)
    {
        RemoteKey = remoteKey;
        Message = message;
    }
}
