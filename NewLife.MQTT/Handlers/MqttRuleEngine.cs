using NewLife.Log;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>规则动作类型</summary>
public enum RuleActionType
{
    /// <summary>转发到本地主题</summary>
    Republish,

    /// <summary>触发 WebHook</summary>
    WebHook,

    /// <summary>桥接到远端 Broker</summary>
    Bridge,

    /// <summary>丢弃消息</summary>
    Drop,

    /// <summary>自定义委托处理</summary>
    Custom,
}

/// <summary>消息规则。定义主题匹配条件和处理动作</summary>
public class MqttRule
{
    /// <summary>规则名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>源主题过滤器。支持通配符</summary>
    public String TopicFilter { get; set; } = "#";

    /// <summary>动作类型</summary>
    public RuleActionType ActionType { get; set; }

    /// <summary>目标主题（Republish 时使用）</summary>
    public String? TargetTopic { get; set; }

    /// <summary>WebHook URL（WebHook 动作时使用）</summary>
    public String? WebHookUrl { get; set; }

    /// <summary>桥接器名称（Bridge 动作时使用）</summary>
    public String? BridgeName { get; set; }

    /// <summary>自定义处理委托</summary>
    public Action<PublishMessage>? CustomAction { get; set; }

    /// <summary>是否启用。默认true</summary>
    public Boolean Enabled { get; set; } = true;

    /// <summary>匹配次数统计</summary>
    public Int64 HitCount;
}

/// <summary>MQTT 规则引擎。根据配置的规则对消息进行过滤和路由</summary>
/// <remarks>
/// 规则引擎在消息发布时触发，按顺序匹配规则列表中的主题过滤器。
/// 匹配成功后执行对应的动作：转发到其它主题、触发 WebHook、桥接到远端、丢弃、或自定义处理。
/// 一条消息可匹配多条规则，按规则列表顺序依次执行。
/// </remarks>
public class MqttRuleEngine
{
    #region 属性
    /// <summary>规则列表。按顺序匹配</summary>
    public IList<MqttRule> Rules { get; set; } = [];

    /// <summary>本地消息交换机</summary>
    public IMqttExchange? Exchange { get; set; }

    /// <summary>WebHook 处理器</summary>
    public MqttWebHook? WebHook { get; set; }

    /// <summary>桥接器集合。key=桥接名称</summary>
    public IDictionary<String, MqttBridge> Bridges { get; set; } = new Dictionary<String, MqttBridge>();
    #endregion

    #region 方法
    /// <summary>添加规则</summary>
    /// <param name="rule">规则</param>
    public void AddRule(MqttRule rule) => Rules.Add(rule);

    /// <summary>处理消息。遍历规则列表，匹配后执行动作</summary>
    /// <param name="message">发布消息</param>
    /// <param name="clientId">发布者客户端标识</param>
    /// <returns>是否应该继续正常投递（false 表示消息被丢弃）</returns>
    public Boolean ProcessMessage(PublishMessage message, String? clientId = null)
    {
        var shouldDeliver = true;

        foreach (var rule in Rules)
        {
            if (!rule.Enabled) continue;
            if (!MqttTopicFilter.IsMatch(message.Topic, rule.TopicFilter)) continue;

            // 匹配成功，更新统计
            Interlocked.Increment(ref rule.HitCount);

            switch (rule.ActionType)
            {
                case RuleActionType.Republish:
                    OnRepublish(rule, message);
                    break;

                case RuleActionType.WebHook:
                    OnWebHook(rule, message, clientId);
                    break;

                case RuleActionType.Bridge:
                    OnBridge(rule, message);
                    break;

                case RuleActionType.Drop:
                    shouldDeliver = false;
                    break;

                case RuleActionType.Custom:
                    rule.CustomAction?.Invoke(message);
                    break;
            }
        }

        return shouldDeliver;
    }

    /// <summary>转发到本地主题</summary>
    private void OnRepublish(MqttRule rule, PublishMessage message)
    {
        var exchange = Exchange;
        if (exchange == null || rule.TargetTopic.IsNullOrEmpty()) return;

        var msg = new PublishMessage
        {
            Topic = rule.TargetTopic,
            Payload = message.Payload,
            QoS = message.QoS,
            Retain = message.Retain,
        };
        exchange.Publish(msg);
    }

    /// <summary>触发 WebHook</summary>
    private void OnWebHook(MqttRule rule, PublishMessage message, String? clientId)
    {
        var webHook = WebHook;
        if (webHook == null) return;

        webHook.OnMessagePublish(clientId, message);
    }

    /// <summary>桥接到远端</summary>
    private void OnBridge(MqttRule rule, PublishMessage message)
    {
        if (rule.BridgeName.IsNullOrEmpty()) return;
        if (!Bridges.TryGetValue(rule.BridgeName, out var bridge)) return;

        bridge.ForwardToRemote(message);
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion
}
