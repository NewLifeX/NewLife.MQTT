#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.MQTT;
using NewLife.MQTT.Handlers;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>管理 API 访问令牌鉴权测试。验证管理接口在配置 Token 后的放行与拒绝行为</summary>
public class MqttManagementAuthTests
{
    /// <summary>启动管理服务。端口0由系统自动分配，避免并发冲突</summary>
    private static MqttManagementServer StartServer(String? token)
    {
        var server = new MqttManagementServer
        {
            Port = 0,
            Exchange = new MqttExchange(),
            Token = token,
        };
        server.Start();

        return server;
    }

    /// <summary>请求管理接口。token 为 null 时不携带令牌</summary>
    private static async Task<(Int32 Code, String Body)> GetAsync(MqttManagementServer server, String path, String? token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}{path}");
        if (token != null) request.Headers.TryAddWithoutValidation("x-token", token);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return ((Int32)response.StatusCode, body);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：未配置 Token 时请求放行（向后兼容）")]
    public async Task NoTokenConfigured_AllowsRequest()
    {
        using var server = StartServer(null);

        var (code, body) = await GetAsync(server, "/Api/Stats", null);

        Assert.Equal(200, code);
        Assert.Contains("ConnectedClients", body);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 且请求携带正确 Token 时放行")]
    public async Task CorrectToken_AllowsRequest()
    {
        using var server = StartServer("secret-123");

        var (code, _) = await GetAsync(server, "/Api/Stats", "secret-123");

        Assert.Equal(200, code);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 但请求未携带 Token 时拒绝")]
    public async Task MissingToken_RejectsRequest()
    {
        using var server = StartServer("secret-123");

        var (code, _) = await GetAsync(server, "/Api/Stats", null);

        Assert.Equal(401, code);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 但请求携带错误 Token 时拒绝")]
    public async Task WrongToken_RejectsRequest()
    {
        using var server = StartServer("secret-123");

        var (code, _) = await GetAsync(server, "/Api/Stats", "wrong-token");

        Assert.Equal(401, code);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：通过查询参数携带正确 Token 时放行")]
    public async Task QueryToken_AllowsRequest()
    {
        using var server = StartServer("secret-123");

        var (code, _) = await GetAsync(server, "/Api/Stats?token=secret-123", null);

        Assert.Equal(200, code);
    }

    [Fact]
    [System.ComponentModel.DisplayName("管理接口：未知动作返回404")]
    public async Task UnknownAction_Returns404()
    {
        using var server = StartServer(null);

        var (code, _) = await GetAsync(server, "/Api/NotExists", null);

        Assert.Equal(404, code);
    }
}
