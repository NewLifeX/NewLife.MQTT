using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using NewLife.MQTT.Messaging;
using NewLife.Serialization;

namespace NewLife.MQTT.Handlers;

/// <summary>OAuth 2.0 认证器。支持外部颁发的 JWT Bearer Token 认证，HS256/RS256 签名验证</summary>
/// <remarks>
/// 客户端将 OAuth 访问令牌作为密码传入。
/// Token 验证通过后提取 Claims（sub/scope/pub/mqtt_sub），可用于主题级 ACL。
/// 
/// 配置示例：
/// <code>
/// var oauth = new MqttOAuthAuthenticator
/// {
///     ValidIssuer = "https://auth.example.com",
///     ValidAudience = "mqtt-broker",
///     JwksEndpoint = "https://auth.example.com/.well-known/jwks.json",
/// };
/// services.AddSingleton&lt;IMqttAuthenticator&gt;(oauth);
/// </code>
/// 
/// 客户端连接时将 OAuth Token 放入 Password 字段：
/// <code>
/// var client = new MqttClient
/// {
///     Server = "tcp://broker:1883",
///     UserName = "client-id",
///     Password = "oauth_access_token_here",
/// };
/// </code>
/// </remarks>
public class MqttOAuthAuthenticator : IMqttAuthenticator
{
    #region 属性
    /// <summary>HMAC 签名密钥（对称算法，HS256 用）。为空时尝试 RS256（JWKS）</summary>
    private readonly Byte[]? _secretKey;

    /// <summary>允许的签发者（iss claim）。为空则不验证</summary>
    public String? ValidIssuer { get; set; }

    /// <summary>允许的受众（aud claim）。为空则不验证</summary>
    public String? ValidAudience { get; set; }

    /// <summary>JWKS 端点。用于 RS256 签名的公钥获取，非空时启用 RS256 验证</summary>
    public String? JwksEndpoint { get; set; }

    /// <summary>时钟偏移容忍度。默认 30 秒</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Token 传递方式。默认从 Password 字段获取</summary>
    public JwtTokenLocation TokenLocation { get; set; } = JwtTokenLocation.Password;

    /// <summary>已验证 Token 的 Claims 缓存。key=clientId，value=claims</summary>
    private readonly ConcurrentDictionary<String, IDictionary<String, String>> _claimsCache = new();

    /// <summary>RS256 公钥缓存。key=kid，value=RSA</summary>
    private readonly ConcurrentDictionary<String, RSA> _rsaKeyCache = new();

    private String? _lastJwksJson;
    private DateTime _lastJwksFetch = DateTime.MinValue;
    #endregion

    #region 构造
    /// <summary>使用 HMAC 密钥实例化（HS256）</summary>
    /// <param name="secretKey">共享密钥</param>
    public MqttOAuthAuthenticator(Byte[] secretKey)
    {
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
    }

    /// <summary>使用密钥字符串实例化（HS256）</summary>
    /// <param name="secret">共享密钥</param>
    public MqttOAuthAuthenticator(String secret)
    {
        if (secret.IsNullOrEmpty()) throw new ArgumentNullException(nameof(secret));
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>默认构造（RS256 模式，需设置 JwksEndpoint）</summary>
    public MqttOAuthAuthenticator() { }
    #endregion

    #region IMqttAuthenticator
    /// <summary>认证客户端连接</summary>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password)
    {
        var token = TokenLocation switch
        {
            JwtTokenLocation.Password => password,
            JwtTokenLocation.Username => username,
            _ => password,
        };

        if (token.IsNullOrEmpty())
            return ConnectReturnCode.RefusedBadUsernameOrPassword;

        if (!TryParseAndValidate(token, out var claims, out var error))
            return ConnectReturnCode.RefusedNotAuthorized;

        var cacheKey = clientId;
        if (cacheKey == null && claims!.TryGetValue("sub", out var subVal))
            cacheKey = subVal;
        if (cacheKey == null) cacheKey = token;
        _claimsCache[cacheKey] = claims!;

        return ConnectReturnCode.Accepted;
    }

    /// <summary>检查发布权限。基于 OAuth scope 或 pub claim</summary>
    public Boolean AuthorizePublish(String? clientId, String topic)
    {
        var claims = GetClaims(clientId);
        if (claims == null) return true;

        // 检查 pub claim（自定义）
        if (claims.TryGetValue("pub", out var pubPatterns))
        {
            foreach (var pattern in pubPatterns.Split(','))
            {
                var trimmed = pattern.Trim();
                if (trimmed == "#") return true;
                if (MatchTopic(topic, trimmed)) return true;
            }
            return false;
        }

        // 检查 OAuth scope，包含 "mqtt:publish" 或 "mqtt:*" 时放行
        if (claims.TryGetValue("scope", out var scope))
        {
            var scopes = scope.Split(' ');
            if (Array.Exists(scopes, s => s == "mqtt:publish" || s == "mqtt:*" || s == "*"))
                return true;
            // scope 级别不够但无 pub claim 时拒绝
            return false;
        }

        return true;
    }

    /// <summary>检查订阅权限。基于 OAuth scope 或 mqtt_sub claim</summary>
    public Boolean AuthorizeSubscribe(String? clientId, String topicFilter)
    {
        var claims = GetClaims(clientId);
        if (claims == null) return true;

        if (claims.TryGetValue("mqtt_sub", out var subPatterns))
        {
            foreach (var pattern in subPatterns.Split(','))
            {
                var trimmed = pattern.Trim();
                if (trimmed == "#") return true;
                if (MatchTopic(topicFilter, trimmed)) return true;
            }
            return false;
        }

        if (claims.TryGetValue("scope", out var scope))
        {
            var scopes = scope.Split(' ');
            if (Array.Exists(scopes, s => s == "mqtt:subscribe" || s == "mqtt:*" || s == "*"))
                return true;
            return false;
        }

        return true;
    }
    #endregion

    #region JWT 解析与验证
    private Boolean TryParseAndValidate(String token, out IDictionary<String, String>? claims, out String? error)
    {
        claims = null;
        error = null;

        // 分割 JWT: header.payload.signature
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            error = "Invalid JWT format";
            return false;
        }

        // 解码头部
        var headerJson = DecodeBase64Url(parts[0]);
        if (headerJson == null) { error = "Invalid JWT header"; return false; }

        var header = JsonParser.Decode(headerJson);
        if (header == null) { error = "Invalid JWT header JSON"; return false; }

        header.TryGetValue("alg", out var algObj);
        var alg = algObj?.ToString();
        header.TryGetValue("kid", out var kidObj);
        var kid = kidObj?.ToString();

        // 解码载荷
        var payloadJson = DecodeBase64Url(parts[1]);
        if (payloadJson == null) { error = "Invalid JWT payload"; return false; }

        var payloadObj = JsonParser.Decode(payloadJson);
        if (payloadObj == null) { error = "Invalid JWT payload JSON"; return false; }

        claims = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in payloadObj)
        {
            claims[kv.Key] = kv.Value?.ToString() ?? String.Empty;
        }

        // 验证签名
        var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = DecodeBase64UrlBytes(parts[2]);
        if (signature == null) { error = "Invalid JWT signature encoding"; return false; }

        var valid = alg switch
        {
            "HS256" => VerifyHs256(signingInput, signature),
            "RS256" => VerifyRs256(signingInput, signature, kid),
            _ => false,
        };

        if (!valid)
        {
            error = $"JWT signature verification failed (alg={alg})";
            return false;
        }

        // 验证 exp
        if (claims.TryGetValue("exp", out var expStr) && Int64.TryParse(expStr, out var exp))
        {
#if NET6_0_OR_GREATER
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
#else
            var expTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(exp);
#endif
            if (expTime <= DateTime.UtcNow.Add(ClockSkew))
            {
                error = "JWT token expired";
                return false;
            }
        }

        // 验证 nbf
        if (claims.TryGetValue("nbf", out var nbfStr) && Int64.TryParse(nbfStr, out var nbf))
        {
#if NET6_0_OR_GREATER
            var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf).UtcDateTime;
#else
            var nbfTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(nbf);
#endif
            if (nbfTime > DateTime.UtcNow.Add(-ClockSkew))
            {
                error = "JWT token not yet valid (nbf)";
                return false;
            }
        }

        // 验证 iss
        if (!ValidIssuer.IsNullOrEmpty())
        {
            if (!claims.TryGetValue("iss", out var iss) || iss != ValidIssuer)
            {
                error = $"JWT issuer mismatch: expected '{ValidIssuer}', got '{iss}'";
                return false;
            }
        }

        // 验证 aud
        if (!ValidAudience.IsNullOrEmpty())
        {
            if (!claims.TryGetValue("aud", out var aud) || aud != ValidAudience)
            {
                error = $"JWT audience mismatch: expected '{ValidAudience}', got '{aud}'";
                return false;
            }
        }

        return true;
    }

    private Boolean VerifyHs256(Byte[] signingInput, Byte[] signature)
    {
        if (_secretKey == null) return false;

        using var hmac = new HMACSHA256(_secretKey);
        var expected = hmac.ComputeHash(signingInput);
        return ConstantTimeEquals(expected, signature);
    }

    private Boolean VerifyRs256(Byte[] signingInput, Byte[] signature, String? kid)
    {
        // 获取 RSA 公钥
        var rsa = GetRsaKey(kid);
        if (rsa == null) return false;

#if NET6_0_OR_GREATER
        return rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
#else
        var csp = rsa as RSACryptoServiceProvider;
        if (csp == null) return false;
        return csp.VerifyData(signingInput, CryptoConfig.MapNameToOID("SHA256"), signature);
#endif
    }

    private RSA? GetRsaKey(String? kid)
    {
        // 先查缓存
        if (kid != null && _rsaKeyCache.TryGetValue(kid, out var cached)) return cached;

        // 从 JWKS 端点获取
        if (JwksEndpoint.IsNullOrEmpty()) return null;

        // 60 秒内不重复拉取
        if (!_lastJwksJson.IsNullOrEmpty() && (DateTime.UtcNow - _lastJwksFetch).TotalSeconds < 60)
        {
            // 尝试从缓存中找匹配 kid 的 key
            if (kid == null && _rsaKeyCache.Count > 0)
                return _rsaKeyCache.Values.First();
            return null;
        }

        try
        {
            // 使用 SimpleHttpClient 或直接 HttpClient 获取 JWKS
            var http = new HttpClient();
            _lastJwksJson = http.GetStringAsync(JwksEndpoint).Result;
            _lastJwksFetch = DateTime.UtcNow;

            // 解析 JWKS
            var jwks = JsonParser.Decode(_lastJwksJson);
            if (jwks == null) return null;

            if (!jwks.TryGetValue("keys", out var keysObj) || keysObj == null) return null;

            if (keysObj is not IList<Object> keyList) return null;

            foreach (var keyObj in keyList)
            {
                if (keyObj is not IDictionary<String, Object?> keyDict) continue;

                keyDict.TryGetValue("kty", out var ktyObj);
                keyDict.TryGetValue("kid", out var keyKidObj);
                keyDict.TryGetValue("n", out var nObj);
                keyDict.TryGetValue("e", out var eObj);

                var kty = ktyObj?.ToString();
                var keyKid = keyKidObj?.ToString();
                var n = nObj?.ToString();
                var e = eObj?.ToString();

                if (kty != "RSA" || n.IsNullOrEmpty() || e.IsNullOrEmpty()) continue;

                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportParameters(new RSAParameters
                    {
                        Modulus = DecodeBase64UrlBytes(n),
                        Exponent = DecodeBase64UrlBytes(e),
                    });

                    var cacheKid = keyKid ?? $"default_{_rsaKeyCache.Count}";
                    _rsaKeyCache[cacheKid] = rsa;

                    // 如果 kid 匹配，直接返回
                    if (kid != null && kid == keyKid) return rsa;
                }
                catch { /* 跳过无效 key */ }
            }
        }
        catch { /* JWKS 获取失败，尝试使用缓存的 key */ }

        // 如果指定了 kid 但没找到，或者没指定 kid，返回第一个缓存的 key
        if (kid != null && _rsaKeyCache.TryGetValue(kid, out var fallback)) return fallback;
        if (_rsaKeyCache.Count > 0) return _rsaKeyCache.Values.First();

        return null;
    }

    private IDictionary<String, String>? GetClaims(String? clientId)
    {
        if (clientId == null) return null;

        // 先按 clientId 查
        if (_claimsCache.TryGetValue(clientId, out var claims)) return claims;

        // 再按 claims 中的 sub field 查
        foreach (var kv in _claimsCache)
        {
            if (kv.Value.TryGetValue("sub", out var sub) && sub == clientId)
                return kv.Value;
        }

        return null;
    }
    #endregion

    #region 辅助方法
    private static String? DecodeBase64Url(String input)
    {
        try
        {
            var bytes = DecodeBase64UrlBytes(input);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }
        catch { return null; }
    }

    private static Byte[]? DecodeBase64UrlBytes(String input)
    {
        try
        {
            var padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
        catch { return null; }
    }

    private static Boolean ConstantTimeEquals(Byte[] a, Byte[] b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }

    /// <summary>通配符主题匹配</summary>
    private static Boolean MatchTopic(String topic, String filter)
    {
        if (filter == "#") return true;
        if (filter == topic) return true;

        var topicParts = topic.Split('/');
        var filterParts = filter.Split('/');

        for (var i = 0; i < filterParts.Length; i++)
        {
            if (i >= topicParts.Length) return filterParts[i] == "#";

            if (filterParts[i] == "+") continue;
            if (filterParts[i] == "#") return true;
            if (filterParts[i] != topicParts[i]) return false;
        }

        return topicParts.Length == filterParts.Length;
    }
    #endregion
}
