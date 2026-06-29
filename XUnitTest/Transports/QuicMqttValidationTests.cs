#if NET7_0_OR_GREATER
using System;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>MQTT over QUIC 快速验证测试（不依赖 QUIC 平台运行时）</summary>
/// <remarks>
/// 端到端 QUIC 连接测试需要 Windows 11 或 Linux with msquic 环境，
/// 不适合作为自动化单元测试运行。本文件仅包含不需要实际 QUIC 连接的验证测试。
/// </remarks>
public class QuicMqttValidationTests
{
    [Fact(DisplayName = "验证_QUIC服务端未配置证书时抛InvalidOperationException")]
    public void Server_WithoutCertificate_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var server = new MqttServer
            {
                QuicPort = 14568,
                ServiceProvider = new ObjectContainer().BuildServiceProvider(),
                Log = XTrace.Log,
            };
            server.Start();
        });

        Assert.Contains("QuicCertificate", ex.Message);
    }

    [Fact(DisplayName = "验证_QuicPort为0时不启动QUIC监听器")]
    public void Server_QuicPortZero_NoListenerStarted()
    {
        using var server = new MqttServer
        {
            Port = 0,
            QuicPort = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        server.Start();

        // QuicPort=0 时不应启动 QUIC 监听器，服务端应正常启动 TCP
        Assert.True(server.Active);
        Assert.Equal(0, server.QuicPort);

        server.Stop("测试完成");
    }

    [Fact(DisplayName = "验证_仅设置QuicPort不设置证书时抛异常")]
    public void Server_QuicPortWithoutCertificate_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var server = new MqttServer
            {
                Port = 0,
                QuicPort = 24567,
                ServiceProvider = new ObjectContainer().BuildServiceProvider(),
                Log = XTrace.Log,
            };
            server.Start();
        });

        Assert.Contains("QuicCertificate", ex.Message);
    }

    [Fact(DisplayName = "验证_MqttServer默认QuicPort为0")]
    public void Server_DefaultQuicPort_IsZero()
    {
        using var server = new MqttServer();

        Assert.Equal(0, server.QuicPort);
    }
}
#endif
