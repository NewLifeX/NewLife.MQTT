using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Handlers;

public class MqttOAuthAuthenticatorTests
{
    private static String CreateHs256Token(String secret, IDictionary<String, Object> claims)
    {
        var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        var payload = System.Text.Json.JsonSerializer.Serialize(claims);
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signingInput = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = hmac.ComputeHash(signingInput);
        var sigB64 = Base64UrlEncode(signature);
        return $"{headerB64}.{payloadB64}.{sigB64}";
    }

    private static String Base64UrlEncode(Byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    [Fact(DisplayName = "OAuth HS256 Token 验证成功")]
    public void Authenticate_ValidToken_ReturnsAccepted()
    {
        var secret = "test-secret-key-12345";
        var auth = new MqttOAuthAuthenticator(secret);
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["sub"] = "test-client",
            ["scope"] = "mqtt:*",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        });

        var result = auth.Authenticate("test-client", "user", token);
        Assert.Equal(ConnectReturnCode.Accepted, result);
    }

    [Fact(DisplayName = "OAuth 无效签名返回拒绝")]
    public void Authenticate_InvalidSignature_ReturnsNotAuthorized()
    {
        var auth = new MqttOAuthAuthenticator("real-secret");
        var token = CreateHs256Token("wrong-secret", new Dictionary<String, Object>
        {
            ["sub"] = "test-client",
        });

        var result = auth.Authenticate("test-client", "user", token);
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [Fact(DisplayName = "OAuth 过期 Token 返回拒绝")]
    public void Authenticate_ExpiredToken_ReturnsNotAuthorized()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret);
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["sub"] = "test-client",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(), // 已过期
        });

        var result = auth.Authenticate("test-client", "user", token);
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [Fact(DisplayName = "OAuth 空 Token 返回拒绝")]
    public void Authenticate_EmptyToken_ReturnsBadUsername()
    {
        var auth = new MqttOAuthAuthenticator("secret");
        var result = auth.Authenticate("test-client", "user", "");
        Assert.Equal(ConnectReturnCode.RefusedBadUsernameOrPassword, result);
    }

    [Fact(DisplayName = "OAuth null Token 返回拒绝")]
    public void Authenticate_NullToken_ReturnsBadUsername()
    {
        var auth = new MqttOAuthAuthenticator("secret");
        var result = auth.Authenticate("test-client", "user", null);
        Assert.Equal(ConnectReturnCode.RefusedBadUsernameOrPassword, result);
    }

    [Fact(DisplayName = "OAuth AuthorizePublish 基于 scope")]
    public void AuthorizePublish_ByScope()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret);
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["scope"] = "mqtt:publish",
        });

        auth.Authenticate("pub-client", "user", token);
        Assert.True(auth.AuthorizePublish("pub-client", "test/topic"));
    }

    [Fact(DisplayName = "OAuth AuthorizePublish 基于 pub claim")]
    public void AuthorizePublish_ByPubClaim()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret);
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["pub"] = "sensors/#",
        });

        auth.Authenticate("pub-client", "user", token);
        Assert.True(auth.AuthorizePublish("pub-client", "sensors/temp"));
        Assert.False(auth.AuthorizePublish("pub-client", "actuators/valve"));
    }

    [Fact(DisplayName = "OAuth AuthorizeSubscribe 基于 mqtt_sub claim")]
    public void AuthorizeSubscribe_BySubClaim()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret);
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["mqtt_sub"] = "alerts/#",
        });

        auth.Authenticate("sub-client", "user", token);
        Assert.True(auth.AuthorizeSubscribe("sub-client", "alerts/critical"));
        Assert.False(auth.AuthorizeSubscribe("sub-client", "logs/debug"));
    }

    [Fact(DisplayName = "OAuth TokenLocation 从 Username 获取")]
    public void TokenLocation_Username()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret)
        {
            TokenLocation = JwtTokenLocation.Username,
        };
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["sub"] = "test-client",
        });

        var result = auth.Authenticate("test-client", token, null); // token 放在 username 字段
        Assert.Equal(ConnectReturnCode.Accepted, result);
    }

    [Fact(DisplayName = "OAuth ValidIssuer 验证")]
    public void ValidIssuer_Matches()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret)
        {
            ValidIssuer = "https://auth.example.com",
        };
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["iss"] = "https://auth.example.com",
        });

        var result = auth.Authenticate("test-client", "user", token);
        Assert.Equal(ConnectReturnCode.Accepted, result);
    }

    [Fact(DisplayName = "OAuth ValidIssuer 不匹配拒绝")]
    public void ValidIssuer_Mismatch()
    {
        var secret = "test-secret";
        var auth = new MqttOAuthAuthenticator(secret)
        {
            ValidIssuer = "https://auth.example.com",
        };
        var token = CreateHs256Token(secret, new Dictionary<String, Object>
        {
            ["iss"] = "https://evil.com",
        });

        var result = auth.Authenticate("test-client", "user", token);
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [Fact(DisplayName = "OAuth 默认构造不抛异常")]
    public void DefaultConstructor_NoException()
    {
        var auth = new MqttOAuthAuthenticator();
        Assert.NotNull(auth);
    }
}
