#if NET7_0_OR_GREATER
using System.Net.Quic;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Quic;

/// <summary>MQTT over QUIC 服务端会话</summary>
/// <remarks>
/// <para>为每个 QUIC 连接创建一个会话实例，负责从 QUIC 流中定界 MQTT 帧、交给协议会话处理，并将响应写回 QUIC 流。</para>
/// <para>协议处理复用 <see cref="MqttSession"/> 桥接模式（与 WebSocket 桥接一致）：没有协议泵，发送走 QUIC 流，
/// 请求-响应配对由协议会话自行完成，交换机/集群交换/认证等能力与 TCP 接入完全一致。</para>
/// </remarks>
public class MqttQuicSession : DisposeBase
{
    #region 属性
    /// <summary>QUIC 连接</summary>
    public QuicConnection Connection { get; }

    /// <summary>QUIC 双向流</summary>
    public QuicStream Stream { get; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>MQTT 协议版本</summary>
    public MqttVersion ProtocolVersion { get; set; } = MqttVersion.V311;

    /// <summary>是否处于活动状态</summary>
    public Boolean Active => !Disposed && _receiveCts != null && !_receiveCts.IsCancellationRequested;

    private IMqttHandler? _handler;

    /// <summary>指令处理器。可为空，未指定时由协议会话按默认规则创建</summary>
    public IMqttHandler? Handler
    {
        get => _handler;
        set
        {
            _handler = value;
            if (_session != null) _session.MqttHandler = value!;
        }
    }

    /// <summary>性能跟踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>会话关闭时触发</summary>
    public event EventHandler? Closed;

    private readonly MqttCodec _codec = new();
    private MqttFrameDeframer? _deframer;
    private MqttSession? _session;
    private CancellationTokenSource? _receiveCts;
    #endregion

    #region 构造
    /// <summary>实例化 QUIC 会话</summary>
    /// <param name="connection">QUIC 连接</param>
    /// <param name="stream">QUIC 双向流</param>
    public MqttQuicSession(QuicConnection connection, QuicStream stream)
    {
        Connection = connection;
        Stream = stream;
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;

        // 协议会话关闭会级联关闭处理器（交换机注销等）
        var session = _session;
        _session = null;
        session.TryDispose();

        CloseTransport();
    }

    /// <summary>关闭传输层。幂等，重复调用安全</summary>
    private void CloseTransport()
    {
        try { Stream.DisposeAsync().AsTask().Wait(1000); } catch { }
        try { Connection.DisposeAsync().AsTask().Wait(1000); } catch { }
    }
    #endregion

    #region 方法
    /// <summary>启动会话处理</summary>
    /// <param name="host">MQTT 服务端。协议会话经其注入交换机、集群交换与认证等能力</param>
    /// <param name="cancellationToken"></param>
    public void Start(MqttServer host, CancellationToken cancellationToken = default)
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // 协议会话：桥接 QUIC 流，与 WebSocket 桥接同一模型
        var session = new MqttSession();
        if (_handler != null) session.MqttHandler = _handler;
        if (!session.AttachQuic(host, Connection.RemoteEndPoint + "", SendFrame))
        {
            WriteLog("Connection rate limit exceeded; rejecting QUIC connection");
            CloseTransport();
            return;
        }

        _session = session;
        _handler ??= session.MqttHandler;
        session.OnDisposed += (s, e) => CloseTransport();

        _ = ReceiveLoopAsync(_receiveCts.Token);

        WriteLog("QUIC session started; client identifier: {0}", ClientId ?? "(unknown)");
    }

    /// <summary>发送 MQTT 消息到客户端</summary>
    /// <param name="message">MQTT 消息</param>
    public void SendMessage(MqttMessage message)
    {
        if (Disposed) return;

        try
        {
            using var data = message.ToPacket();
            Stream.Write(data.GetSpan());
        }
        catch (Exception ex)
        {
            WriteLog("Failed to send message: {0}", ex.Message);
        }
    }

    /// <summary>后台接收循环</summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new Byte[64 * 1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    WriteLog("QUIC stream closed; client: {0}", ClientId ?? "(unknown)");
                    break;
                }

                var deframer = _deframer ??= new MqttFrameDeframer();
                var pk = new ArrayPacket(buffer, 0, bytesRead);
                if (!deframer.Process(_codec, pk, frame => _session?.Process(frame)) && deframer.Overflow)
                {
                    WriteLog("QUIC packet residue exceeded the limit; closing the connection");
                    break;
                }

                // CONNECT 解码后同步协议版本与客户端标识，供日志与诊断使用
                var session = _session;
                if (session != null)
                {
                    ProtocolVersion = session.ProtocolLevel;
                    ClientId ??= (session.MqttHandler as MqttHandler)?.ClientId;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                // 会话关闭过程中流已被释放，属正常生命周期，静默退出
                break;
            }
            catch (QuicException ex) when (ex.QuicError == QuicError.ConnectionAborted)
            {
                WriteLog("QUIC connection aborted: {0}", ex.Message);
                break;
            }
            catch (Exception ex)
            {
                WriteLog("QUIC receive error: {0}", ex.Message);

                if (Disposed) break;

                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }

        _session?.Close(L.T("Connection closed"));
        Closed?.Invoke(this, EventArgs.Empty);

        // 接收循环结束即整个会话生命周期结束，自行释放传输资源
        Dispose();
    }

    /// <summary>把协议会话产出的帧写到 QUIC 流</summary>
    /// <param name="frame">MQTT 协议帧</param>
    /// <returns>写入字节数</returns>
    private Int32 SendFrame(IMessage frame)
    {
        var data = _codec.Build(frame)!;
        try
        {
            var total = data.Total;
            Stream.Write(data.GetSpan());
            return total;
        }
        finally { data.TryDispose(); }
    }
    #endregion

    #region 日志
    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.InfoPrefixed("[MqttQuicSession]", format, args);
    #endregion
}
#endif
