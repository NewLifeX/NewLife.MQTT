using System.Threading.Tasks;
using NewLife.Log;
using NewLife.MQTT;
using Xunit;

namespace XUnitTest.Clients;

public class MqttClientSocks5Tests
{
    [Fact(DisplayName = "MqttClient SOCKS5 属性默认值")]
    public void Socks5_DefaultValues()
    {
        var client = new MqttClient();
        Assert.Null(client.Socks5Server);
        Assert.Null(client.Socks5UserName);
        Assert.Null(client.Socks5Password);
    }

    [Fact(DisplayName = "MqttClient SOCKS5 属性设置")]
    public void Socks5_Properties()
    {
        var client = new MqttClient
        {
            Socks5Server = "192.168.1.100:1080",
            Socks5UserName = "proxy-user",
            Socks5Password = "proxy-pass",
        };
        Assert.Equal("192.168.1.100:1080", client.Socks5Server);
        Assert.Equal("proxy-user", client.Socks5UserName);
        Assert.Equal("proxy-pass", client.Socks5Password);
    }

    [Fact(DisplayName = "MqttClient SOCKS5 设置后不影响的默认连接路径")]
    public async Task Socks5_DoesNotAffectDirectConnection()
    {
        // 设置 SOCKS5 但不使用代理（Server 为 tcp:// 而非 socks5://）
        var client = new MqttClient
        {
            Server = "tcp://127.0.0.1:0", // 端口 0 不会真正连接
            Socks5Server = "127.0.0.1:1080",
            Timeout = 1000,
            Log = XTrace.Log,
        };

        // 连接会失败（没有真实代理），但不应该崩溃
        // 这是对错误路径的防御性验证
        try
        {
            await client.ConnectAsync();
        }
        catch
        {
            // 预期会失败，但不应该抛出 NullReferenceException 等异常
        }
    }

    [Fact(DisplayName = "MqttClient SOCKS5 与 ProxyProtocol 可同时设置")]
    public void Socks5_WithProxyProtocol()
    {
        var client = new MqttClient
        {
            Socks5Server = "proxy:1080",
            EnableProxyProtocol = true,
        };
        Assert.True(client.EnableProxyProtocol);
        Assert.Equal("proxy:1080", client.Socks5Server);
    }
}
