using System.Collections.Concurrent;
using NewLife.MQTT.Messaging;
using NewLife.Threading;

namespace NewLife.MQTT.Handlers;

/// <summary>Inflight消息管理器。管理待确认消息的超时重发</summary>
/// <remarks>
/// MQTT 协议要求 QoS>0 的消息在未收到确认时需要重发。
/// 重发时需要设置 DUP=1 标志，表示这是一条重复消息。
/// </remarks>
public class InflightManager : DisposeBase
{
    #region 属性
    /// <summary>重发超时时间（毫秒）。默认10000ms</summary>
    public Int32 RetryTimeout { get; set; } = 10_000;

    /// <summary>最大重发次数。默认3次</summary>
    public Int32 MaxRetries { get; set; } = 3;

    private readonly ConcurrentDictionary<UInt16, InflightMessage> _messages = new();
    private TimerX? _timer;
    private readonly Func<PublishMessage, Task> _sendAction;
    #endregion

    #region 构造
    /// <summary>实例化Inflight消息管理器</summary>
    /// <param name="sendAction">发送消息的委托</param>
    public InflightManager(Func<PublishMessage, Task> sendAction)
    {
        _sendAction = sendAction ?? throw new ArgumentNullException(nameof(sendAction));
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
        _messages.Clear();
    }
    #endregion

    #region 方法
    /// <summary>添加待确认消息</summary>
    /// <param name="packetId">报文标识</param>
    /// <param name="message">发布消息</param>
    public void Add(UInt16 packetId, PublishMessage message)
    {
        _messages[packetId] = new InflightMessage
        {
            PacketId = packetId,
            Message = message,
            SendTime = DateTime.Now,
        };

        _timer ??= new TimerX(CheckRetry, null, 1000, 1000);
    }

    /// <summary>确认消息（收到ACK/COMP后移除）</summary>
    /// <param name="packetId">报文标识</param>
    /// <returns></returns>
    public Boolean Acknowledge(UInt16 packetId) => _messages.TryRemove(packetId, out _);

    /// <summary>检查超时并重发</summary>
    private void CheckRetry(Object? state)
    {
        var now = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(RetryTimeout);

        foreach (var item in _messages)
        {
            var msg = item.Value;
            if (now - msg.SendTime < timeout) continue;

            if (msg.RetryCount >= MaxRetries)
            {
                // 超过最大重试次数，放弃
                _messages.TryRemove(item.Key, out _);
                continue;
            }

            // 重发消息，设置 DUP=1
            msg.RetryCount++;
            msg.SendTime = now;
            msg.Message.Duplicate = true;

            try
            {
                _sendAction(msg.Message);
            }
            catch
            {
                // 发送失败不影响其他消息重试
            }
        }

        // 如果没有待确认消息，停止定时器
        if (_messages.IsEmpty)
        {
            _timer.TryDispose();
            _timer = null;
        }
    }
    #endregion
}

/// <summary>Inflight消息项</summary>
class InflightMessage
{
    /// <summary>报文标识</summary>
    public UInt16 PacketId { get; set; }

    /// <summary>消息</summary>
    public PublishMessage Message { get; set; } = null!;

    /// <summary>发送时间</summary>
    public DateTime SendTime { get; set; }

    /// <summary>重试次数</summary>
    public Int32 RetryCount { get; set; }
}
