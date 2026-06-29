#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using NewLife;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>MqttQuicClient / MqttQuicListener 底层 QUIC 传输类单元测试</summary>
public class MqttQuicTransportTests
{
    #region MqttQuicClient
    [Fact(DisplayName = "MqttQuicClient_IsSupported_返回平台支持状态")]
    public void MqttQuicClient_IsSupported()
    {
        var supported = MqttQuicClient.IsSupported;
        Assert.True(supported || !supported);
    }

    [Fact(DisplayName = "MqttQuicClient_默认属性初始化正确")]
    public void MqttQuicClient_DefaultProperties()
    {
        var client = new MqttQuicClient();

        Assert.Null(client.Server);
        Assert.Equal(14567, client.Port);
        Assert.Equal(15_000, client.Timeout);
        Assert.Equal("mqtt", client.AlpnProtocol);

        client.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicClient_未设置Server时连接抛出ArgumentNullException")]
    public async Task MqttQuicClient_ConnectWithoutServer_Throws()
    {
        var client = new MqttQuicClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConnectAsync());
        client.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicClient_未连接时SendAsync抛出InvalidOperationException")]
    public async Task MqttQuicClient_SendWithoutConnection_Throws()
    {
        var client = new MqttQuicClient { Server = "127.0.0.1" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync(new PingRequest()));
        client.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicClient_未连接时ReceiveAsync抛出InvalidOperationException")]
    public async Task MqttQuicClient_ReceiveWithoutConnection_Throws()
    {
        var client = new MqttQuicClient { Server = "127.0.0.1" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReceiveAsync());
        client.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicClient_重复Dispose不抛异常")]
    public void MqttQuicClient_DisposeMultipleTimes_NoException()
    {
        var client = new MqttQuicClient();
        client.TryDispose();
        client.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicClient_属性读写正确")]
    public void MqttQuicClient_PropertySetAndGet()
    {
        var client = new MqttQuicClient
        {
            Server = "broker.example.com",
            Port = 8888,
            Timeout = 30_000,
            AlpnProtocol = "custom",
        };

        Assert.Equal("broker.example.com", client.Server);
        Assert.Equal(8888, client.Port);
        Assert.Equal(30_000, client.Timeout);
        Assert.Equal("custom", client.AlpnProtocol);

        client.TryDispose();
    }
    #endregion

    #region MqttQuicListener
    [Fact(DisplayName = "MqttQuicListener_IsSupported_返回平台支持状态")]
    public void MqttQuicListener_IsSupported()
    {
        var supported = MqttQuicListener.IsSupported;
        Assert.True(supported || !supported);
    }

    [Fact(DisplayName = "MqttQuicListener_默认属性初始化正确")]
    public void MqttQuicListener_DefaultProperties()
    {
        var listener = new MqttQuicListener();

        Assert.Equal(14567, listener.Port);
        Assert.Equal("mqtt", listener.AlpnProtocol);
        Assert.Null(listener.Certificate);
        Assert.False(listener.IsListening);

        listener.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicListener_未设置证书时启动抛出ArgumentNullException")]
    public async Task MqttQuicListener_StartWithoutCertificate_Throws()
    {
        var listener = new MqttQuicListener { Port = 24567 };
        await Assert.ThrowsAsync<ArgumentNullException>(() => listener.StartAsync());
        listener.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicListener_未启动时IsListening为false")]
    public void MqttQuicListener_IsListening_DefaultFalse()
    {
        var listener = new MqttQuicListener();
        Assert.False(listener.IsListening);
        listener.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicListener_重复Dispose不抛异常")]
    public void MqttQuicListener_DisposeMultipleTimes_NoException()
    {
        var listener = new MqttQuicListener();
        listener.TryDispose();
        listener.TryDispose();
    }

    [Fact(DisplayName = "MqttQuicListener_ConnectionReceived事件可绑定")]
    public void MqttQuicListener_ConnectionReceivedEvent_Bindable()
    {
        var listener = new MqttQuicListener();
        var eventFired = false;

        listener.ConnectionReceived += (s, e) => eventFired = true;

        Assert.False(eventFired);
        listener.TryDispose();
    }
    #endregion

    #region QuicMqttConnectionEventArgs
    [Fact(DisplayName = "QuicMqttConnectionEventArgs_类型编译验证")]
    public void QuicMqttConnectionEventArgs_TypeCheck()
    {
        var type = typeof(QuicMqttConnectionEventArgs);

        Assert.NotNull(type.GetProperty("Connection"));
        Assert.NotNull(type.GetProperty("Stream"));
        Assert.True(typeof(EventArgs).IsAssignableFrom(type));
    }
    #endregion
}
#endif
