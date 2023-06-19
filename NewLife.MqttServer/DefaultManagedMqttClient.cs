using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Collections;
using NewLife.Log;
using NewLife.MQTT.Messaging;
using NewLife.Net;

namespace NewLife.MqttServer;

/// <summary>
/// 默认简单的向已连接订阅主题的客户端发送即时发布消息的管理通道
/// </summary>
public sealed class DefaultManagedMqttClient
{
    private ConcurrentQueue<PublishMessage> _messageQueue = new ConcurrentQueue<PublishMessage>();

    private ConcurrentDictionary<Int32, INetSession> _clients = new ConcurrentDictionary<Int32, INetSession>();
    private ConcurrentDictionary<Int32, ConcurrentHashSet<String>> _subscriptions = new ConcurrentDictionary<Int32, ConcurrentHashSet<String>>();
    private ManualResetEventSlim _msgArrived = new ManualResetEventSlim(false);

    public DefaultManagedMqttClient()
    {
        XTrace.WriteLine("Create!");
    }

    public void AddClient(INetSession session)
    {
        if (!_clients.TryGetValue(session.ID, out var client))
        {
            _clients.TryAdd(session.ID, session);
        }
    }

    public void RemoveClient(INetSession session)
    {
        _clients.Remove(session.ID);
        _subscriptions.Remove(session.ID);
    }

    public void SubTopic(INetSession session, String topic)
    {
        if (_subscriptions.TryGetValue(session.ID, out var bag))
        {
            bag.TryRemove(topic);
        }
        else
        {
            bag = new ConcurrentHashSet<String>();
            bag.TryAdd(topic);
            _subscriptions.TryAdd(session.ID, bag);
        }
    }

    public void SubTopic(INetSession session, IList<Subscription> subscriptions)
    {
        foreach (var item in subscriptions)
        {
            SubTopic(session, item.TopicFilter);
        }
    }

    public void UnSubTopic(INetSession session, String topic)
    {
        if (_subscriptions.TryGetValue(session.ID, out var bag))
        {
            bag.TryRemove(topic);
        }
    }

    public void UnSubTopic(INetSession session, IList<String> topics)
    {
        foreach (var topic in topics)
        {
            UnSubTopic(session, topic);
        }
    }

    public void Enqueue(PublishMessage item)
    {
        _messageQueue.Enqueue(item);
        _msgArrived?.Set();
    }

    public PublishMessage Dequeue(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_messageQueue.TryDequeue(out var message))
            {
                return message;
            }
        }
        throw new OperationCanceledException();
    }

    public PublishMessage PeekAndWait(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_messageQueue.TryDequeue(out var message))
            {
                return message;
            }
            // _gate?.Wait(cancellationToken);
        }

        throw new OperationCanceledException();
    }

    private async Task MaintainConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_messageQueue.TryDequeue(out var message))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    //向消费者发送消息
                    foreach (var item in _subscriptions)
                    {
                        if (item.Value.Contains(message.Topic))
                        {
                            if (_clients.TryGetValue(item.Key, out var client))
                            {
                                client.SendMessage(message);
                            }
                        }
                    }
                }
                _msgArrived?.Reset();
                _msgArrived?.Wait(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            XTrace.WriteException(exception);
        }
        finally
        {
            _subscriptions.Clear();
            _clients.Clear();
            _messageQueue.Clear();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var task = Task.Run(() => MaintainConnectionAsync(cancellationToken), cancellationToken);

        task?.ContinueWith(t =>
        {
            if (t.Exception != null)
                XTrace.WriteException(t.Exception);
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task StopAsync(Boolean cleanDisconnect = true)
    {
        _subscriptions.Clear();
        _clients.Clear();
        _messageQueue.Clear();
    }
}