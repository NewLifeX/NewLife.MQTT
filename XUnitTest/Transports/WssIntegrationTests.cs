#nullable enable
using System;
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

/// <summary>WSS 传输集成测试。验证配置 WebSocketCertificate 后 wss:// 的连接、订阅、发布与接收</summary>
public class WssIntegrationTests
{
    /// <summary>生成自签名服务器证书</summary>
    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(30));

        // SslStream 需要带私钥且可用的实例，重新导入一次
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    [Fact(DisplayName = "WSS：连接、订阅、发布、接收（自签名证书）")]
    public async Task Wss_Subscribe_Publish_Receive()
    {
        using var certificate = CreateSelfSignedCertificate();

        var server = new MqttServer
        {
            Port = 0,
            EnableWebSocket = true,
            WebSocketCertificate = certificate,
            ServiceProvider = new ObjectContainer().BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        server.Start();

        try
        {
            var url = $"wss://127.0.0.1:{server.WebSocketPort}{server.WebSocketPath}";

            using var sub = new MqttClient
            {
                Log = XTrace.Log,
                Server = url,
                ClientId = $"wss_sub_{Rand.NextString(4)}",
                Timeout = 15_000,
                Reconnect = false,
            };
            using var pub = new MqttClient
            {
                Log = XTrace.Log,
                Server = url,
                ClientId = $"wss_pub_{Rand.NextString(4)}",
                Timeout = 15_000,
                Reconnect = false,
            };

            var ack = await sub.ConnectAsync();
            Assert.NotNull(ack);

            var ack2 = await pub.ConnectAsync();
            Assert.NotNull(ack2);

            var received = new TaskCompletionSource<String>();
            sub.Received += (s, e) => received.TrySetResult(e.Arg.Payload?.ToStr() ?? "");

            var topic = $"wss/topic/{Rand.NextString(4)}";
            await sub.SubscribeAsync(topic);
            await Task.Delay(500);

            await pub.PublishAsync(topic, "hello-wss", QualityOfService.AtMostOnce);

            var done = await Task.WhenAny(received.Task, Task.Delay(10_000));
            Assert.True(done == received.Task, "10秒内未收到WSS消息");
            Assert.Equal("hello-wss", await received.Task);

            await sub.DisconnectAsync();
            await pub.DisconnectAsync();
        }
        finally
        {
            server.TryDispose();
        }
    }
}
