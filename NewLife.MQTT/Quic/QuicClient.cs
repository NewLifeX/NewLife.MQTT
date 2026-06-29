#if NET7_0_OR_GREATER
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Threading;

namespace NewLife.MQTT.Quic;

/// <summary>MQTT over QUIC 传输适配器</summary>
/// <remarks>
/// 封装 System.Net.Quic 的 QUIC 传输层，提供 MQTT 消息的收发和请求/响应匹配。
/// 作为 MqttClient 的传输后端，与 TCP/WebSocket 传输并行的第三种传输方式。
/// 需要 .NET 7+ 和操作系统 QUIC 支持（Windows 11+ / Linux with msquic）。
/// </remarks>
public class QuicClient : DisposeBase
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

    /// <summary>SSL 协议。QUIC 强制 TLS，默认使用系统默认</summary>
    public SslProtocols SslProtocol { get; set; }

    /// <summary>TLS 证书。用于验证服务端证书指纹，未指定时不验证</summary>
    public X509Certificate? Certificate { get; set; }

    /// <summary>是否处于活动状态</summary>
    public Boolean Active => _connection != null && _stream != null;

    /// <summary>性能跟踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private QuicConnection? _connection;
    private QuicStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private MqttVersion _protocolVersion = MqttVersion.V311;
    private readonly MqttFactory _factory = new();
    private Int32 _nextId;
    private readonly ConcurrentDictionary<UInt16, TaskCompletionSource<MqttMessage?>> _pendingRequests = new();
    #endregion

    #region 事件
    /// <summary>接收到消息时触发（非响应消息）</summary>
    public event EventHandler<MqttMessage>? Received;

    /// <summary>连接关闭时触发</summary>
    public event EventHandler? Closed;

    /// <summary>发生错误时触发</summary>
    public event EventHandler<Exception>? Error;
    #endregion

    #region 静态方法
    /// <summary>检查当前平台是否支持 QUIC</summary>
    public static Boolean IsSupported => QuicConnection.IsSupported;
    #endregion

    #region 方法
    /// <summary>连接到 QUIC 服务端</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));
        if (!QuicConnection.IsSupported)
            throw new NotSupportedException("当前平台不支持 QUIC，需要 Windows 11 或安装 msquic 的 Linux");

        WriteLog("正在通过 QUIC 连接 {0}:{1}", Server, Port);

        var sslOptions = new SslClientAuthenticationOptions
        {
            ApplicationProtocols = [new SslApplicationProtocol(AlpnProtocol)],
        };

        // 证书验证
        if (Certificate != null)
        {
            sslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
            {
                if (cert is X509Certificate2 cert2)
                {
                    var hash = cert2.GetCertHashString();
                    var expectedHash = (Certificate as X509Certificate2)?.GetCertHashString();
                    if (hash == expectedHash) return true;
                }
                // 未指定证书时跳过验证
                return Certificate == null || errors == SslPolicyErrors.None;
            };
        }
        else
        {
            // 默认跳过证书验证（与 MqttQuicClient 行为一致）
            sslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = new DnsEndPoint(Server, Port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ClientAuthenticationOptions = sslOptions,
        };

        _connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        _stream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);

        // 启动后台接收循环
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ReceiveLoopAsync(_receiveCts.Token);

        WriteLog("QUIC 连接已建立");
    }

    /// <summary>发送 MQTT 消息（不等待响应）</summary>
    /// <param name="message">MQTT 消息</param>
    public void SendMessage(MqttMessage message)
    {
        if (_stream == null) throw new InvalidOperationException("未建立 QUIC 连接");

        using var data = message.ToPacket();
        _stream.Write(data.GetSpan());
    }

    /// <summary>发送 MQTT 消息并等待响应</summary>
    /// <param name="message">MQTT 消息</param>
    /// <param name="cancellationToken"></param>
    /// <returns>响应消息，无响应则返回 null</returns>
    public Task<MqttMessage?> SendMessageAsync(MqttMessage message, CancellationToken cancellationToken = default)
    {
        if (_stream == null) throw new InvalidOperationException("未建立 QUIC 连接");

        // 分配消息 ID（用于 QoS 1/2 的消息匹配）
        if (message is MqttIdMessage idm && idm.Id == 0 && (message.Type != MqttType.Publish || message.QoS > 0))
            idm.Id = (UInt16)Interlocked.Increment(ref _nextId);

        var tcs = new TaskCompletionSource<MqttMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 注册待响应请求
        if (message is MqttIdMessage idm2 && idm2.Id > 0)
        {
            _pendingRequests[idm2.Id] = tcs;

            // 超时自动取消
            cancellationToken.Register(() =>
            {
                if (_pendingRequests.TryRemove(idm2.Id, out var pending))
                    pending.TrySetCanceled(cancellationToken);
            });
        }

        using var data = message.ToPacket();
        try
        {
            _stream.Write(data.GetSpan());
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove((message as MqttIdMessage)?.Id ?? 0, out _);
            tcs.TrySetException(ex);
            throw;
        }

        // 不需要响应的消息（如 QoS 0 Publish）
        if (message.Type == MqttType.Publish && message.QoS == QualityOfService.AtMostOnce)
        {
            tcs.TrySetResult(null!);
        }

        return tcs.Task;
    }

    /// <summary>关闭 QUIC 连接</summary>
    /// <returns></returns>
    public async Task CloseAsync()
    {
        WriteLog("关闭 QUIC 连接");

        _receiveCts?.Cancel();

        // 清理所有待响应请求
        foreach (var item in _pendingRequests)
        {
            item.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();

        if (_stream != null)
        {
            try
            {
                _stream.CompleteWrites();
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            catch { }
            _stream = null;
        }

        if (_connection != null)
        {
            try
            {
                await _connection.CloseAsync(0, default).ConfigureAwait(false);
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch { }
            _connection = null;
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;

        foreach (var item in _pendingRequests)
        {
            item.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();

        _stream?.DisposeAsync().AsTask().Wait(1000);
        _connection?.DisposeAsync().AsTask().Wait(1000);
        _stream = null;
        _connection = null;
    }
    #endregion

    #region 接收循环
    /// <summary>后台接收循环</summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new Byte[64 * 1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stream = _stream;
                if (stream == null) break;

                var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    // 连接正常关闭
                    WriteLog("QUIC 流已关闭");
                    break;
                }

                // 解码 MQTT 消息
                var pk = new ArrayPacket(buffer, 0, bytesRead);
                ProcessReceivedData(pk);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (QuicException ex) when (ex.QuicError == QuicError.ConnectionAborted)
            {
                WriteLog("QUIC 连接被中止: {0}", ex.Message);
                Error?.Invoke(this, ex);
                break;
            }
            catch (Exception ex)
            {
                WriteLog("QUIC 接收异常: {0}", ex.Message);
                Error?.Invoke(this, ex);

                // 连接异常，退出接收循环
                if (_connection == null || _stream == null) break;

                // 短暂等待后重试
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

        // 连接断开
        if (!cancellationToken.IsCancellationRequested && _connection != null)
        {
            WriteLog("QUIC 接收循环退出，触发关闭");
            await CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>处理接收到的数据</summary>
    /// <param name="pk">数据包</param>
    private void ProcessReceivedData(IPacket pk)
    {
        try
        {
            var msg = _factory.ReadMessage(pk, _protocolVersion);
            if (msg == null) return;

            // 记录协议版本（从 CONNACK 或服务端响应中获取）
            if (msg is ConnAck && _protocolVersion < MqttVersion.V500)
            {
                // 保持当前版本
            }

            if (msg.Reply)
            {
                // 响应消息：匹配待响应请求
                var matched = false;
                if (msg is MqttIdMessage idm)
                {
                    matched = _pendingRequests.TryRemove(idm.Id, out var tcs);
                    if (matched && tcs != null)
                        tcs.TrySetResult(msg);
                }

                // 部分响应消息没有 Id（如 ConnAck），需要特殊匹配
                if (!matched)
                {
                    // 尝试匹配最近的未匹配请求（类型前后配对）
                    MatchPendingRequest(msg);
                }
            }
            else
            {
                // 非响应消息：触发 Received 事件
                Received?.Invoke(this, msg);
            }
        }
        catch (Exception ex)
        {
            WriteLog("解析 MQTT 消息异常: {0}", ex.Message);
        }
    }

    /// <summary>尝试匹配未按 ID 匹配的响应消息</summary>
    /// <remarks>
    /// 某些响应消息（如 ConnAck）不携带与请求相同的 ID，
    /// 需要根据消息类型的前后配对关系来匹配。
    /// </remarks>
    private void MatchPendingRequest(MqttMessage response)
    {
        // CONNECT → CONNACK
        if (response.Type == MqttType.ConnAck)
        {
            // ConnAck 通过类型匹配（而不是 ID）
            foreach (var item in _pendingRequests)
            {
                item.Value.TrySetResult(response);
                _pendingRequests.Clear();
                return;
            }
        }

        // DISCONNECT 不期望响应，但可能触发清理
        if (response.Type == MqttType.Disconnect)
        {
            foreach (var item in _pendingRequests)
            {
                item.Value.TrySetCanceled();
            }
            _pendingRequests.Clear();
        }
    }
    #endregion

    #region 日志
    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[QuicClient]{format}", args);
    #endregion
}
#endif
