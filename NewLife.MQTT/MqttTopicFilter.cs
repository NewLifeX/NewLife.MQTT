namespace NewLife.MQTT;

/// <summary>
/// 主题过滤匹配
/// </summary>
public class MqttTopicFilter
{
    /// <summary>
    /// 提取实际的主题过滤器，处理共享订阅格式
    /// </summary>
    /// <param name="topicFilter">订阅主题名称，可能包含 $share/{group}/ 前缀</param>
    /// <returns>实际的主题过滤器</returns>
    public static String ExtractActualTopicFilter(String topicFilter)
    {
        if (String.IsNullOrEmpty(topicFilter))
            return topicFilter;
        
        // 检查是否是共享订阅格式: $share/{group}/{topic}
        if (topicFilter.StartsWith("$share/", StringComparison.Ordinal))
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

        // 提取实际的主题过滤器（处理共享订阅）
        var actualTopicFilter = ExtractActualTopicFilter(topicFilter);

        var topicFilterParts = actualTopicFilter.Split('/');

        if (topicFilterParts.Count(s => s == "#") > 1)
            return false;

        if (topicFilterParts.Any(s => s.Length > 1 && s.Contains("#")))
            return false;

        if (topicFilterParts.Any(s => s.Length > 1 && s.Contains("+")))
            return false;

        return !topicFilterParts.Any(s => s == "#") || actualTopicFilter.IndexOf("#") >= actualTopicFilter.Length - 1;
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
        // 处理共享订阅格式: $share/{group}/{topic}
        // 提取实际的主题过滤器
        var actualTopicFilter = ExtractActualTopicFilter(topicFilter);

        // 先做轻量校验，避免在热路径中重复 Split
        if (String.IsNullOrEmpty(topicName) || String.IsNullOrEmpty(actualTopicFilter)) return false;
        if (topicName.Length > 65536) return false;

        // 发布主题不能包含通配符
        if (topicName.Contains('#') || topicName.Contains('+')) return false;

        // 快速通道：过滤器不含通配符时等价于恒等匹配，避免分段开销
        if (!actualTopicFilter.Contains('#') && !actualTopicFilter.Contains('+'))
            return String.Equals(topicName, actualTopicFilter, StringComparison.Ordinal);

        return IsMatchCore(topicName.AsSpan(), actualTopicFilter.AsSpan());
    }

    /// <summary>基于 Span 的零分配主题匹配</summary>
    /// <remarks>匹配语义与 MQTT 规范一致：+ 匹配单层，# 匹配剩余多层（含零层），通配符开头不匹配 $ 主题</remarks>
    /// <param name="topic">发布主题</param>
    /// <param name="filter">主题过滤器（已提取共享订阅前缀，且含 + / # 通配符）</param>
    /// <returns></returns>
    private static Boolean IsMatchCore(ReadOnlySpan<Char> topic, ReadOnlySpan<Char> filter)
    {
        // 第一遍：校验过滤器结构，统计段数与 # 所在位置
        var filterSegs = 0;
        var hashIndex = -1;
        for (var start = 0; ;)
        {
            var slash = filter[start..].IndexOf('/');
            var end = slash < 0 ? filter.Length : start + slash;
            var seg = filter[start..end];

            if (seg.Length == 1 && seg[0] == '#')
            {
                // 多个 # 非法
                if (hashIndex >= 0) return false;
                hashIndex = filterSegs;
            }
            else
            {
                // # 或 + 与其他字符混用非法
                if (seg.IndexOf('#') >= 0) return false;
                if (seg.Length > 1 && seg.IndexOf('+') >= 0) return false;
            }

            filterSegs++;
            if (end >= filter.Length) break;
            start = end + 1;
        }

        // # 必须位于末尾
        if (hashIndex >= 0 && hashIndex != filterSegs - 1) return false;

        // 第二遍：统计主题段数
        var topicSegs = 1;
        for (var i = 0; i < topic.Length; i++)
        {
            if (topic[i] == '/') topicSegs++;
        }

        var lastIsHash = hashIndex >= 0 && hashIndex == filterSegs - 1;
        if (topicSegs > filterSegs && !lastIsHash) return false;
        if (filterSegs - topicSegs > 1) return false;
        if (filterSegs - topicSegs == 1 && !lastIsHash) return false;

        // 以通配符开头的过滤器不匹配 $ 开头的主题
        if (topic[0] == '$' && (filter[0] == '#' || filter[0] == '+')) return false;

        // 第三遍：逐段匹配
        var tleft = topicSegs;
        var tpos = 0;
        for (var fpos = 0; fpos < filter.Length;)
        {
            var slash = filter[fpos..].IndexOf('/');
            var fend = slash < 0 ? filter.Length : fpos + slash;
            var fseg = filter[fpos..fend];

            // # 匹配剩余全部（含零段）
            if (fseg.Length == 1 && fseg[0] == '#') return true;

            if (tleft == 0) return false;

            if (fseg.Length == 1 && fseg[0] == '+')
            {
                // 单层通配符消耗一个主题段
                var tslash = topic[tpos..].IndexOf('/');
                tpos = tslash < 0 ? topic.Length + 1 : tpos + tslash + 1;
            }
            else
            {
                // 字面量段与对应主题段逐字符比较
                var tslash = topic[tpos..].IndexOf('/');
                var tend = tslash < 0 ? topic.Length : tpos + tslash;
                if (!fseg.SequenceEqual(topic[tpos..tend])) return false;
                tpos = tend + 1;
            }

            tleft--;
            fpos = fend + 1;
        }

        return true;
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
