#if NET9_0_OR_GREATER
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Quic;

/// <summary>MQTT over QUIC 客户端</summary>
/// <remarks>
/// 基于 .NET 9+ System.Net.Quic API 实现 MQTT over QUIC 传输。
/// QUIC 提供低延迟、无队头阻塞的传输层，适合物联网弱网环境。
/// 需要操作系统支持（Windows 11 / Linux with msquic）。
/// </remarks>
public class MqttQuicClient : DisposeBase
{
    #region 属性
    /// <summary>服务端地址</summary>
    public String? Server { get; set; }

    /// <summary>服务端端口。默认14567（MQTT over QUIC 常用端口）</summary>
    public Int32 Port { get; set; } = 14567;

    /// <summary>超时时间（毫秒）。默认15000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>ALPN 协议名。默认 mqtt</summary>
    public String AlpnProtocol { get; set; } = "mqtt";

    private QuicConnection? _connection;
    private QuicStream? _stream;
    #endregion

    #region 方法
    /// <summary>检查当前平台是否支持 QUIC</summary>
    public static Boolean IsSupported => QuicConnection.IsSupported;

    /// <summary>连接到 QUIC 服务端</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));
        if (!QuicConnection.IsSupported)
            throw new NotSupportedException("当前平台不支持 QUIC，需要 Windows 11 或安装 msquic 的 Linux");

        WriteLog("正在通过 QUIC 连接 {0}:{1}", Server, Port);

        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = new DnsEndPoint(Server, Port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol(AlpnProtocol)],
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        };

        _connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        _stream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);

        WriteLog("QUIC 连接已建立");
    }

    /// <summary>发送 MQTT 消息</summary>
    /// <param name="message">MQTT 消息</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SendAsync(MqttMessage message, CancellationToken cancellationToken = default)
    {
        var stream = _stream ?? throw new InvalidOperationException("未建立 QUIC 连接");

        var data = message.ToArray();
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>接收 MQTT 消息</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<MqttMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var stream = _stream ?? throw new InvalidOperationException("未建立 QUIC 连接");

        var buffer = new Byte[65536];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead == 0) return null;

        var pk = new ArrayPacket(buffer, 0, bytesRead);
        var factory = new MqttFactory();
        return factory.ReadMessage(pk);
    }

    /// <summary>关闭 QUIC 连接</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_stream != null)
        {
            _stream.CompleteWrites();
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(0, cancellationToken).ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _stream?.DisposeAsync().AsTask().Wait(1000);
        _connection?.DisposeAsync().AsTask().Wait(1000);
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
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttQuic]{format}", args);
    #endregion
}

/// <summary>MQTT over QUIC 监听器</summary>
/// <remarks>
/// 基于 .NET 9+ System.Net.Quic API 实现 MQTT over QUIC 服务端监听。
/// 每个 QUIC 连接打开一个双向流用于 MQTT 报文传输。
/// </remarks>
public class MqttQuicListener : DisposeBase
{
    #region 属性
    /// <summary>监听端口。默认14567</summary>
    public Int32 Port { get; set; } = 14567;

    /// <summary>ALPN 协议名。默认 mqtt</summary>
    public String AlpnProtocol { get; set; } = "mqtt";

    /// <summary>TLS 证书（QUIC 强制 TLS）</summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>是否正在监听</summary>
    public Boolean IsListening { get; private set; }

    /// <summary>接收到新连接时触发</summary>
    public event EventHandler<QuicMqttConnectionEventArgs>? ConnectionReceived;

    private QuicListener? _listener;
    private CancellationTokenSource? _cts;
    #endregion

    #region 方法
    /// <summary>检查当前平台是否支持 QUIC</summary>
    public static Boolean IsSupported => QuicListener.IsSupported;

    /// <summary>启动 QUIC 监听</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Certificate == null) throw new ArgumentNullException(nameof(Certificate), "QUIC 强制 TLS，必须指定证书");
        if (!QuicListener.IsSupported)
            throw new NotSupportedException("当前平台不支持 QUIC，需要 Windows 11 或安装 msquic 的 Linux");

        WriteLog("启动 MQTT over QUIC 监听，端口 {0}", Port);

        var options = new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Any, Port),
            ApplicationProtocols = [new SslApplicationProtocol(AlpnProtocol)],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol(AlpnProtocol)],
                    ServerCertificate = Certificate,
                },
            }),
        };

        _listener = await QuicListener.ListenAsync(options, cancellationToken).ConfigureAwait(false);
        IsListening = true;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = AcceptLoopAsync(_cts.Token);

        WriteLog("MQTT over QUIC 监听已启动");
    }

    /// <summary>停止 QUIC 监听</summary>
    /// <returns></returns>
    public async Task StopAsync()
    {
        IsListening = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_listener != null)
        {
            await _listener.DisposeAsync().ConfigureAwait(false);
            _listener = null;
        }

        WriteLog("MQTT over QUIC 监听已停止");
    }

    /// <summary>接受连接循环</summary>
    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var connection = await _listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
                var stream = await connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);

                var args = new QuicMqttConnectionEventArgs(connection, stream);
                ConnectionReceived?.Invoke(this, args);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                WriteLog("QUIC 接受连接异常: {0}", ex.Message);
            }
        }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _cts?.Cancel();
        _listener?.DisposeAsync().AsTask().Wait(1000);
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
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttQuicListener]{format}", args);
    #endregion
}

/// <summary>QUIC MQTT 连接事件参数</summary>
public class QuicMqttConnectionEventArgs : EventArgs
{
    /// <summary>QUIC 连接</summary>
    public QuicConnection Connection { get; }

    /// <summary>QUIC 双向流</summary>
    public QuicStream Stream { get; }

    /// <summary>实例化</summary>
    /// <param name="connection">QUIC 连接</param>
    /// <param name="stream">QUIC 双向流</param>
    public QuicMqttConnectionEventArgs(QuicConnection connection, QuicStream stream)
    {
        Connection = connection;
        Stream = stream;
    }
}
#endif
