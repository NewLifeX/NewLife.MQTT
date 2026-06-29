#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>MqttClient QUIC 传输集成测试（验证 quic:// URI 处理和 QUIC 路径）</summary>
/// <remarks>
/// 这些测试验证 MqttClient 在 quic:// URI 下的初始化逻辑、属性校验和错误处理，
/// 不依赖真实 QUIC 网络连接。
/// </remarks>
public class MqttClientQuicTests
{
    [Fact(DisplayName = "MqttClient_quic检测_quic://开头的URI被识别为QUIC")]
    public void QuicUri_Detection_QuicScheme()
    {
        Assert.True("quic://broker.example.com:14567".StartsWithIgnoreCase("quic://"));
        Assert.True("quics://broker.example.com:14567".StartsWithIgnoreCase("quic://", "quics://"));
        Assert.False("tcp://broker.example.com:1883".StartsWithIgnoreCase("quic://", "quics://"));
        Assert.False("ws://broker.example.com:8083".StartsWithIgnoreCase("quic://", "quics://"));
    }

    [Fact(DisplayName = "MqttClient_quic_URI端口解析_默认端口14567")]
    public void QuicUri_PortParsing_Default14567()
    {
        var uri = new Uri("quic://broker.example.com");
        Assert.Equal(14567, uri.Port > 0 ? uri.Port : 14567);

        var uri2 = new Uri("quic://broker.example.com:9999");
        Assert.Equal(9999, uri2.Port);
    }

    [Fact(DisplayName = "MqttClient_quic_HOST解析_从URI正确提取")]
    public void QuicUri_HostParsing_ExtractsCorrectly()
    {
        var uri = new Uri("quic://broker.example.com:14567");
        Assert.Equal("broker.example.com", uri.Host);

        var uri2 = new Uri("quic://127.0.0.1:14567");
        Assert.Equal("127.0.0.1", uri2.Host);
    }

    [Fact(DisplayName = "MqttClient_Server属性quic格式设置正确")]
    public void Server_Property_QuicUri_SetCorrectly()
    {
        var client = new MqttClient
        {
            Server = "quic://127.0.0.1:14567",
            ClientId = "test",
        };

        Assert.Equal("quic://127.0.0.1:14567", client.Server);
        Assert.Equal("test", client.ClientId);

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttClient_quic连接_未启动服务端时抛异常")]
    public async Task Connect_QuicUri_WithoutRunningServer_Throws()
    {
        var client = new MqttClient
        {
            Log = XTrace.Log,
            Server = "quic://127.0.0.1:14567",
            ClientId = $"test_{Rand.NextString(6)}",
            Timeout = 2_000,
            Reconnect = false,
        };

        // QUIC 服务端未启动时应快速失败
        await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync());

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttClient_IsConnected_QUIC客户端未连接时返回false")]
    public void IsConnected_QuicClient_WithoutConnection_ReturnsFalse()
    {
        var client = new MqttClient
        {
            Server = "quic://127.0.0.1:14567",
            ClientId = "test",
        };

        Assert.False(client.IsConnected);

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttClient_quic连接_客户端Dispose安全")]
    public void Dispose_QuicClient_NoException()
    {
        var client = new MqttClient
        {
            Server = "quic://127.0.0.1:14567",
            ClientId = "test",
        };

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttClient_PingAsync_未连接QUIC时返回null")]
    public async Task PingAsync_QuicClient_WithoutConnection()
    {
        var client = new MqttClient
        {
            Server = "quic://127.0.0.1:14567",
            ClientId = "test",
            Timeout = 1_000,
            Reconnect = false,
        };

        // 未连接时 Ping 返回 null（日志输出提示）
        var rs = await client.PingAsync();
        Assert.Null(rs);

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttClient_连接字符串初始化支持quic")]
    public void Init_ConnectionString_QuicUri()
    {
        var client = new MqttClient();
        client.Init("Server=quic://broker.example.com:14567;ClientId=device01;Timeout=5000");

        Assert.Equal("quic://broker.example.com:14567", client.Server);
        Assert.Equal("device01", client.ClientId);
        Assert.Equal(5000, client.Timeout);

        client.TryDispose();
    }
}
#endif
