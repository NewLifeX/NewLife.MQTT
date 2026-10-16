#if NET7_0_OR_GREATER
using System;
using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Model;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>QUIC 传输端到端集成测试。验证 quic:// 全链路：连接、订阅、发布、接收</summary>
/// <remarks>
/// QUIC 依赖操作系统的 MsQuic（Windows 11+ / 安装 libmsquic 的 Linux）。
/// 宿主不支持时用例提前返回并在日志说明；CI 的 Linux 作业安装 libmsquic 后真实执行。
/// </remarks>
[Collection("IntegrationTests")]
public class QuicIntegrationTests
{
    /// <summary>生成自签名服务器证书</summary>
    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30));

        // QUIC 服务端 TLS 需要带私钥且可用的实例，重新导入一次
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    /// <summary>探测空闲 UDP 端口（QUIC 监听需要显式端口号）</summary>
    private static Int32 GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    [Fact(DisplayName = "QUIC：连接、订阅、发布、接收（quic:// 全链路）")]
    public async Task Quic_Connect_Subscribe_Publish_Receive()
    {
        if (!QuicConnection.IsSupported)
        {
            XTrace.WriteLine("当前宿主不支持 QUIC（未安装 MsQuic），跳过端到端用例");
            return;
        }

        using var certificate = CreateSelfSignedCertificate();
        var quicPort = GetFreeUdpPort();

        var server = new MqttServer
        {
            Port = 0,
            QuicPort = quicPort,
            QuicCertificate = certificate,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        server.Start();

        try
        {
            var url = $"quic://127.0.0.1:{quicPort}";

            using var sub = new MqttClient
            {
                Log = XTrace.Log,
                Server = url,
                ClientId = $"quic_sub_{Rand.NextString(4)}",
                Timeout = 15_000,
                Reconnect = false,
            };
            using var pub = new MqttClient
            {
                Log = XTrace.Log,
                Server = url,
                ClientId = $"quic_pub_{Rand.NextString(4)}",
                Timeout = 15_000,
                Reconnect = false,
            };

            // QUIC 监听器异步启动，连接失败时重试直至就绪
            var deadline = DateTime.Now.AddSeconds(10);
            while (true)
            {
                try { await sub.ConnectAsync(); break; }
                catch when (DateTime.Now < deadline) { await Task.Delay(200); }
            }

            await pub.ConnectAsync();

            var received = new TaskCompletionSource<String>();
            sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

            var topic = $"quic/topic/{Rand.NextString(4)}";
            await sub.SubscribeAsync(topic);
            await Task.Delay(100);

            await pub.PublishAsync(topic, "hello-quic", QualityOfService.AtMostOnce);

            var done = await Task.WhenAny(received.Task, Task.Delay(10_000));
            Assert.True(done == received.Task, "10秒内未收到 QUIC 消息");
            Assert.Equal("hello-quic", await received.Task);

            await sub.DisconnectAsync();
            await pub.DisconnectAsync();
        }
        finally
        {
            server.TryDispose();
        }
    }
}
#endif
