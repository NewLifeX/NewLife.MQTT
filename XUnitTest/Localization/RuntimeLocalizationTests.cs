using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.MqttSn;
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

    [Fact(DisplayName = "本地化：zh-CN 自动映射到简体中文")]
    public void Translate_MapsChineseRegionToSimplified()
    {
        using var scope = new CultureScope("zh-CN");

        Assert.Equal("正在连接[tcp://localhost:1883]", Localization.Translate("Connecting to [{0}]", "tcp://localhost:1883"));
    }

    [Fact(DisplayName = "本地化：格式说明符保持有效")]
    public void Translate_KeepsFormatSpecifier()
    {
        using var scope = new CultureScope("zh-Hans");

        Assert.Equal("SOCKS5 代理要求认证方式 0x02", Localization.Translate("SOCKS5 proxy requires auth method 0x{0:X2}", 2));
    }

    [Fact(DisplayName = "MQTT-SN：未设置网关时抛出本地化异常")]
    public async Task MqttSnClient_ConnectAsync_ThrowsLocalizedMessage()
    {
        using var scope = new CultureScope("zh-Hans");
        using var client = new MqttSnClient();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync());

        Assert.Equal("未设置网关，请先调用 SearchGatewayAsync", ex.Message);
    }

    [Fact(DisplayName = "消息工厂：不支持的类型抛出本地化异常")]
    public void MqttFactory_UnsupportedType_ThrowsLocalizedMessage()
    {
        using var scope = new CultureScope("zh-Hans");

        var ex = Assert.Throws<NotSupportedException>(() => new MqttFactory().CreateMessage((MqttType)0));

        Assert.Equal("不支持的消息类型：0", ex.Message);
    }

    [Fact(DisplayName = "看板：HTML 按当前语言渲染")]
    public void DashboardHtml_UsesCurrentCulture()
    {
        var method = typeof(MqttDashboard).GetMethod("GetDashboardHtml", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using (new CultureScope("zh-Hans"))
        {
            var html = (String)method!.Invoke(null, null)!;
            Assert.Contains("lang=\"zh-Hans\"", html);
            Assert.Contains("在线客户端", html);
            Assert.Contains("MQTT Broker 看板", html);
            Assert.Contains("客户端 ID", html);
            Assert.DoesNotContain("Online clients", html);
        }

        using (new CultureScope("en"))
        {
            var html = (String)method!.Invoke(null, null)!;
            Assert.Contains("lang=\"en\"", html);
            Assert.Contains("Online clients", html);
            Assert.Contains("MQTT Broker Dashboard", html);
            Assert.Contains("Client ID", html);
            Assert.DoesNotContain("在线客户端", html);
        }
    }

    [Fact(DisplayName = "词表：所有语言键集合与基准一致")]
    public void LocaleFiles_HaveSameKeys()
    {
        var root = FindRepositoryRoot();
        foreach (var dir in new[] { "NewLife.MQTT/Locales", "NewLife.MQTT.Storage/Locales", "NewLife.MqttServer/Locales" })
        {
            var path = Path.Combine(root, dir);
            var baseline = ReadLocaleKeys(Path.Combine(path, "zh-Hans.yml"));
            foreach (var file in Directory.GetFiles(path, "*.yml"))
            {
                var keys = ReadLocaleKeys(file);
                var missing = baseline.Except(keys).ToList();
                var extra = keys.Except(baseline).ToList();
                Assert.True(missing.Count == 0, $"{Path.GetFileName(file)} 缺少键：{String.Join(", ", missing)}");
                Assert.True(extra.Count == 0, $"{Path.GetFileName(file)} 多余键：{String.Join(", ", extra)}");
            }
        }
    }

    private static String FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NewLife.MQTT.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static IList<String> ReadLocaleKeys(String file)
    {
        var list = new List<String>();
        foreach (var line in File.ReadAllLines(file))
        {
            var match = Regex.Match(line, "^\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:");
            if (match.Success) list.Add(match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\"));
        }
        return list;
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
