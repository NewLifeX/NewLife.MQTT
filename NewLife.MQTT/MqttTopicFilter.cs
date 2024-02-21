namespace NewLife.MQTT;

/// <summary>
/// 主题过滤匹配
/// </summary>
public class MqttTopicFilter
{
    /// <summary>
    /// 订阅主题过滤
    /// </summary>
    /// <param name="topicFilter">订阅主题名称</param>
    /// <returns></returns>
    public static Boolean IsValidTopicFilter(String topicFilter)
    {
        if (String.IsNullOrEmpty(topicFilter))
            return false;

        if (topicFilter.Length > 65536)
            return false;

        var topicFilterParts = topicFilter.Split('/');

        if (topicFilterParts.Count(s => s == "#") > 1)
            return false;

        if (topicFilterParts.Any(s => s.Length > 1 && s.Contains("#")))
            return false;

        if (topicFilterParts.Any(s => s.Length > 1 && s.Contains("+")))
            return false;

        return !topicFilterParts.Any(s => s == "#") || topicFilter.IndexOf("#") >= topicFilter.Length - 1;
    }

    /// <summary>
    /// 发布主题验证
    /// </summary>
    /// <param name="topicName"></param>
    /// <returns></returns>
    public static Boolean IsValidTopicName(String topicName)
    {
        return !String.IsNullOrEmpty(topicName) &&
            topicName.Length <= 65536 &&
            !topicName.Contains("#") &&
            !topicName.Contains("+");
    }

    /// <summary>
    ///匹配发布主题和订阅主题是否匹配
    /// </summary>
    /// <param name="topicName">发布主题名称</param>
    /// <param name="topicFilter">订阅主题名称</param>
    /// <returns></returns>
    public static Boolean IsMatch(String topicName, String topicFilter)
    {
        if (!IsValidTopicName(topicName)) throw new Exception($"{topicName}:发布主题不符合规范");
        if (!IsValidTopicFilter(topicFilter)) throw new Exception($"{topicName}:订阅主题不符合规范");

        var topicFilterParts = topicFilter.Split('/');
        var topicNameParts = topicName.Split('/');

        if (topicNameParts.Length > topicFilterParts.Length && topicFilterParts[^1] != "#")
            return false;

        if (topicFilterParts.Length - topicNameParts.Length > 1)
            return false;

        if (topicFilterParts.Length - topicNameParts.Length == 1 && topicFilterParts[^1] != "#")
            return false;

        if ((topicFilterParts[0] == "#" || topicFilterParts[0] == "+") && topicNameParts[0].StartsWith("$"))
            return false;

        var matches = true;

        for (var i = 0; i < topicFilterParts.Length; i++)
        {
            var topicFilterPart = topicFilterParts[i];

            if (topicFilterPart == "#")
            {
                matches = true;
                break;
            }

            if (topicFilterPart == "+")
            {
                if (i == topicFilterParts.Length - 1 && topicNameParts.Length > topicFilterParts.Length)
                {
                    matches = false;
                    break;
                }

                continue;
            }

            if (topicFilterPart != topicNameParts[i])
            {
                matches = false;
                break;
            }
        }
        return matches;
    }

    /// <summary>扩展主题，得到所有通配符组合</summary>
    /// <remarks>
    /// 在分布式MQTT集群中，发布主题时，需要将主题扩展为带有通配符的所有组合，然后逐个转发消息。
    /// 可使用Redis的GetAll来获取各个通配符组合的订阅情况。
    /// </remarks>
    /// <param name="topic"></param>
    /// <returns></returns>
    public static IList<String> Expand(String topic)
    {
        // 去掉首尾/
        topic = topic.Trim('/');

        // 结果集合
        var ts = new List<String>();

        // 分割主题
        var ss = topic.Split('/');

        // 多级主题，递归处理
        Expand(ss, 0, "", ts);

        return ts;
    }

    private static void Expand(String[] ss, Int32 index, String topic, List<String> ts)
    {
        if (index >= ss.Length) return;

        /*
         * 到了最后一级时，加入结果集合。
         * +和#不能同时出现
         * 末尾不能是两个+，因为可以#替代
         */

        // 本级常规
        {
            if (index == ss.Length - 1)
                ts.Add(topic + "/" + ss[index]);

            Expand(ss, index + 1, topic + "/" + ss[index], ts);
        }

        // 单层通配符+
        if (!topic.Contains('#'))
        {
            if (index == ss.Length - 1 && topic[^1] != '+')
                ts.Add(topic + "/+");

            Expand(ss, index + 1, topic + "/+", ts);
        }

        // 多层通配符#
        if (!topic.Contains('+'))
        {
            ts.Add(topic + "/#");
        }
    }
}
