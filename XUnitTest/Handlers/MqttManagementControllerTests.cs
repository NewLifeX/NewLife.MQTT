using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>MqttManagementController 管理 API 单元测试（直接测试控制器逻辑，无需真实 HTTP）</summary>
public class MqttManagementControllerTests
{
    /// <summary>构造一个具有完整 Exchange 的 ManagementServer 并返回一个注入了该 Server 的控制器</summary>
    private static (MqttManagementServer server, MqttManagementController controller) CreateController(MqttExchange? exchange = null)
    {
        var ex = exchange ?? new MqttExchange();
        var server = new MqttManagementServer
        {
            Exchange = ex,
            Port = 0,
        };
        // 直接注入到控制器（绕过 HTTP 管道）
        var ctrl = new MqttManagementController();
        ctrl.InjectServer(server);
        return (server, ctrl);
    }

    /// <summary>读取 Stats 返回值（字典形态）中的字段。AOT 下匿名类型属性会被裁剪，Stats 以字典承载字段</summary>
    private static Object? GetStat(Object result, String name) => (result as IDictionary<String, Object?>)?[name];

    #region Echo
    [Fact]
    [System.ComponentModel.DisplayName("Management：Echo 返回原始消息")]
    public void Echo_ReturnsInput()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Echo("hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Echo 空字符串原样返回")]
    public void Echo_EmptyString_Returns()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Echo("");

        Assert.Equal("", result);
    }
    #endregion

    #region Stats
    [Fact]
    [System.ComponentModel.DisplayName("Management：Stats 返回包含 ConnectedClients 字段")]
    public void Stats_ContainsConnectedClients()
    {
        var exchange = new MqttExchange();
        exchange.Stats.ConnectedClients = 7;
        var (_, ctrl) = CreateController(exchange);

        var result = ctrl.Stats();

        Assert.NotNull(result);
        // 读取字典字段（键名与字段一致）
        var value = GetStat(result, "ConnectedClients");
        Assert.Equal(7, (Int32)value!);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Stats 返回包含 Version 字段且不为空")]
    public void Stats_VersionFieldNotEmpty()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Stats();

        var version = GetStat(result, "Version") as String;
        Assert.False(String.IsNullOrEmpty(version));
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Stats StartTime 格式 yyyy-MM-dd HH:mm:ss")]
    public void Stats_StartTimeFormatCorrect()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Stats();

        var startTime = GetStat(result, "StartTime") as String;
        Assert.NotNull(startTime);
        // 验证格式
        Assert.True(DateTime.TryParseExact(startTime, "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _),
            $"StartTime 格式不正确: {startTime}");
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Stats Uptime 大于等于 0")]
    public void Stats_UptimeNonNegative()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Stats();

        var uptime = (Int64)GetStat(result, "Uptime")!;
        Assert.True(uptime >= 0);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Stats MessagesReceived 反映 Exchange.Stats")]
    public void Stats_MessagesReceived_ReflectsExchange()
    {
        var exchange = new MqttExchange();
        exchange.Stats.MessagesReceived = 999;
        var (_, ctrl) = CreateController(exchange);

        var result = ctrl.Stats();

        Assert.Equal(999L, (Int64)GetStat(result, "MessagesReceived")!);
    }
    #endregion

    #region Clients
    [Fact]
    [System.ComponentModel.DisplayName("Management：Clients 无连接时返回空列表")]
    public void Clients_NoConnections_ReturnsEmpty()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Clients();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Clients 返回 IReadOnlyList<String>")]
    public void Clients_ReturnsReadOnlyList()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Clients();

        Assert.IsAssignableFrom<IReadOnlyList<String>>(result);
    }
    #endregion

    #region Topics
    [Fact]
    [System.ComponentModel.DisplayName("Management：Topics 无订阅时返回空字典")]
    public void Topics_NoSubscriptions_ReturnsEmpty()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Topics();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Topics 返回 IReadOnlyDictionary<String, Int32>")]
    public void Topics_ReturnsReadOnlyDictionary()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Topics();

        Assert.IsAssignableFrom<IReadOnlyDictionary<String, Int32>>(result);
    }
    #endregion

    #region Retained
    [Fact]
    [System.ComponentModel.DisplayName("Management：Retained 无保留消息时返回空列表")]
    public void Retained_NoRetain_ReturnsEmpty()
    {
        var (_, ctrl) = CreateController();

        var result = ctrl.Retained();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [System.ComponentModel.DisplayName("Management：Retained 发布 Retain 消息后出现在列表中")]
    public void Retained_AfterPublishRetain_TopicAppears()
    {
        var exchange = new MqttExchange();
        // 通过 Exchange.Publish 写入 Retain 消息（公开 API）
        exchange.Publish(new PublishMessage
        {
            Topic = "mgmt/test/retain",
            Payload = new NewLife.Data.ArrayPacket("data"u8.ToArray()),
            QoS = QualityOfService.AtMostOnce,
            Retain = true,
        });

        var (_, ctrl) = CreateController(exchange);

        var result = ctrl.Retained();

        Assert.Contains("mgmt/test/retain", result);
    }
    #endregion
}

/// <summary>测试辅助扩展：向控制器注入服务器引用（绕过 HTTP 管道）</summary>
file static class ControllerTestExtensions
{
    public static void InjectServer(this MqttManagementController ctrl, MqttManagementServer server)
    {
        // 通过私有字段反射注入（仅用于单元测试）
        var field = typeof(MqttManagementController)
            .GetField("_server", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(ctrl, server);
    }
}
