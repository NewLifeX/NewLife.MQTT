using System;
using System.Globalization;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using Xunit;

namespace XUnitTest.Runtime;

/// <summary>运行时本地化测试。验证带前缀日志先翻译消息再拼接前缀，并保持英文兜底</summary>
[Collection("Localization")]
public class RuntimeLocalizationTests
{
    [Fact(DisplayName = "带前缀日志：翻译消息后再拼接实例前缀")]
    public void InfoPrefixed_TranslatesMessageAndKeepsPrefix()
    {
        _ = typeof(MqttClient).Name;

        using var scope = new CultureScope("zh-Hans");
        var log = new CaptureLog();

        log.InfoPrefixed("[MqttClient]", "Connecting to [{0}]", "tcp://localhost:1883");

        Assert.Equal("[MqttClient]正在连接[tcp://localhost:1883]", log.Last);
    }

    [Fact(DisplayName = "带前缀日志：客户端实例输出中文前缀日志")]
    public void MqttClient_WriteLog_UsesPrefixAndTranslation()
    {
        using var scope = new CultureScope("zh-Hans");
        var log = new CaptureLog();
        using var client = new MqttClient { Name = "TestClient", Log = log };

        client.WriteLog("Disconnected");

        Assert.Equal("[TestClient]断开连接", log.Last);
    }

    [Fact(DisplayName = "带前缀日志：缺少译文时回退英文")]
    public void InfoPrefixed_FallsBackToEnglish()
    {
        using var scope = new CultureScope("zh-Hans");
        var log = new CaptureLog();

        log.InfoPrefixed("[Test]", "Message without translation {0}", 42);

        Assert.Equal("[Test]Message without translation 42", log.Last);
    }

    [Fact(DisplayName = "带前缀日志：日志语言优先于界面语言")]
    public void InfoPrefixed_UsesLogCultureFirst()
    {
        using var scope = new CultureScope("zh-Hans", "en");
        var log = new CaptureLog();

        log.InfoPrefixed("[Test]", "Connecting to [{0}]", "tcp://localhost:1883");

        Assert.Equal("[Test]Connecting to [tcp://localhost:1883]", log.Last);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo? _culture;
        private readonly CultureInfo? _logCulture;

        public CultureScope(String culture, String? logCulture = null)
        {
            _culture = Localization.Culture;
            _logCulture = Localization.LogCulture;

            Localization.Culture = new CultureInfo(culture);
            Localization.LogCulture = logCulture == null ? null : new CultureInfo(logCulture);
        }

        public void Dispose()
        {
            Localization.Culture = _culture;
            Localization.LogCulture = _logCulture;
        }
    }

    private sealed class CaptureLog : ILog
    {
        public Boolean Enable { get; set; } = true;

        public LogLevel Level { get; set; } = LogLevel.Debug;

        public String? Last { get; private set; }

        public void Write(LogLevel level, String format, params Object?[] args) => Last = args.Length == 0 ? format : String.Format(format, args);

        public void Debug(String format, params Object?[] args) => Write(LogLevel.Debug, format, args);

        public void Info(String format, params Object?[] args) => Write(LogLevel.Info, format, args);

        public void Warn(String format, params Object?[] args) => Write(LogLevel.Warn, format, args);

        public void Error(String format, params Object?[] args) => Write(LogLevel.Error, format, args);

        public void Fatal(String format, params Object?[] args) => Write(LogLevel.Fatal, format, args);
    }
}

/// <summary>本地化测试集合。修改全局语言，禁止与其他集合并行</summary>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection
{
}
