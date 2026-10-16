using BenchmarkDotNet.Attributes;
using NewLife.MQTT;

namespace Benchmark.MqttMessageBenchmarks;

/// <summary>MQTT 主题匹配性能基准测试。对比旧版 Split 实现与新版 Span 零分配实现的延迟与内存分配</summary>
[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class MqttTopicFilterBenchmark
{
    #region 场景数据
    private String _topicLiteral = null!;
    private String _filterLiteral = null!;
    private String _topicSingleWildcard = null!;
    private String _filterSingleWildcard = null!;
    private String _filterMultiWildcard = null!;
    private String _topicDeep = null!;
    private String _filterDeepNoMatch = null!;
    private String _filterShared = null!;
    #endregion

    [GlobalSetup]
    public void Setup()
    {
        // 最热场景：字面量精确匹配（大量设备的固定主题）
        _topicLiteral = "sensor/temperature/device001";
        _filterLiteral = "sensor/temperature/device001";

        // 单层通配符
        _topicSingleWildcard = "sensor/temperature/device001";
        _filterSingleWildcard = "sensor/+/device001";

        // 多层通配符
        _filterMultiWildcard = "sensor/#";

        // 深层主题的不匹配场景
        _topicDeep = "a/b/c/d/e/f/g";
        _filterDeepNoMatch = "a/b/c/x/e/f/g";

        // 共享订阅前缀
        _filterShared = "$share/group1/sensor/+/device001";
    }

    #region 字面量精确匹配
    [Benchmark(Baseline = true, OperationsPerInvoke = 10000, Description = "旧实现：字面量精确匹配")]
    public Int32 Legacy_Literal()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (LegacyTopicMatcher.IsMatch(_topicLiteral, _filterLiteral)) count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 10000, Description = "新实现：字面量精确匹配")]
    public Int32 Current_Literal()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (MqttTopicFilter.IsMatch(_topicLiteral, _filterLiteral)) count++;
        }
        return count;
    }
    #endregion

    #region 单层通配符 +
    [Benchmark(OperationsPerInvoke = 10000, Description = "旧实现：单层通配符 +")]
    public Int32 Legacy_SingleWildcard()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (LegacyTopicMatcher.IsMatch(_topicSingleWildcard, _filterSingleWildcard)) count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 10000, Description = "新实现：单层通配符 +")]
    public Int32 Current_SingleWildcard()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (MqttTopicFilter.IsMatch(_topicSingleWildcard, _filterSingleWildcard)) count++;
        }
        return count;
    }
    #endregion

    #region 多层通配符 #
    [Benchmark(OperationsPerInvoke = 10000, Description = "旧实现：多层通配符 #")]
    public Int32 Legacy_MultiWildcard()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (LegacyTopicMatcher.IsMatch(_topicSingleWildcard, _filterMultiWildcard)) count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 10000, Description = "新实现：多层通配符 #")]
    public Int32 Current_MultiWildcard()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (MqttTopicFilter.IsMatch(_topicSingleWildcard, _filterMultiWildcard)) count++;
        }
        return count;
    }
    #endregion

    #region 深层主题不匹配
    [Benchmark(OperationsPerInvoke = 10000, Description = "旧实现：深层主题不匹配")]
    public Int32 Legacy_DeepNoMatch()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (LegacyTopicMatcher.IsMatch(_topicDeep, _filterDeepNoMatch)) count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 10000, Description = "新实现：深层主题不匹配")]
    public Int32 Current_DeepNoMatch()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (MqttTopicFilter.IsMatch(_topicDeep, _filterDeepNoMatch)) count++;
        }
        return count;
    }
    #endregion

    #region 共享订阅
    [Benchmark(OperationsPerInvoke = 10000, Description = "旧实现：共享订阅匹配")]
    public Int32 Legacy_Shared()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (LegacyTopicMatcher.IsMatch(_topicSingleWildcard, _filterShared)) count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 10000, Description = "新实现：共享订阅匹配")]
    public Int32 Current_Shared()
    {
        var count = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (MqttTopicFilter.IsMatch(_topicSingleWildcard, _filterShared)) count++;
        }
        return count;
    }
    #endregion
}
