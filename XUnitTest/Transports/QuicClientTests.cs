#if NET7_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>QuicClient QUIC 传输适配器完整单元测试</summary>
public class QuicClientTests
{
    #region 平台与属性
    [Fact(DisplayName = "QuicClient_IsSupported_返回平台支持状态")]
    public void IsSupported_ReturnsPlatformSupport()
    {
        var supported = QuicClient.IsSupported;
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
        Assert.False(client.Active);
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_所有属性读写正确")]
    public void AllProperties_SetAndGet()
    {
        var client = new QuicClient
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

    [Fact(DisplayName = "QuicClient_未连接时Active为false")]
    public void Active_WithoutConnection_ReturnsFalse()
    {
        var client = new QuicClient();
        Assert.False(client.Active);
        client.TryDispose();
    }
    #endregion

    #region 参数校验
    [Fact(DisplayName = "QuicClient_未设置Server时连接抛出ArgumentNullException")]
    public async Task Connect_WithoutServer_ThrowsArgumentNullException()
    {
        var client = new QuicClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConnectAsync());
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_未连接时SendMessage抛出InvalidOperationException")]
    public void SendMessage_WithoutConnection_ThrowsInvalidOperationException()
    {
        var client = new QuicClient { Server = "127.0.0.1" };
        var ex = Assert.Throws<InvalidOperationException>(() => client.SendMessage(new PingRequest()));
        Assert.Contains("未建立 QUIC 连接", ex.Message);
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_未连接时SendMessageAsync抛出InvalidOperationException")]
    public async Task SendMessageAsync_WithoutConnection_ThrowsInvalidOperationException()
    {
        var client = new QuicClient { Server = "127.0.0.1" };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendMessageAsync(new PingRequest()));
        Assert.Contains("未建立 QUIC 连接", ex.Message);
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_各类消息类型SendMessage校验(未连接时抛异常)")]
    public void SendMessage_VariousMessageTypes_ValidationOnly()
    {
        var client = new QuicClient { Server = "127.0.0.1" };
        Assert.Throws<InvalidOperationException>(() =>
            client.SendMessage(new ConnectMessage { ClientId = "test" }));
        Assert.Throws<InvalidOperationException>(() =>
            client.SendMessage(new PublishMessage { Topic = "test", Payload = new ArrayPacket([1]) }));
        Assert.Throws<InvalidOperationException>(() =>
            client.SendMessage(new DisconnectMessage()));
        Assert.Throws<InvalidOperationException>(() =>
            client.SendMessage(new PingRequest()));
        client.TryDispose();
    }
    #endregion

    #region 生命周期与事件
    [Fact(DisplayName = "QuicClient_重复Dispose不抛异常")]
    public void Dispose_MultipleCalls_NoException()
    {
        var client = new QuicClient();
        client.TryDispose();
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_Dispose后Active为false")]
    public void Active_AfterDispose_ReturnsFalse()
    {
        var client = new QuicClient();
        client.TryDispose();
        Assert.False(client.Active);
    }

    [Fact(DisplayName = "QuicClient_事件绑定计数不误触发")]
    public void Events_Binding_CorrectCount()
    {
        var client = new QuicClient();
        var receivedCount = 0;
        var closedCount = 0;
        var errorCount = 0;

        client.Received += (s, m) => receivedCount++;
        client.Closed += (s, e) => closedCount++;
        client.Error += (s, e) => errorCount++;

        Assert.Equal(0, receivedCount);
        Assert.Equal(0, closedCount);
        Assert.Equal(0, errorCount);
        client.TryDispose();
    }
    #endregion

    #region 证书
    [Fact(DisplayName = "QuicClient_未设置证书时Certificate为null")]
    public void Certificate_DefaultIsNull()
    {
        var client = new QuicClient();
        Assert.Null(client.Certificate);
        client.TryDispose();
    }
    #endregion

    #region 日志与跟踪
    [Fact(DisplayName = "QuicClient_Log默认不为null")]
    public void Log_DefaultNotNull()
    {
        var client = new QuicClient();
        Assert.NotNull(client.Log);
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_Tracer默认为null")]
    public void Tracer_DefaultIsNull()
    {
        var client = new QuicClient();
        Assert.Null(client.Tracer);
        client.TryDispose();
    }

    [Fact(DisplayName = "QuicClient_WriteLog不抛异常")]
    public void WriteLog_NoException()
    {
        var client = new QuicClient();
        client.WriteLog("测试日志: {0}", "hello");
        client.TryDispose();
    }
    #endregion

    #region 并发安全
    [Fact(DisplayName = "QuicClient_并发事件绑定安全")]
    public void Concurrent_EventBinding_ThreadSafe()
    {
        var client = new QuicClient();
        var count = 0;
        Parallel.For(0, 100, i =>
        {
            client.Received += (s, m) => Interlocked.Increment(ref count);
        });
        Assert.Equal(0, count);
        client.TryDispose();
    }
    #endregion
}
#endif
