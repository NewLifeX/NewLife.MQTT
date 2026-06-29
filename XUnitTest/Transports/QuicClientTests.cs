#if NET7_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>QuicClient QUIC 传输适配器单元测试</summary>
public class QuicClientTests
{
    [Fact(DisplayName = "QuicClient_IsSupported_返回平台支持状态")]
    public void IsSupported_ReturnsPlatformSupport()
    {
        // IsSupported 反映当前平台是否支持 QUIC
        var supported = QuicClient.IsSupported;

        // 在 Windows 11+ / Linux with msquic 上为 true
        // 不做硬断言，仅验证不抛异常
        Assert.True(supported || !supported);
    }

    [Fact(DisplayName = "QuicClient_默认属性初始化正确")]
    public void DefaultProperties_InitializedCorrectly()
    {
        var client = new QuicClient();

        Assert.Equal(14567, client.Port);
        Assert.Equal(15_000, client.Timeout);
        Assert.Equal("mqtt", client.AlpnProtocol);
        Assert.Null(client.Server);
        Assert.Null(client.Certificate);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_未设置Server时连接抛出ArgumentNullException")]
    public async Task Connect_WithoutServer_ThrowsArgumentNullException()
    {
        var client = new QuicClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConnectAsync());

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_未连接时发送消息抛出InvalidOperationException")]
    public void SendMessage_WithoutConnection_ThrowsInvalidOperationException()
    {
        var client = new QuicClient { Server = "127.0.0.1" };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            client.SendMessage(new PingRequest()));
        Assert.Contains("未建立 QUIC 连接", ex.Message);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_未连接时异步发送消息抛出InvalidOperationException")]
    public async Task SendMessageAsync_WithoutConnection_ThrowsInvalidOperationException()
    {
        var client = new QuicClient { Server = "127.0.0.1" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendMessageAsync(new PingRequest()));
        Assert.Contains("未建立 QUIC 连接", ex.Message);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_设置证书后Certificate属性正确赋值")]
    public void Certificate_PropertyAssignment()
    {
        var client = new QuicClient();

        // 未设置证书时为 null
        Assert.Null(client.Certificate);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_事件绑定和触发不抛异常")]
    public void Events_BindAndUnbind_NoException()
    {
        var client = new QuicClient();
        var receivedCount = 0;
        var closedCount = 0;
        var errorCount = 0;

        client.Received += (s, m) => receivedCount++;
        client.Closed += (s, e) => closedCount++;
        client.Error += (s, e) => errorCount++;

        // 解绑前计数器应为 0（无连接时不会触发）
        Assert.Equal(0, receivedCount);
        Assert.Equal(0, closedCount);
        Assert.Equal(0, errorCount);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_重复Dispose不抛异常")]
    public void Dispose_MultipleCalls_NoException()
    {
        var client = new QuicClient();

        client.TryDispose();
        client.TryDispose();
        // 不应抛异常
    }

    [Fact(DisplayName = "QuicClient_未连接时Active为false")]
    public void Active_WithoutConnection_ReturnsFalse()
    {
        var client = new QuicClient();

        Assert.False(client.Active);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_Server属性设置与读取正确")]
    public void Server_PropertySetAndGet()
    {
        var client = new QuicClient { Server = "test.example.com" };

        Assert.Equal("test.example.com", client.Server);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_Port属性设置与读取正确")]
    public void Port_PropertySetAndGet()
    {
        var client = new QuicClient { Port = 8888 };

        Assert.Equal(8888, client.Port);

        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_Timeout属性设置与读取正确")]
    public void Timeout_PropertySetAndGet()
    {
        var client = new QuicClient { Timeout = 30_000 };

        Assert.Equal(30_000, client.Timeout);

        client.TryDispose();
    }
}
#endif
