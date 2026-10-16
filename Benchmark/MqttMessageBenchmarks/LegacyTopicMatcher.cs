namespace Benchmark.MqttMessageBenchmarks;

/// <summary>旧版主题匹配实现（Split 分割）。仅用于基准测试对比，验证优化收益</summary>
internal static class LegacyTopicMatcher
{
    /// <summary>匹配发布主题和订阅主题是否匹配（优化前版本）</summary>
    /// <param name="topicName">发布主题名称</param>
    /// <param name="topicFilter">订阅主题名称</param>
    /// <returns></returns>
    public static Boolean IsMatch(String topicName, String topicFilter)
    {
        // 处理共享订阅格式: $share/{group}/{topic}
        var actualTopicFilter = ExtractActualTopicFilter(topicFilter);

        if (String.IsNullOrEmpty(topicName) || String.IsNullOrEmpty(actualTopicFilter)) return false;
        if (topicName.Length > 65536) return false;

        if (topicName.Contains('#') || topicName.Contains('+')) return false;

        // 拆分一次即可，供后续匹配使用
        var topicFilterParts = actualTopicFilter.Split('/');
        var topicNameParts = topicName.Split('/');

        // 订阅主题过滤器非法：多个 #、# 或 + 与其他字符混用、# 不在末尾
        var hashCount = 0;
        for (var i = 0; i < topicFilterParts.Length; i++)
        {
            var part = topicFilterParts[i];
            if (part == "#") hashCount++;
            else if (part.Contains('#')) return false;
            else if (part.Contains('+') && part.Length > 1) return false;
        }
        if (hashCount > 1) return false;
        if (hashCount == 1 && !actualTopicFilter.EndsWith("#")) return false;

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

    private static String ExtractActualTopicFilter(String topicFilter)
    {
        if (String.IsNullOrEmpty(topicFilter))
            return topicFilter;

        // 检查是否是共享订阅格式: $share/{group}/{topic}
        if (topicFilter.StartsWith("$share/"))
        {
            // 跳过 $share/ 和组名，找到第二个 / 之后的内容
            var firstSlash = topicFilter.IndexOf('/', 7); // 7 是 "$share/" 的长度
            if (firstSlash > 0 && firstSlash < topicFilter.Length - 1)
            {
                return topicFilter.Substring(firstSlash + 1);
            }
        }

        return topicFilter;
    }
}
