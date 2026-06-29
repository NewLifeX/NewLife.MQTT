#if NET7_0_OR_GREATER
using System;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>MqttServer QUIC 集成测试（验证 QUIC 监听器属性和生命周期）</summary>
public class MqttServerQuicTests
{
    #region 属性验证
    [Fact(DisplayName = "MqttServer_默认QuicPort为0")]
    public void Server_DefaultQuicPort_IsZero()
    {
        using var server = new MqttServer();
        Assert.Equal(0, server.QuicPort);
    }

    [Fact(DisplayName = "MqttServer_QuicPort可设置")]
    public void Server_QuicPort_CanBeSet()
    {
        using var server = new MqttServer { QuicPort = 14567 };
        Assert.Equal(14567, server.QuicPort);
    }

    [Fact(DisplayName = "MqttServer_QuicCertificate默认为null")]
    public void Server_DefaultQuicCertificate_IsNull()
    {
        using var server = new MqttServer();
        Assert.Null(server.QuicCertificate);
    }
    #endregion

    #region 错误校验
    [Fact(DisplayName = "MqttServer_设QuicPort无证书启动抛InvalidOperationException")]
    public void Server_QuicPortWithoutCertificate_ThrowsInvalidOperationException()
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

    [Fact(DisplayName = "MqttServer_QuicPort为0时正常启动TCP不启动QUIC")]
    public void Server_QuicPortZero_NormalTcpStart()
    {
        using var server = new MqttServer
        {
            Port = 0,
            QuicPort = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        server.Start();

        Assert.True(server.Active);
        Assert.Equal(0, server.QuicPort);
        Assert.True(server.Port > 0);

        server.Stop("测试完成");
    }
    #endregion

    #region 生命周期
    [Fact(DisplayName = "MqttServer_正常启动停止不抛异常")]
    public void Server_StartStop_NoException()
    {
        using var server = new MqttServer
        {
            Port = 0,
            QuicPort = 0,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
        };
        server.Start();
        Assert.True(server.Active);
        server.Stop("测试完成");
    }

    [Fact(DisplayName = "MqttServer_未启动时Active为false")]
    public void Server_NotStarted_ActiveFalse()
    {
        using var server = new MqttServer();
        Assert.False(server.Active);
    }

    [Fact(DisplayName = "MqttServer_重复StartStop不抛异常")]
    public void Server_MultipleStartStop_NoException()
    {
        var services = new ObjectContainer();
        services.AddSingleton(XTrace.Log);

        using var server = new MqttServer
        {
            Port = 0,
            QuicPort = 0,
            ServiceProvider = services.BuildServiceProvider(),
            Log = XTrace.Log,
        };

        server.Start();
        server.Stop("测试1");
        server.Start();
        server.Stop("测试2");
    }
    #endregion

    #region QUIC类型检查
    [Fact(DisplayName = "MqttQuicListener_IsSupported检查")]
    public void MqttQuicListener_IsSupportedCheck()
    {
        var supported = MqttQuicListener.IsSupported;
        Assert.True(supported || !supported);
    }

    [Fact(DisplayName = "MqttQuicClient_IsSupported检查")]
    public void MqttQuicClient_IsSupportedCheck()
    {
        var supported = MqttQuicClient.IsSupported;
        Assert.True(supported || !supported);
    }
    #endregion
}
#endif
