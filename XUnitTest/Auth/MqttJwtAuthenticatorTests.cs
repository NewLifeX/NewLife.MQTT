using System;
using System.Security.Cryptography;
using System.Text;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>JWT 认证器单元测试</summary>
public class MqttJwtAuthenticatorTests
{
    private const String TestSecret = "my_test_secret_key_for_jwt_hs256_signature";

    #region Token 解析与验证
    [System.ComponentModel.DisplayName("JWT 有效 Token 解析成功")]
    [Fact]
    public void ValidToken_ParseSuccess()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001", pub = "sensor/+/data" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.TryParseAndValidate(token, out var claims, out var error);

        Assert.True(result, error);
        Assert.NotNull(claims);
        Assert.Equal("device001", claims["sub"]);
        Assert.Equal("sensor/+/data", claims["pub"]);
    }

    [System.ComponentModel.DisplayName("JWT 无效签名被拒绝")]
    [Fact]
    public void InvalidSignature_Fails()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001" });
        // 篡改 Token 中间部分
        var parts = token.Split('.');
        parts[1] = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"sub\":\"hacker\"}"));

        var auth = new MqttJwtAuthenticator(TestSecret);
        var result = auth.TryParseAndValidate(String.Join(".", parts), out _, out var error);

        Assert.False(result);
        Assert.Contains("签名", error);
    }

    [System.ComponentModel.DisplayName("JWT 过期 Token 被拒绝")]
    [Fact]
    public void ExpiredToken_Fails()
    {
        // 生成 1 小时前过期的 Token
        var exp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var token = GenerateToken(TestSecret, new { sub = "device001", exp });
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.TryParseAndValidate(token, out _, out var error);

        Assert.False(result);
        Assert.Contains("过期", error);
    }

    [System.ComponentModel.DisplayName("JWT 未生效 Token（nbf）被拒绝")]
    [Fact]
    public void NotYetValidToken_Fails()
    {
        // nbf 设置为 1 小时后
        var nbf = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token = GenerateToken(TestSecret, new { sub = "device001", nbf });
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.TryParseAndValidate(token, out _, out var error);

        Assert.False(result);
        Assert.Contains("尚未生效", error);
    }

    [System.ComponentModel.DisplayName("JWT 格式错误被拒绝")]
    [Fact]
    public void MalformedToken_Fails()
    {
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.TryParseAndValidate("not.a.jwt.token.extra", out _, out var error);
        Assert.False(result);
        Assert.Contains("格式无效", error);

        result = auth.TryParseAndValidate("invalid", out _, out var error2);
        Assert.False(result2);
        Assert.Contains("格式无效", error2);
    }

    [System.ComponentModel.DisplayName("JWT 非 HS256 算法被拒绝")]
    [Fact]
    public void NonHS256Algorithm_Fails()
    {
        // 手动构造一个 alg=RS256 的 header
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"sub\":\"test\"}"));
        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        var signature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestSecret), signingInput));

        var auth = new MqttJwtAuthenticator(TestSecret);
        var result = auth.TryParseAndValidate($"{header}.{payload}.{signature}", out _, out var error);

        Assert.False(result);
        Assert.Contains("算法", error);
    }
    #endregion

    #region 签发者/受众验证
    [System.ComponentModel.DisplayName("JWT 签发者验证：匹配时通过")]
    [Fact]
    public void ValidIssuer_Match_Passes()
    {
        var token = GenerateToken(TestSecret, new { sub = "dev1", iss = "my-issuer" });
        var auth = new MqttJwtAuthenticator(TestSecret) { ValidIssuer = "my-issuer" };

        var result = auth.TryParseAndValidate(token, out _, out _);
        Assert.True(result);
    }

    [System.ComponentModel.DisplayName("JWT 签发者验证：不匹配时拒绝")]
    [Fact]
    public void ValidIssuer_Mismatch_Fails()
    {
        var token = GenerateToken(TestSecret, new { sub = "dev1", iss = "bad-issuer" });
        var auth = new MqttJwtAuthenticator(TestSecret) { ValidIssuer = "my-issuer" };

        var result = auth.TryParseAndValidate(token, out _, out var error);
        Assert.False(result);
        Assert.Contains("签发者", error);
    }

    [System.ComponentModel.DisplayName("JWT 受众验证：匹配时通过")]
    [Fact]
    public void ValidAudience_Match_Passes()
    {
        var token = GenerateToken(TestSecret, new { sub = "dev1", aud = "mqtt-broker" });
        var auth = new MqttJwtAuthenticator(TestSecret) { ValidAudience = "mqtt-broker" };

        var result = auth.TryParseAndValidate(token, out _, out _);
        Assert.True(result);
    }
    #endregion

    #region 认证集成
    [System.ComponentModel.DisplayName("JWT 作为密码认证：有效 Token 通过")]
    [Fact]
    public void Authenticate_ValidTokenInPassword_Accepted()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.Authenticate("device001", "anyUser", token);

        Assert.Equal(ConnectReturnCode.Accepted, result);
    }

    [System.ComponentModel.DisplayName("JWT 作为密码认证：无效 Token 拒绝")]
    [Fact]
    public void Authenticate_InvalidToken_Refused()
    {
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.Authenticate("device001", "anyUser", "invalid-token");

        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [System.ComponentModel.DisplayName("JWT 作为密码认证：空 Token 拒绝")]
    [Fact]
    public void Authenticate_EmptyToken_Refused()
    {
        var auth = new MqttJwtAuthenticator(TestSecret);

        var result = auth.Authenticate("device001", "anyUser", null);

        Assert.Equal(ConnectReturnCode.RefusedBadUsernameOrPassword, result);
    }

    [System.ComponentModel.DisplayName("JWT Token 放在 Username 字段")]
    [Fact]
    public void Authenticate_TokenInUsername_Accepted()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001" });
        var auth = new MqttJwtAuthenticator(TestSecret) { TokenLocation = JwtTokenLocation.Username };

        var result = auth.Authenticate("device001", token, "anyPass");

        Assert.Equal(ConnectReturnCode.Accepted, result);
    }
    #endregion

    #region Claims 主题授权
    [System.ComponentModel.DisplayName("JWT pub claim：限制发布主题")]
    [Fact]
    public void AuthorizePublish_PubClaimLimitsTopics()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001", pub = "sensor/+/data" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        // 先认证以缓存 Claims
        auth.Authenticate("device001", "user", token);

        Assert.True(auth.AuthorizePublish("device001", "sensor/temp/data"));
        Assert.False(auth.AuthorizePublish("device001", "admin/restart"));
    }

    [System.ComponentModel.DisplayName("JWT pub claim: # 通配允许所有发布")]
    [Fact]
    public void AuthorizePublish_WildcardAllowsAll()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001", pub = "#" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        auth.Authenticate("device001", "user", token);

        Assert.True(auth.AuthorizePublish("device001", "any/topic"));
    }

    [System.ComponentModel.DisplayName("JWT mqtt_sub claim：限制订阅主题")]
    [Fact]
    public void AuthorizeSubscribe_MqttSubClaimLimitsTopics()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001", mqtt_sub = "device/+/status" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        auth.Authenticate("device001", "user", token);

        Assert.True(auth.AuthorizeSubscribe("device001", "device/abc/status"));
        Assert.False(auth.AuthorizeSubscribe("device001", "$SYS/broker/version"));
    }

    [System.ComponentModel.DisplayName("JWT 无 pub claim 时全部放行")]
    [Fact]
    public void AuthorizePublish_NoPubClaim_AllAllowed()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        auth.Authenticate("device001", "user", token);

        Assert.True(auth.AuthorizePublish("device001", "any/topic"));
    }

    [System.ComponentModel.DisplayName("JWT Claims 缓存清除后重新认证")]
    [Fact]
    public void ClearClaims_RemovesCache()
    {
        var token = GenerateToken(TestSecret, new { sub = "device001", pub = "sensor/+/data" });
        var auth = new MqttJwtAuthenticator(TestSecret);

        auth.Authenticate("device001", "user", token);
        Assert.True(auth.AuthorizePublish("device001", "sensor/temp/data"));

        auth.ClearClaims("device001");
        // 清除后，无 pub claim 限制，全部放行
        Assert.True(auth.AuthorizePublish("device001", "admin/restart"));
    }
    #endregion

    #region 辅助方法
    /// <summary>生成 HS256 签名的 JWT Token</summary>
    private static String GenerateToken(String secret, Object payload)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var headerJson = header.ToJson();
        var payloadJson = payload.ToJson();

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signingInput);
        var signatureBase64 = Base64UrlEncode(signature);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    private static String Base64UrlEncode(Byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
    #endregion
}
