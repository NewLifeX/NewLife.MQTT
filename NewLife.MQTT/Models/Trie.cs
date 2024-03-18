namespace NewLife.MQTT.Models;

/// <summary>前缀树</summary>
/// <remarks>
/// 当涉及到在海量订阅主题中快速匹配当前发布消息的订阅者时，可以使用基于哈希表和前缀树（Trie）的算法来实现。以下是一个简单的算法示例：
/// 1，构建前缀树（Trie）：将所有订阅主题按照层级拆分，构建前缀树，每个节点代表一个主题层级，每个节点的子节点代表下一级主题层级，直到叶子节点代表完整的订阅主题。
/// 2，哈希表存储订阅者信息：使用哈希表来存储每个订阅主题对应的订阅者列表，键为订阅主题，值为订阅者列表。
/// 3，匹配发布消息：当有新的发布消息时，将消息的主题按照层级拆分，然后在前缀树中进行匹配，找到所有匹配的订阅主题。
/// 4，查找订阅者：根据匹配的订阅主题，在哈希表中查找对应的订阅者列表，即为当前发布消息相匹配的订阅者。
/// 
/// 这种算法利用了前缀树的高效匹配能力和哈希表的快速查找特性，能够快速地匹配发布消息和订阅者，提高系统的性能和响应速度。
/// 同时，可以通过优化前缀树的构建和查询算法，以及哈希表的存储和查找方式来进一步提高匹配的速度。
/// </remarks>
public class Trie
{
    private readonly TrieNode root;

    public Trie() => root = new TrieNode();

    // 插入订阅主题
    public void Insert(String topic, String subscriber)
    {
        var node = root;
        foreach (var c in topic)
        {
            if (!node.Children.ContainsKey(c))
            {
                node.Children[c] = new TrieNode();
            }
            node = node.Children[c];
            node.Subscribers.Add(subscriber);
        }
    }

    // 匹配订阅主题
    public List<String> Match(String topic)
    {
        var node = root;
        foreach (var c in topic)
        {
            // 没有匹配的订阅者
            if (!node.Children.ContainsKey(c))
                return [];

            node = node.Children[c];
        }

        // 返回匹配的订阅者
        return node.Subscribers;
    }
}
