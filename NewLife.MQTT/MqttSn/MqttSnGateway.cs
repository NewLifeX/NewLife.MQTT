using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Threading;

namespace NewLife.MQTT.MqttSn;

/// <summary>MQTT-SN Gateway. Listens on UDP and bridges to standard MQTT Broker</summary>
public class MqttSnGateway : DisposeBase
{
    #region Properties
    public Byte GatewayId { get; set; } = 1;
    public Int32 Port { get; set; } = 1884;
    public Int32 AdvertiseInterval { get; set; } = 60;
    public UInt16 AdvertiseDuration { get; set; } = 300;
    public String MqttBroker { get; set; } = "tcp://127.0.0.1:1883";
    public MqttClient? MqttClient { get; set; }
    public MqttSnTopicRegistry TopicRegistry { get; } = new();
    public ILog Log { get; set; } = Logger.Null;
    public ITracer? Tracer { get; set; }
    public Boolean Active { get; private set; }

    /// <summary>Actual listening port (after binding, reads from socket)</summary>
    public Int32 ActualPort
    {
        get
        {
            if (_udpServer?.Client?.LocalEndPoint is IPEndPoint ep)
                return ep.Port;
            return Port;
        }
    }

    private readonly ConcurrentDictionary<IPEndPoint, MqttSnClientState> _clients = new();
    private UdpClient? _udpServer;
    private TimerX? _advertiseTimer;
    #endregion

    #region Start/Stop
    public void Start()
    {
        if (Active) return;
        Active = true;

        MqttClient ??= new MqttClient { Server = MqttBroker, Log = Log, Tracer = Tracer };
        if (!MqttClient.IsConnected) MqttClient.ConnectAsync().Wait();

        _udpServer = new UdpClient(Port) { EnableBroadcast = true };
        _ = ListenAsync();

        _advertiseTimer = new TimerX(s => SendAdvertise(), null, 1000, AdvertiseInterval * 1000);

        WriteLog("MQTT-SN Gateway started, port={0}, broker={1}", Port, MqttBroker);
    }

    public void Stop()
    {
        Active = false;
        _advertiseTimer.TryDispose();
        _udpServer?.Close();
        _udpServer = null;
        _clients.Clear();
        WriteLog("MQTT-SN Gateway stopped");
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
    #endregion

    #region UDP Listen
    private async Task ListenAsync()
    {
        while (Active && _udpServer != null)
        {
            try
            {
                var result = await _udpServer.ReceiveAsync().ConfigureAwait(false);
                _ = ProcessMessageAsync(result.Buffer, result.RemoteEndPoint);
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { WriteLog("UDP recv error: {0}", ex.Message); }
        }
    }

    private async Task ProcessMessageAsync(Byte[] data, IPEndPoint remote)
    {
        var msg = MqttSnCodec.Decode(data, 0, data.Length);
        if (msg == null) return;

        using var span = Tracer?.NewSpan("mqttsn:gateway", msg.MessageType.ToString());

        switch (msg.MessageType)
        {
            case MqttSnMessageType.SearchGw: await HandleSearchGwAsync((SearchGwMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Connect: await HandleConnectAsync((MqttSnConnectMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Register: await HandleRegisterAsync((RegisterMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Publish: await HandlePublishAsync((MqttSnPublishMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Subscribe: await HandleSubscribeAsync((MqttSnSubscribeMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Unsubscribe: await HandleUnsubscribeAsync((MqttSnUnsubscribeMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.PingReq: await HandlePingReqAsync((MqttSnPingReqMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.Disconnect: await HandleDisconnectAsync((MqttSnDisconnectMessage)msg, remote).ConfigureAwait(false); break;
            case MqttSnMessageType.WillTopic: HandleWillTopic((WillTopicMessage)msg, remote); break;
            case MqttSnMessageType.WillMsg: HandleWillMsg((WillMsgMessage)msg, remote); break;
        }
    }
    #endregion

    #region Message Handlers
    private async Task HandleSearchGwAsync(SearchGwMessage msg, IPEndPoint remote)
    {
        var gwInfo = new GwInfoMessage { GatewayId = GatewayId };
        await SendAsync(gwInfo, remote).ConfigureAwait(false);
        WriteLog("SEARCHGW from {0}, sent GWINFO", remote);
    }

    private async Task HandleConnectAsync(MqttSnConnectMessage msg, IPEndPoint remote)
    {
        WriteLog("MQTT-SN client {0} connecting, ClientId={1}", remote, msg.ClientId);

        if (MqttClient == null || !MqttClient.IsConnected)
        {
            await SendAsync(new MqttSnConnAckMessage { ReturnCode = MqttSnReturnCode.RejectedCongestion }, remote).ConfigureAwait(false);
            return;
        }

        var state = new MqttSnClientState
        {
            ClientId = msg.ClientId,
            CleanSession = msg.CleanSession,
            KeepAliveSeconds = msg.Duration,
            RemoteEndPoint = remote,
        };
        _clients[remote] = state;

        await SendAsync(new MqttSnConnAckMessage { ReturnCode = MqttSnReturnCode.Accepted }, remote).ConfigureAwait(false);
    }

    private async Task HandleRegisterAsync(RegisterMessage msg, IPEndPoint remote)
    {
        if (msg.TopicName.IsNullOrEmpty())
        {
            await SendAsync(new RegAckMessage { TopicId = msg.TopicId, MessageId = msg.MessageId, ReturnCode = MqttSnReturnCode.RejectedInvalidTopicId }, remote).ConfigureAwait(false);
            return;
        }

        var topicId = TopicRegistry.Register(msg.TopicName);
        WriteLog("Registered topic: {0} -> ID={1}", msg.TopicName, topicId);

        await SendAsync(new RegAckMessage { TopicId = topicId, MessageId = msg.MessageId, ReturnCode = MqttSnReturnCode.Accepted }, remote).ConfigureAwait(false);
    }

    private async Task HandlePublishAsync(MqttSnPublishMessage msg, IPEndPoint remote)
    {
        var topic = ResolveTopic(msg.TopicId, msg.TopicIdType);
        if (topic == null) return;

        if (MqttClient != null && MqttClient.IsConnected)
        {
            await MqttClient.PublishAsync(topic, msg.Data, msg.QoS).ConfigureAwait(false);
            WriteLog("MQTT-SN->MQTT publish: Topic={0}, QoS={1}, Len={2}", topic, msg.QoS, msg.Data.Length);
        }

        if (msg.QoS == QualityOfService.AtLeastOnce)
        {
            await SendAsync(new MqttSnPubAckMessage { TopicId = msg.TopicId, MessageId = msg.MessageId, ReturnCode = MqttSnReturnCode.Accepted }, remote).ConfigureAwait(false);
        }
    }

    private async Task HandleSubscribeAsync(MqttSnSubscribeMessage msg, IPEndPoint remote)
    {
        var topic = ResolveTopic(msg.TopicId, msg.TopicIdType) ?? msg.TopicName;
        if (topic.IsNullOrEmpty())
        {
            await SendAsync(new MqttSnSubAckMessage { TopicId = msg.TopicId, MessageId = msg.MessageId, ReturnCode = MqttSnReturnCode.RejectedInvalidTopicId }, remote).ConfigureAwait(false);
            return;
        }

        var topicId = TopicRegistry.Register(topic);

        if (MqttClient != null && MqttClient.IsConnected)
        {
            await MqttClient.SubscribeAsync(topic, e =>
            {
                var payload = e.Payload is Packet pk ? pk.ToArray() : [];
                var pub = new MqttSnPublishMessage
                {
                    Flags = 0,
                    TopicId = topicId,
                    Data = payload,
                };
                _ = SendAsync(pub, remote);
            }).ConfigureAwait(false);
        }

        await SendAsync(new MqttSnSubAckMessage { Flags = 0, TopicId = topicId, MessageId = msg.MessageId, ReturnCode = MqttSnReturnCode.Accepted }, remote).ConfigureAwait(false);
        WriteLog("MQTT-SN subscribed: {0} -> TopicId={1}", topic, topicId);
    }

    private async Task HandleUnsubscribeAsync(MqttSnUnsubscribeMessage msg, IPEndPoint remote)
    {
        var topic = TopicRegistry.Lookup(msg.TopicId);
        if (topic != null && MqttClient != null && MqttClient.IsConnected)
            await MqttClient.UnsubscribeAsync(new[] { topic }).ConfigureAwait(false);

        await SendAsync(new MqttSnUnsubAckMessage { MessageId = msg.MessageId }, remote).ConfigureAwait(false);
    }

    private async Task HandlePingReqAsync(MqttSnPingReqMessage msg, IPEndPoint remote)
    {
        if (_clients.TryGetValue(remote, out var state)) state.LastPing = DateTime.UtcNow;
        await SendAsync(new MqttSnPingRespMessage(), remote).ConfigureAwait(false);
    }

    private async Task HandleDisconnectAsync(MqttSnDisconnectMessage msg, IPEndPoint remote)
    {
        if (_clients.TryGetValue(remote, out var state))
        {
            if (state.WillTopic != null && state.WillData != null && MqttClient != null)
            {
                await MqttClient.PublishAsync(state.WillTopic, state.WillData, QualityOfService.AtMostOnce).ConfigureAwait(false);
                WriteLog("Published will message: Topic={0}", state.WillTopic);
            }

            if (msg.Duration.HasValue && msg.Duration > 0)
            {
                state.IsSleeping = true;
                state.SleepUntil = DateTime.UtcNow.AddSeconds(msg.Duration.Value);
                WriteLog("Client {0} entering sleep for {1}s", state.ClientId, msg.Duration);
                return;
            }
        }

        _clients.TryRemove(remote, out _);
        WriteLog("MQTT-SN client {0} disconnected", remote);
    }

    private void HandleWillTopic(WillTopicMessage msg, IPEndPoint remote)
    {
        if (_clients.TryGetValue(remote, out var state)) state.WillTopic = msg.TopicName;
    }

    private void HandleWillMsg(WillMsgMessage msg, IPEndPoint remote)
    {
        if (_clients.TryGetValue(remote, out var state)) state.WillData = msg.Data;
    }
    #endregion

    #region Helpers
    private void SendAdvertise()
    {
        if (_udpServer == null) return;
        var data = new AdvertiseMessage { GatewayId = GatewayId, Duration = AdvertiseDuration }.Encode();
        _udpServer.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port));
    }

    private async Task SendAsync(MqttSnMessage message, IPEndPoint remote)
    {
        if (_udpServer == null) return;
        var data = message.Encode();
        await _udpServer.SendAsync(data, data.Length, remote).ConfigureAwait(false);
    }

    private String? ResolveTopic(UInt16 topicId, MqttSnTopicIdType type)
    {
        return type switch
        {
            MqttSnTopicIdType.Short => new String(new[] { (Char)(topicId >> 8), (Char)(topicId & 0xFF) }),
            MqttSnTopicIdType.Predefined => TopicRegistry.Lookup(topicId),
            MqttSnTopicIdType.Normal => TopicRegistry.Lookup(topicId),
            _ => TopicRegistry.Lookup(topicId),
        };
    }

    private void WriteLog(String format, params Object?[] args) => Log.Info(format, args);
    #endregion
}

/// <summary>MQTT-SN client state tracked by gateway</summary>
internal class MqttSnClientState
{
    public String ClientId { get; set; } = String.Empty;
    public IPEndPoint? RemoteEndPoint { get; set; }
    public Boolean CleanSession { get; set; } = true;
    public UInt16 KeepAliveSeconds { get; set; }
    public DateTime LastPing { get; set; } = DateTime.UtcNow;
    public String? WillTopic { get; set; }
    public Byte[]? WillData { get; set; }
    public Boolean IsSleeping { get; set; }
    public DateTime SleepUntil { get; set; }
}
