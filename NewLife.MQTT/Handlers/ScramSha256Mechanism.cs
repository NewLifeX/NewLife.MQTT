using System.Security.Cryptography;
using System.Text;

namespace NewLife.MQTT.Handlers;

/// <summary>SCRAM-SHA-256 服务端实现</summary>
/// <remarks>
/// 实现 RFC 5802 SCRAM-SHA-256，作为 MQTT 5.0 增强认证机制。
/// 握手流程：
/// 1. 客户端发 CONNECT 携带 client-first-message（AuthenticationData）  
/// 2. 服务端返回 AUTH(0x18) 携带 server-first-message
/// 3. 客户端回 AUTH(0x18) 携带 client-final-message
/// 4. 服务端验证通过后返回 AUTH(0x00) 携带 server-final-message
/// </remarks>
public class ScramSha256Mechanism : ISaslMechanism
{
    #region 属性
    /// <summary>机制名称</summary>
    public String Name => "SCRAM-SHA-256";

    /// <inheritdoc/>
    public Boolean IsAuthenticated { get; private set; }

    /// <inheritdoc/>
    public String? AuthenticatedUser { get; private set; }

    private readonly IMqttSaslCredentialStore _store;
    private Int32 _step;
    private String? _clientFirstBare;
    private String? _serverFirstMessage;
    private String? _nonce;
    private static readonly Int32 _iterations = 4096;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="store">凭证存储</param>
    public ScramSha256Mechanism(IMqttSaslCredentialStore store) => _store = store;
    #endregion

    #region 握手
    /// <summary>处理客户端数据，推进握手</summary>
    public SaslStep Process(Byte[]? clientData)
    {
        _step++;
        try
        {
            return _step switch
            {
                1 => ProcessClientFirstMessage(clientData),
                2 => ProcessClientFinalMessage(clientData),
                _ => new SaslStep { IsComplete = true, Success = false },
            };
        }
        catch
        {
            return new SaslStep { IsComplete = true, Success = false };
        }
    }

    // 步骤 1：处理 client-first-message，返回 server-first-message
    private SaslStep ProcessClientFirstMessage(Byte[]? data)
    {
        if (data == null || data.Length == 0)
            return new SaslStep { IsComplete = true, Success = false };

        var msg = Encoding.UTF8.GetString(data);

        // 去掉 GS2 前缀 "n,," 或 "y,,"
        var bare = msg;
        if (msg.StartsWith("n,,") || msg.StartsWith("y,,"))
            bare = msg[3..];

        _clientFirstBare = bare;

        // 解析 n=<user>,r=<cnonce>
        var parts = ParsePairs(bare);
        if (!parts.TryGetValue("n", out var username) || !parts.TryGetValue("r", out var cnonce))
            return new SaslStep { IsComplete = true, Success = false };

        AuthenticatedUser = username;

        // 生成服务端随机 nonce，拼接客户端 nonce
        var snonceBytes = new Byte[18];
        GenerateRandomBytes(snonceBytes);
        var snonce = Convert.ToBase64String(snonceBytes);
        _nonce = cnonce + snonce;

        // 生成随机盐
        var salt = new Byte[16];
        GenerateRandomBytes(salt);
        var saltB64 = Convert.ToBase64String(salt);

        _serverFirstMessage = $"r={_nonce},s={saltB64},i={_iterations}";

        // 将 salt/iterations 存入字段，供步骤2使用
        _salt = salt;

        return new SaslStep { ServerData = Encoding.UTF8.GetBytes(_serverFirstMessage) };
    }

    private Byte[]? _salt;

    // 步骤 2：处理 client-final-message，验证并返回 server-final-message
    private SaslStep ProcessClientFinalMessage(Byte[]? data)
    {
        if (data == null || data.Length == 0 || _clientFirstBare == null || _serverFirstMessage == null || _salt == null)
            return new SaslStep { IsComplete = true, Success = false };

        var msg = Encoding.UTF8.GetString(data);

        // client-final-message = client-final-message-without-proof "," "p=" clientProof
        var proofIdx = msg.LastIndexOf(",p=", StringComparison.Ordinal);
        if (proofIdx < 0) return new SaslStep { IsComplete = true, Success = false };

        var withoutProof = msg[..proofIdx];
        var proofB64 = msg[(proofIdx + 3)..];

        // 解析 r= 验证 nonce
        var parts = ParsePairs(withoutProof);
        if (!parts.TryGetValue("r", out var nonce) || nonce != _nonce)
            return new SaslStep { IsComplete = true, Success = false };

        // 获取密码
        var password = _store.GetPassword(AuthenticatedUser!);
        if (password == null)
            return new SaslStep { IsComplete = true, Success = false };

        // AuthMessage = client-first-message-bare + "," + server-first-message + "," + client-final-without-proof
        var authMessage = $"{_clientFirstBare},{_serverFirstMessage},{withoutProof}";
        var authMessageBytes = Encoding.UTF8.GetBytes(authMessage);

        // SaltedPassword = Hi(password, salt, iterations) — PBKDF2-SHA256
        Byte[] saltedPassword;
#if NET6_0_OR_GREATER
        saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            _salt,
            _iterations,
            HashAlgorithmName.SHA256,
            32);
#else
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, _salt, _iterations))
        {
            saltedPassword = pbkdf2.GetBytes(32);
        }
#endif

        // ClientKey = HMAC-SHA256(SaltedPassword, "Client Key")
        var clientKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));

        // StoredKey = H(ClientKey)
        Byte[] storedKey;
        using (var sha256 = SHA256.Create())
        {
            storedKey = sha256.ComputeHash(clientKey);
        }

        // ClientSignature = HMAC-SHA256(StoredKey, AuthMessage)
        var clientSignature = HmacSha256(storedKey, authMessageBytes);

        // ClientProof = ClientKey XOR ClientSignature
        var expectedProof = new Byte[clientKey.Length];
        for (var i = 0; i < expectedProof.Length; i++)
            expectedProof[i] = (Byte)(clientKey[i] ^ clientSignature[i]);

        // 验证
        var receivedProof = Convert.FromBase64String(proofB64);
        if (!FixedTimeEquals(expectedProof, receivedProof))
            return new SaslStep { IsComplete = true, Success = false };

        // ServerKey = HMAC-SHA256(SaltedPassword, "Server Key")
        var serverKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Server Key"));

        // ServerSignature = HMAC-SHA256(ServerKey, AuthMessage)
        var serverSignature = HmacSha256(serverKey, authMessageBytes);

        IsAuthenticated = true;
        return new SaslStep
        {
            ServerData = Encoding.UTF8.GetBytes($"v={Convert.ToBase64String(serverSignature)}"),
            IsComplete = true,
            Success = true,
        };
    }
    #endregion

    #region 辅助
    private static Byte[] HmacSha256(Byte[] key, Byte[] data)
    {
#if NET6_0_OR_GREATER
        return HMACSHA256.HashData(key, data);
#else
        using (var hmac = new HMACSHA256(key))
        {
            return hmac.ComputeHash(data);
        }
#endif
    }

    private static void GenerateRandomBytes(Byte[] buffer)
    {
#if NET6_0_OR_GREATER
        RandomNumberGenerator.Fill(buffer);
#else
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
#endif
    }

    private static Boolean FixedTimeEquals(Byte[] a, Byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }

    private static Dictionary<String, String> ParsePairs(String input)
    {
        var result = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var segment in input.Split(','))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
                result[segment[..eq]] = segment[(eq + 1)..];
        }
        return result;
    }
    #endregion
}
