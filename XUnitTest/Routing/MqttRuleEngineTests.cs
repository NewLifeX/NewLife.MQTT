using System;
using System.Collections.Generic;
using NewLife.Data;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttRuleEngine 规则引擎测试</summary>
public class MqttRuleEngineTests
{
    [Fact]
    public void MatchingRuleTriggersRepublish()
    {
        var engine = new MqttRuleEngine();
        var republished = false;

        var exchange = new TestExchange();
        exchange.OnPublishCalled = _ => republished = true;
        engine.Exchange = exchange;

        engine.AddRule(new MqttRule
        {
            Name = "test_rule",
            TopicFilter = "sensor/#",
            ActionType = RuleActionType.Republish,
            TargetTopic = "archive/sensor",
        });

        var msg = new PublishMessage
        {
            Topic = "sensor/temp",
            Payload = new ArrayPacket("25.5"u8.ToArray()),
        };

        var result = engine.ProcessMessage(msg, "client1");

        Assert.True(result);
        Assert.True(republished);
    }

    [Fact]
    public void DropRulePreventsDelivery()
    {
        var engine = new MqttRuleEngine();

        engine.AddRule(new MqttRule
        {
            Name = "drop_rule",
            TopicFilter = "spam/#",
            ActionType = RuleActionType.Drop,
        });

        var msg = new PublishMessage { Topic = "spam/ads" };

        var result = engine.ProcessMessage(msg);

        Assert.False(result);
    }

    [Fact]
    public void NonMatchingRuleDoesNotTrigger()
    {
        var engine = new MqttRuleEngine();
        var triggered = false;

        engine.AddRule(new MqttRule
        {
            Name = "specific_rule",
            TopicFilter = "device/+/alarm",
            ActionType = RuleActionType.Custom,
            CustomAction = _ => triggered = true,
        });

        var msg = new PublishMessage { Topic = "device/1/status" };

        engine.ProcessMessage(msg);

        Assert.False(triggered);
    }

    [Fact]
    public void DisabledRuleIsSkipped()
    {
        var engine = new MqttRuleEngine();
        var triggered = false;

        engine.AddRule(new MqttRule
        {
            Name = "disabled_rule",
            TopicFilter = "#",
            ActionType = RuleActionType.Custom,
            CustomAction = _ => triggered = true,
            Enabled = false,
        });

        var msg = new PublishMessage { Topic = "any/topic" };

        engine.ProcessMessage(msg);

        Assert.False(triggered);
    }

    [Fact]
    public void HitCountIncrements()
    {
        var engine = new MqttRuleEngine();
        var rule = new MqttRule
        {
            Name = "counter_rule",
            TopicFilter = "#",
            ActionType = RuleActionType.Custom,
            CustomAction = _ => { },
        };
        engine.AddRule(rule);

        var msg = new PublishMessage { Topic = "test" };

        engine.ProcessMessage(msg);
        engine.ProcessMessage(msg);
        engine.ProcessMessage(msg);

        Assert.Equal(3, rule.HitCount);
    }

    [Fact]
    public void MultipleRulesExecuteInOrder()
    {
        var engine = new MqttRuleEngine();
        var order = new System.Collections.Generic.List<String>();

        engine.AddRule(new MqttRule
        {
            Name = "first",
            TopicFilter = "#",
            ActionType = RuleActionType.Custom,
            CustomAction = _ => order.Add("first"),
        });

        engine.AddRule(new MqttRule
        {
            Name = "second",
            TopicFilter = "#",
            ActionType = RuleActionType.Custom,
            CustomAction = _ => order.Add("second"),
        });

        var msg = new PublishMessage { Topic = "test" };

        engine.ProcessMessage(msg);

        Assert.Equal(2, order.Count);
        Assert.Equal("first", order[0]);
        Assert.Equal("second", order[1]);
    }

    /// <summary>测试用交换机</summary>
    private class TestExchange : IMqttExchange
    {
        public Action<PublishMessage>? OnPublishCalled { get; set; }
        public MqttStats Stats { get; } = new();

        public Boolean Add(Int32 sessionId, IMqttHandler session) => true;
        public Boolean TryGetValue(Int32 sessionId, out IMqttHandler session) { session = null!; return false; }
        public Boolean Remove(Int32 sessionId) => true;

        public void Publish(PublishMessage message) => OnPublishCalled?.Invoke(message);
        public void Subscribe(Int32 sessionId, String topic, QualityOfService qos) { }
        public void Unsubscribe(Int32 sessionId, String topic) { }
        public IList<PublishMessage> GetRetainMessages(String topicFilter) => [];

        public void SavePersistentSession(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions) { }
        public Boolean RestorePersistentSession(String clientId, Int32 sessionId) => false;
        public void ClearPersistentSession(String clientId) { }
        public void EnqueueOfflineMessage(String clientId, PublishMessage message) { }

        public IList<String> GetClientIds() => [];
        public IList<String> GetTopics() => [];
        public Int32 GetSubscriberCount(String topic) => 0;
    }
}
