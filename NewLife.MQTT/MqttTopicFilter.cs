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
    public static Boolean Matches(String topicName, String topicFilter)
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
}
