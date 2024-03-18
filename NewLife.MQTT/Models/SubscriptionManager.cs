namespace NewLife.MQTT.Models;

/// <summary>使用哈希表存储订阅者信息</summary>
/// <remarks>
/// <code>
/// Trie trie = new Trie();
/// SubscriptionManager manager = new SubscriptionManager();
/// 
/// // 订阅者订阅主题
/// manager.Subscribe("sports/football", "subscriber1");
/// manager.Subscribe("sports/basketball", "subscriber2");
/// manager.Subscribe("news", "subscriber3");
/// 
/// // 插入订阅主题到前缀树
/// trie.Insert("sports/football", "subscriber1");
/// trie.Insert("sports/basketball", "subscriber2");
/// trie.Insert("news", "subscriber3");
/// // 匹配发布消息
/// List<string> matchedSubscribers = manager.MatchSubscribers("sports/football/match");
/// Console.WriteLine("Matched subscribers: " + string.Join(", ", matchedSubscribers));
/// // 使用前缀树匹配
/// List<string> matchedSubscribersTrie = trie.Match("sports/football/match");
/// Console.WriteLine("Matched subscribers using Trie: " + string.Join(", ", matchedSubscribersTrie));
/// </code>
/// </remarks>
public class SubscriptionManager
{
    private readonly Dictionary<String, List<String>> subscriptions;

    public SubscriptionManager() => subscriptions = [];

    // 订阅主题
    public void Subscribe(String topic, String subscriber)
    {
        if (!subscriptions.ContainsKey(topic))
        {
            subscriptions[topic] = [];
        }
        subscriptions[topic].Add(subscriber);
    }

    // 发布消息匹配订阅者
    public List<String> MatchSubscribers(String topic)
    {
        List<String> matchedSubscribers = [];
        foreach (var kvp in subscriptions)
        {
            // 可以根据实际需求修改匹配逻辑
            if (topic.StartsWith(kvp.Key))
            {
                matchedSubscribers.AddRange(kvp.Value);
            }
        }
        return matchedSubscribers;
    }
}