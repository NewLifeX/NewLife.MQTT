using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NewLife.MQTT.Messaging;
using NewLife.Serialization;

namespace NewLife.MQTT.Handlers;

/// <summary>JWT 认证器。支持 HMAC-SHA256 签名的 JWT Token 认证和 Claims 主题级授权</summary>
/// <remarks>
/// 客户端将 JWT Token 作为密码（或用户名）传入。
/// Token 验证通过后提取 Claims，可用于主题级 ACL。
/// 支持的 Claims：
/// - sub: 客户端标识（ClientId）
/// - pub: 允许发布的主题模式（逗号分隔）
/// - sub: 允许订阅的主题模式（逗号分隔）
/// - exp: 过期时间（Unix 时间戳）
/// 零第三方依赖，手动实现 JWT 解析与验证。
/// </remarks>
public class MqttJwtAuthenticator : IMqttAuthenticator
{
    #region 属性
    /// <summary>JWT 签名密钥（UTF-8 字节）</summary>
    private readonly Byte[] _secretKey;

    /// <summary>允许的签发者。为空则不验证</summary>
    public String? ValidIssuer { get; set; }

    /// <summary>允许的受众。为空则不验证</summary>
    public String? ValidAudience { get; set; }

    /// <summary>时钟偏移容忍度。默认 30 秒</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Token 传递方式。默认从 Password 字段获取</summary>
    public JwtTokenLocation TokenLocation { get; set; } = JwtTokenLocation.Password;

    /// <summary>已验证 Token 的 Claims 缓存。key=clientId，value=claims</summary>
    private readonly ConcurrentDictionary<String, IDictionary<String, String>> _claimsCache = new();
    #endregion

    #region 构造
    /// <summary>使用密钥字符串实例化</summary>
    /// <param name="secret">JWT 签名密钥</param>
    public MqttJwtAuthenticator(String secret)
    {
        if (secret.IsNullOrEmpty()) throw new ArgumentNullException(nameof(secret));
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>使用密钥字节数组实例化</summary>
    /// <param name="secretKey">JWT 签名密钥</param>
    public MqttJwtAuthenticator(Byte[] secretKey)
    {
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
    }
    #endregion

    #region IMqttAuthenticator
    /// <summary>认证客户端连接</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password)
    {
        // 提取 Token
        var token = TokenLocation switch
        {
            JwtTokenLocation.Password => password,
            JwtTokenLocation.Username => username,
            _ => password,
        };

        if (token.IsNullOrEmpty())
            return ConnectReturnCode.RefusedBadUsernameOrPassword;

        // 解析并验证 JWT
        var result = TryParseAndValidate(token, out var claims, out var error);
        if (!result)
        {
            // 验证失败
            return ConnectReturnCode.RefusedNotAuthorized;
        }

        // 缓存 Claims
        var cacheKey = clientId ?? claims!["sub"];
        _claimsCache[cacheKey] = claims!;

        return ConnectReturnCode.Accepted;
    }

    /// <summary>检查发布权限。基于 JWT Claims 中的 pub 字段</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topic">发布主题</param>
    /// <returns></returns>
    public Boolean AuthorizePublish(String? clientId, String topic)
    {
        var claims = GetClaims(clientId);
        if (claims == null) return true; // 无法获取 Claims 时放行

        // 检查 pub claim
        if (claims.TryGetValue("pub", out var pubPatterns))
        {
            foreach (var pattern in pubPatterns.Split(','))
            {
                var trimmed = pattern.Trim();
                if (trimmed == "#") return true;
                if (MqttTopicFilter.IsMatch(trimmed, topic))
                    return true;
            }
            return false;
        }

        // 无 pub claim 时放行
        return true;
    }

    /// <summary>检查订阅权限。基于 JWT Claims 中的 sub claim（非标准，本实现用 "mqtt_sub" 区分）</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topicFilter">订阅主题过滤器</param>
    /// <returns></returns>
    public Boolean AuthorizeSubscribe(String? clientId, String topicFilter)
    {
        var claims = GetClaims(clientId);
        if (claims == null) return true;

        // 检查 mqtt_sub claim（使用 mqtt_sub 避免与 JWT 标准 sub 冲突）
        if (claims.TryGetValue("mqtt_sub", out var subPatterns))
        {
            foreach (var pattern in subPatterns.Split(','))
            {
                var trimmed = pattern.Trim();
                if (trimmed == "#") return true;
                if (MqttTopicFilter.IsMatch(trimmed, topicFilter))
                    return true;
            }
            return false;
        }

        // 也检查 sub claim（JWT 标准 claim，本实现同时用作主题匹配）
        if (claims.TryGetValue("sub_pattern", out var subPatPatterns))
        {
            foreach (var pattern in subPatPatterns.Split(','))
            {
                var trimmed = pattern.Trim();
                if (trimmed == "#") return true;
                if (MqttTopicFilter.IsMatch(trimmed, topicFilter))
                    return true;
            }
            return false;
        }

        return true;
    }
    #endregion

    #region JWT 解析
    /// <summary>尝试解析并验证 JWT Token</summary>
    /// <param name="token">JWT Token 字符串</param>
    /// <param name="claims">输出的 Claims 字典</param>
    /// <param name="error">错误信息</param>
    /// <returns>是否验证通过</returns>
    public Boolean TryParseAndValidate(String token, out IDictionary<String, String>? claims, out String? error)
    {
        claims = null;
        error = null;

        try
        {
            // 分割 JWT 三部分
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                error = "JWT 格式无效：应包含 Header.Payload.Signature 三部分";
                return false;
            }

            var headerBase64 = parts[0];
            var payloadBase64 = parts[1];
            var signatureBase64 = parts[2];

            // 验证签名
            var signingInput = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");
            var expectedSignature = Base64UrlDecode(signatureBase64);

            Byte[] computedSignature;
            using (var hmac = new HMACSHA256(_secretKey))
            {
                computedSignature = hmac.ComputeHash(signingInput);
            }

            if (!CryptographicEquals(expectedSignature, computedSignature))
            {
                error = "JWT 签名验证失败";
                return false;
            }

            // 解析 Header
            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(headerBase64));
            var header = JsonParser.Decode(headerJson);
            if (header == null || !header.TryGetValue("alg", out var alg) || alg?.ToString() != "HS256")
            {
                error = "JWT 算法不支持，仅支持 HS256";
                return false;
            }

            // 解析 Payload
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payloadBase64));
            var payloadObj = JsonParser.Decode(payloadJson);
            claims = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            if (payloadObj != null)
            {
                foreach (var kv in payloadObj)
                {
                    claims[kv.Key] = kv.Value?.ToString() ?? String.Empty;
                }
            }

            // 验证过期时间
            if (claims.TryGetValue("exp", out var expStr) && Int64.TryParse(expStr, out var expUnix))
            {
                var expTime = UnixTimeSecondsToDateTime(expUnix);
                if (DateTime.UtcNow > expTime + ClockSkew)
                {
                    error = "JWT Token 已过期";
                    return false;
                }
            }

            // 验证 NotBefore
            if (claims.TryGetValue("nbf", out var nbfStr) && Int64.TryParse(nbfStr, out var nbfUnix))
            {
                var nbfTime = UnixTimeSecondsToDateTime(nbfUnix);
                if (DateTime.UtcNow < nbfTime - ClockSkew)
                {
                    error = "JWT Token 尚未生效";
                    return false;
                }
            }

            // 验证签发者
            if (!ValidIssuer.IsNullOrEmpty() && claims.TryGetValue("iss", out var iss))
            {
                if (!String.Equals(iss, ValidIssuer, StringComparison.Ordinal))
                {
                    error = $"JWT 签发者不匹配：期望 {ValidIssuer}，实际 {iss}";
                    return false;
                }
            }

            // 验证受众
            if (!ValidAudience.IsNullOrEmpty() && claims.TryGetValue("aud", out var aud))
            {
                if (!String.Equals(aud, ValidAudience, StringComparison.Ordinal))
                {
                    error = $"JWT 受众不匹配：期望 {ValidAudience}，实际 {aud}";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"JWT 解析异常：{ex.Message}";
            return false;
        }
    }

    /// <summary>获取缓存的 Claims</summary>
    private IDictionary<String, String>? GetClaims(String? clientId)
    {
        if (clientId == null) return null;
        return _claimsCache.TryGetValue(clientId, out var claims) ? claims : null;
    }

    /// <summary>清除指定客户端的 Claims 缓存</summary>
    /// <param name="clientId">客户端标识</param>
    public void ClearClaims(String clientId) => _claimsCache.TryRemove(clientId, out _);

    /// <summary>清除所有缓存</summary>
    public void ClearAllClaims() => _claimsCache.Clear();
    #endregion

    #region 辅助
    /// <summary>Base64Url 解码</summary>
    private static Byte[] Base64UrlDecode(String input)
    {
        // 将 Base64Url 字符替换为标准 Base64
        var base64 = input.Replace('-', '+').Replace('_', '/');

        // 补充填充字符
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }

    /// <summary>安全比较两个字节数组（防时序攻击）</summary>
    private static Boolean CryptographicEquals(Byte[] a, Byte[] b)
    {
        if (a.Length != b.Length) return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }

    /// <summary>Unix 时间戳转 DateTime（兼容 net45）</summary>
    private static DateTime UnixTimeSecondsToDateTime(Int64 unixTimeSeconds)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(unixTimeSeconds);
    }
    #endregion
}

/// <summary>JWT Token 传递位置</summary>
public enum JwtTokenLocation
{
    /// <summary>Token 放在 Password 字段</summary>
    Password,

    /// <summary>Token 放在 Username 字段</summary>
    Username,
}
