using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Routing;

/// <summary>MqttExchange 持久会话与保留消息统计测试。验证修复 D2（DeletePersistentSession 不递减保留消息数）与 P2（sessionId 反向索引）</summary>
public class MqttExchangePersistentSessionTests : IDisposable
{
    private readonly MqttExchange _exchange;

    public MqttExchangePersistentSessionTests()
    {
        _exchange = new MqttExchange { EnableSysTopic = false };
    }

    public void Dispose() => _exchange.TryDispose();

    #region D2 保留消息计数
    [Fact]
    [System.ComponentModel.DisplayName("保留消息：Publish 存储保留消息后计数正确")]
    public void Publish_RetainMessage_IncrementsCount()
    {
        var msg = new PublishMessage
        {
            Topic = "test/retain",
            Payload = new ArrayPacket("hello".GetBytes()),
            Retain = true,
        };

        _exchange.Publish(msg);

        Assert.Equal(1, _exchange.Stats.RetainedMessages);
        Assert.Contains("test/retain", _exchange.GetRetainedTopics());
    }

    [Fact]
    [System.ComponentModel.DisplayName("保留消息：空 payload 清除保留消息并递减计数")]
    public void Publish_EmptyPayload_ClearsRetain()
    {
        var msg = new PublishMessage
        {
            Topic = "test/retain",
            Payload = new ArrayPacket("hello".GetBytes()),
            Retain = true,
        };
        _exchange.Publish(msg);
        Assert.Equal(1, _exchange.Stats.RetainedMessages);

        // 空 payload 清除
        _exchange.Publish(new PublishMessage { Topic = "test/retain", Retain = true, Payload = ArrayPacket.Empty });
        Assert.Equal(0, _exchange.Stats.RetainedMessages);
    }

    [Fact]
    [System.ComponentModel.DisplayName("D2 修复：删除持久会话不影响保留消息计数")]
    public void DeletePersistentSession_DoesNotAffectRetainedMessages()
    {
        // 存储一条保留消息
        var msg = new PublishMessage
        {
            Topic = "test/retain",
            Payload = new ArrayPacket("hello".GetBytes()),
            Retain = true,
        };
        _exchange.Publish(msg);
        Assert.Equal(1, _exchange.Stats.RetainedMessages);

        // 保存并删除持久会话
        _exchange.SavePersistentSession("client-a", 101, null);
        _exchange.DeletePersistentSession("client-a");

        // 保留消息计数不受影响（修复前会递减为 0）
        Assert.Equal(1, _exchange.Stats.RetainedMessages);
        Assert.Contains("test/retain", _exchange.GetRetainedTopics());
    }
    #endregion

    #region P2 持久会话反向索引
    [Fact]
    [System.ComponentModel.DisplayName("P2 优化：离线持久会话按 sessionId O(1) 定位并暂存消息")]
    public void Publish_OfflinePersistentSession_EnqueuesOfflineMessage()
    {
        // 注册一个离线持久会话（不 Add 到 _sessions），并建立订阅关系
        _exchange.SavePersistentSession("client-offline", 202, new Dictionary<String, QualityOfService>
        {
            ["test/#"] = QualityOfService.AtLeastOnce,
        });
        _exchange.Subscribe(202, "test/#", QualityOfService.AtLeastOnce);

        // 发布匹配消息
        var msg = new PublishMessage
        {
            Topic = "test/data",
            Payload = new ArrayPacket("payload".GetBytes()),
            QoS = QualityOfService.AtLeastOnce,
        };
        _exchange.Publish(msg, -1);

        // 离线消息已入队（通过反向索引找到）
        var info = _exchange.GetPersistentSessionInfo("client-offline");
        Assert.NotNull(info);
        Assert.Single(info!.OfflineMessages);
    }

    [Fact]
    [System.ComponentModel.DisplayName("P2 优化：清除持久会话后反向索引同步清理")]
    public void ClearPersistentSession_RemovesReverseIndex()
    {
        _exchange.SavePersistentSession("client-b", 303, null);

        // 恢复会话后索引更新为新 sessionId
        _exchange.RestorePersistentSession("client-b", 404);
        _exchange.ClearPersistentSession("client-b");

        // 删除后不再能定位
        Assert.Null(_exchange.GetPersistentSessionInfo("client-b"));
    }

    [Fact]
    [System.ComponentModel.DisplayName("P2 优化：会话恢复后新 sessionId 可定位离线消息")]
    public void RestorePersistentSession_NewSessionId_Resolves()
    {
        _exchange.SavePersistentSession("client-c", 505, new Dictionary<String, QualityOfService>
        {
            ["test/#"] = QualityOfService.AtLeastOnce,
        });

        // 模拟重连：sessionId 变化
        Assert.True(_exchange.RestorePersistentSession("client-c", 606));

        // 用新 sessionId 发布
        var msg = new PublishMessage
        {
            Topic = "test/data",
            Payload = new ArrayPacket("x".GetBytes()),
            QoS = QualityOfService.AtLeastOnce,
        };
        _exchange.Publish(msg, 606);

        var info = _exchange.GetPersistentSessionInfo("client-c");
        Assert.NotNull(info);
        Assert.Single(info!.OfflineMessages);
    }
    #endregion
}
