#if NET7_0_OR_GREATER
using System.Net.Quic;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Quic;

/// <summary>MQTT over QUIC 服务端会话</summary>
/// <remarks>
/// 为每个 QUIC 连接创建一个会话实例，负责从 QUIC 流中读取 MQTT 报文、
/// 交给 MqttHandler 处理，并将响应写回 QUIC 流。
/// 不支持 MQTT 集群消息转发（仅处理单连接消息）。
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

    /// <summary>指令处理器</summary>
    public IMqttHandler? Handler { get; set; }

    /// <summary>性能跟踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>会话关闭时触发</summary>
    public event EventHandler? Closed;

    private readonly MqttFactory _factory = new();
    private CancellationTokenSource? _receiveCts;
    private Int32 _nextId;
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

        Handler?.Close("会话已销毁");

        try
        {
            Stream.DisposeAsync().AsTask().Wait(1000);
            Connection.DisposeAsync().AsTask().Wait(1000);
        }
        catch { }
    }
    #endregion

    #region 方法
    /// <summary>启动会话处理</summary>
    /// <param name="cancellationToken"></param>
    public void Start(CancellationToken cancellationToken = default)
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = ReceiveLoopAsync(_receiveCts.Token);

        WriteLog("QUIC 会话已启动，客户端标识: {0}", ClientId ?? "(未知)");
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
            WriteLog("发送消息失败: {0}", ex.Message);
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
                    WriteLog("QUIC 流已关闭，客户端: {0}", ClientId ?? "(未知)");
                    break;
                }

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
                break;
            }
            catch (Exception ex)
            {
                WriteLog("QUIC 接收异常: {0}", ex.Message);

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

        Handler?.Close("连接已断开");
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>处理接收到的数据</summary>
    private void ProcessReceivedData(IPacket pk)
    {
        try
        {
            var msg = _factory.ReadMessage(pk, ProtocolVersion);
            if (msg == null) return;

            // 从 CONNECT 消息中更新协议版本
            if (msg is ConnectMessage connect)
            {
                ProtocolVersion = connect.ProtocolLevel;
                ClientId = connect.ClientId;
            }

            // 分配消息 ID（用于需要响应的消息）
            if (msg is MqttIdMessage idm && idm.Id == 0 && (msg.Type != MqttType.Publish || msg.QoS > 0))
                idm.Id = (UInt16)Interlocked.Increment(ref _nextId);

            if (Log != null && Log.Level <= LogLevel.Debug)
            {
                if (msg is PublishMessage pm)
                    WriteLog("<={0} {1}", msg, pm.Payload?.ToStr());
                else
                    WriteLog("<={0}", msg);
            }

            // 处理消息
            var handler = Handler;
            if (handler != null)
            {
                var result = handler.Process(msg);

                // 匹配响应 ID
                if (result is MqttIdMessage response && response.Id == 0 && msg is MqttIdMessage request)
                    response.Id = request.Id;

                if (result != null)
                {
                    if (Log != null && Log.Level <= LogLevel.Debug) WriteLog("=> {0}", result);
                    SendMessage(result);
                }
            }

            // 客户端断开
            if (msg.Type == MqttType.Disconnect)
            {
                WriteLog("客户端主动断开: {0}", ClientId ?? "(未知)");
                Handler?.Close("客户端主动断开");
                Dispose();
            }
        }
        catch (Exception ex)
        {
            WriteLog("处理 MQTT 消息异常: {0}", ex.Message);
        }
    }
    #endregion

    #region 日志
    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[MqttQuicSession]{format}", args);
    #endregion
}
#endif
