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

        using (new CultureScope("de"))
        {
            var html = (String)method!.Invoke(null, null)!;
            Assert.Contains("lang=\"de\"", html);
            Assert.Contains("Online-Clients", html);
            Assert.Contains("MQTT-Broker-Dashboard", html);
            Assert.Contains("Client-ID", html);
            Assert.DoesNotContain("在线客户端", html);
        }
    }

    [Fact(DisplayName = "程序集元数据：标题与描述按语言翻译")]
    public void AssemblyMetadata_IsTranslated()
    {
        using (new CultureScope("zh-Hans"))
        {
            Assert.Equal("NewLife.MQTT - MQTT 协议库", Localization.Translate("NewLife.MQTT - MQTT Protocol Library"));
            Assert.StartsWith("纯托管 MQTT 3.1/3.1.1/5.0", Localization.Translate("Pure managed MQTT 3.1/3.1.1/5.0 client and server with six transports, clustering, bridging, rule engine, WebHook, ACL and $SYS. Zero third-party dependencies, MIT licensed."));
        }

        using (new CultureScope("de"))
        {
            Assert.Equal("NewLife.MQTT - MQTT-Protokollbibliothek", Localization.Translate("NewLife.MQTT - MQTT Protocol Library"));
        }
    }

    [Fact(DisplayName = "程序集元数据：标题与描述键与词表一致")]
    public void AssemblyMetadata_KeysMatchLocales()
    {
        var asm = typeof(MqttClient).Assembly;
        var title = asm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        var description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

        Assert.Equal("NewLife.MQTT - MQTT Protocol Library", title);
        Assert.False(description.IsNullOrEmpty());

        var entries = ReadLocaleEntries(Path.Combine(FindRepositoryRoot(), "NewLife.MQTT", "Locales", "zh-Hans.yml"));
        Assert.True(entries.ContainsKey(title!), "词表缺少程序集标题键");
        Assert.True(entries.ContainsKey(description!), "词表缺少程序集描述键");
    }

    [Fact(DisplayName = "词表：程序集元数据键覆盖全部 31 种语言")]
    public void MetadataKeys_CoverAllLanguages()
    {
        var dir = Path.Combine(FindRepositoryRoot(), "NewLife.MQTT", "Locales");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "metadata-keys.json")));
        var keys = doc.RootElement.GetProperty("keys").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.NotEmpty(keys);

        foreach (var file in Directory.GetFiles(dir, "*.yml"))
        {
            var entries = ReadLocaleEntries(file);
            foreach (var key in keys)
            {
                Assert.True(entries.TryGetValue(key, out var value), $"{Path.GetFileName(file)} 缺少元数据键：{key}");
                Assert.False(String.Equals(key, value, StringComparison.Ordinal), $"{Path.GetFileName(file)} 元数据键未翻译：{key}");
            }
        }
    }

    [Fact(DisplayName = "词表：所有语言条目均已翻译（技术同形词除外）")]
    public void LocaleEntries_AreTranslated()
    {
        var root = FindRepositoryRoot();
        foreach (var dir in new[] { "NewLife.MQTT/Locales", "NewLife.MQTT.Storage/Locales", "NewLife.MqttServer/Locales" })
        {
            var path = Path.Combine(root, dir);
            var allow = new HashSet<String>(StringComparer.Ordinal);
            var allowPath = Path.Combine(path, "identical.allow.json");
            if (File.Exists(allowPath))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(allowPath));
                foreach (var item in doc.RootElement.GetProperty("keys").EnumerateArray()) allow.Add(item.GetString()!);
            }

            foreach (var file in Directory.GetFiles(path, "*.yml"))
            {
                foreach (var item in ReadLocaleEntries(file))
                {
                    if (allow.Contains(item.Key)) continue;
                    Assert.False(String.Equals(item.Key, item.Value, StringComparison.Ordinal), $"{Path.GetFileName(file)} 未翻译：{item.Key}");
                }
            }
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

    private static IList<String> ReadLocaleKeys(String file) => ReadLocaleEntries(file).Keys.ToList();

    private static Dictionary<String, String> ReadLocaleEntries(String file)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(file))
        {
            var match = Regex.Match(line, "^\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*$");
            if (match.Success)
            {
                var key = match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                var value = match.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\t", "\t");
                map[key] = value;
            }
        }
        return map;
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
