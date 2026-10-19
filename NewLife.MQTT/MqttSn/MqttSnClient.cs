using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Threading;

namespace NewLife.MQTT.MqttSn;

/// <summary>MQTT-SN Client. Communicates with gateway over UDP</summary>
public class MqttSnClient : DisposeBase
{
    #region Properties
    public IPEndPoint? Gateway { get; set; }
    public Int32 LocalPort { get; set; }
    public String ClientId { get; set; } = String.Empty;
    public UInt16 KeepAliveSeconds { get; set; } = 300;
    public Boolean CleanSession { get; set; } = true;
    public String? WillTopic { get; set; }
    public Byte[]? WillMessage { get; set; }
    public Boolean Connected { get; private set; }
    public ILog Log { get; set; } = Logger.Null;

    private readonly Dictionary<String, Action<String, Byte[]>> _subscriptions = new();
    private UInt16 _nextMessageId = 1;
    private UdpClient? _udpClient;
    private TimerX? _keepAliveTimer;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<MqttSnMessage> _pendingResponses = new();
    private readonly SemaphoreSlim _recvSemaphore = new(1, 1);
    #endregion

    #region Connection
    public async Task<IPEndPoint?> SearchGatewayAsync(Int32 timeoutMs = 3000)
    {
        using var searchClient = new UdpClient(0) { EnableBroadcast = true };
        var data = new SearchGwMessage { Radius = 1 }.Encode();
        await searchClient.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, 1884)).ConfigureAwait(false);

        var cts = new CancellationTokenSource(timeoutMs);
        try
        {
#if NET6_0_OR_GREATER
            var result = await searchClient.ReceiveAsync(cts.Token).ConfigureAwait(false);
#else
            var result = await searchClient.ReceiveAsync().ConfigureAwait(false);
#endif
            var msg = MqttSnCodec.Decode(result.Buffer, 0, result.Buffer.Length);
            if (msg is GwInfoMessage gwInfo)
            {
                WriteLog("Found gateway: {0}, ID={1}", result.RemoteEndPoint, gwInfo.GatewayId);
                Gateway = result.RemoteEndPoint;
                return result.RemoteEndPoint;
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        return null;
    }

    public async Task<Boolean> ConnectAsync()
    {
        if (Connected) return true;
        if (Gateway == null) throw new InvalidOperationException(L.T("Gateway not set, call SearchGatewayAsync first"));

        _udpClient ??= new UdpClient(LocalPort);
        _cts = new CancellationTokenSource();

        var flags = (Byte)(CleanSession ? 0x02 : 0x00);
        var connectMsg = new MqttSnConnectMessage { Flags = flags, Duration = KeepAliveSeconds, ClientId = ClientId };
        await SendAsync(connectMsg).ConfigureAwait(false);

        var ackResult = await ReceiveAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (ackResult == null)
        {
            WriteLog("ConnectAsync: timeout waiting CONNACK (no response within 5s)");
            return false;
        }
        if (ackResult is not MqttSnConnAckMessage connAck)
        {
            WriteLog("ConnectAsync: unexpected message type {0} instead of CONNACK", ackResult.MessageType);
            return false;
        }
        if (connAck.ReturnCode != MqttSnReturnCode.Accepted)
        {
            WriteLog("ConnectAsync: CONNACK rejected with {0}", connAck.ReturnCode);
            return false;
        }

        Connected = true;

        if (!WillTopic.IsNullOrEmpty() && WillMessage != null)
        {
            await SendAsync(new WillTopicMessage { Flags = 0, TopicName = WillTopic }).ConfigureAwait(false);
            await SendAsync(new WillMsgMessage { Data = WillMessage }).ConfigureAwait(false);
        }

        _keepAliveTimer = new TimerX(s => SendPingReq(), null, KeepAliveSeconds * 1000 / 2, KeepAliveSeconds * 1000 / 2);
        _ = ReceiveLoopAsync(_cts.Token);

        WriteLog("MQTT-SN connected, gateway={0}", Gateway);
        return true;
    }

    public async Task DisconnectAsync(UInt16 sleepSeconds = 0)
    {
        Connected = false;
        _keepAliveTimer.TryDispose();
        _cts?.Cancel();

        var disconnect = new MqttSnDisconnectMessage();
        if (sleepSeconds > 0) disconnect.Duration = sleepSeconds;
        await SendAsync(disconnect).ConfigureAwait(false);

        _udpClient?.Close();
        WriteLog("MQTT-SN disconnected");
    }
    #endregion

    #region Messaging
    public async Task SubscribeAsync(String topicFilter, Action<String, Byte[]> callback)
    {
        if (!Connected) throw new InvalidOperationException(L.T("Not connected"));

        var msgId = NextMessageId();
        var msg = new MqttSnSubscribeMessage { Flags = 0, MessageId = msgId, TopicName = topicFilter };
        await SendAsync(msg).ConfigureAwait(false);
        _subscriptions[topicFilter] = callback;

        var ackResult = await ReceiveAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        if (ackResult is MqttSnSubAckMessage subAck)
            WriteLog("Subscribed: {0} -> TopicId={1}", topicFilter, subAck.TopicId);
    }

    public async Task PublishAsync(UInt16 topicId, Byte[] data, QualityOfService qos = QualityOfService.AtMostOnce)
    {
        if (!Connected) throw new InvalidOperationException(L.T("Not connected"));

        var flags = (Byte)(((Int32)qos << 5) | (Int32)MqttSnTopicIdType.Normal);
        var msg = new MqttSnPublishMessage { Flags = flags, TopicId = topicId, Data = data };
        if (qos > QualityOfService.AtMostOnce) msg.MessageId = NextMessageId();

        await SendAsync(msg).ConfigureAwait(false);

        if (qos == QualityOfService.AtLeastOnce)
        {
            var ackResult = await ReceiveAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (ackResult is MqttSnPubAckMessage) WriteLog("QoS 1 pub ack: TopicId={0}", topicId);
        }
    }

    public async Task<UInt16> RegisterTopicAsync(String topicName)
    {
        if (!Connected) throw new InvalidOperationException(L.T("Not connected"));

        var msgId = NextMessageId();
        var msg = new RegisterMessage { TopicId = 0, MessageId = msgId, TopicName = topicName };
        await SendAsync(msg).ConfigureAwait(false);

        var ackResult = await ReceiveAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        if (ackResult is RegAckMessage regAck && regAck.ReturnCode == MqttSnReturnCode.Accepted)
        {
            WriteLog("Registered topic: {0} -> TopicId={1}", topicName, regAck.TopicId);
            return regAck.TopicId;
        }
        return 0;
    }
    #endregion

    #region Internal
    private void SendPingReq()
    {
        if (!Connected) return;
        SendAsync(new MqttSnPingReqMessage { ClientId = ClientId }).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && Connected)
        {
            try
            {
                var msg = await RawReceiveAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                if (msg == null) continue;

                if (msg is MqttSnPublishMessage pub)
                {
                    foreach (var sub in _subscriptions.Values)
                        sub.Invoke("sn/" + pub.TopicId, pub.Data);
                }
                else
                {
                    _pendingResponses.Enqueue(msg);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { WriteLog("Recv error: {0}", ex.Message); }
        }
    }

    private async Task<MqttSnMessage?> RawReceiveAsync(TimeSpan timeout)
    {
        if (_udpClient == null) return null;

        await _recvSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var cts = new CancellationTokenSource(timeout);
            try
            {
#if NET6_0_OR_GREATER
                var result = await _udpClient.ReceiveAsync(cts.Token).ConfigureAwait(false);
#else
                var result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
#endif
                return MqttSnCodec.Decode(result.Buffer, 0, result.Buffer.Length);
            }
            catch (OperationCanceledException) { return null; }
            catch (ObjectDisposedException) { return null; }
        }
        finally
        {
            _recvSemaphore.Release();
        }
    }

    private async Task<MqttSnMessage?> ReceiveAsync(TimeSpan timeout)
    {
        // 优先检查已缓冲的响应队列
        if (_pendingResponses.TryDequeue(out var cached))
            return cached;

        var result = await RawReceiveAsync(timeout).ConfigureAwait(false);
        if (result != null) return result;

        // 超时后重新检查队列——接收循环可能已取到并放入
        if (_pendingResponses.TryDequeue(out cached))
            return cached;

        return null;
    }

    private async Task SendAsync(MqttSnMessage message)
    {
        if (_udpClient == null || Gateway == null)
        {
            WriteLog("SendAsync skipped: udp={0}, gw={1}", _udpClient != null, Gateway != null);
            return;
        }
        var data = message.Encode();
        WriteLog("SendAsync: type={0}, len={1}, to={2}", message.MessageType, data.Length, Gateway);
        await _udpClient.SendAsync(data, data.Length, Gateway).ConfigureAwait(false);
    }

    private UInt16 NextMessageId()
    {
        var id = _nextMessageId;
        _nextMessageId = (UInt16)(id >= UInt16.MaxValue ? 1 : id + 1);
        return id;
    }

    private void WriteLog(String format, params Object?[] args) => Log.InfoPrefixed("[MqttSnClient] ", format, args);
    #endregion

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        if (Connected) DisconnectAsync().Wait(3000);
        _cts?.Dispose();
        _udpClient?.Close();
        _udpClient = null;
    }
}
