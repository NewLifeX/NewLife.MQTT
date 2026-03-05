using System;
using System.Text;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>认证器和SASL机制单元测试</summary>
public class AuthenticatorTests
{
    #region DefaultMqttAuthenticator
    [Fact]
    public void DefaultAuthenticator_Authenticate_AlwaysAccepted()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.Equal(ConnectReturnCode.Accepted, auth.Authenticate("client1", "user", "pass"));
    }

    [Fact]
    public void DefaultAuthenticator_Authenticate_NullValues_Accepted()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.Equal(ConnectReturnCode.Accepted, auth.Authenticate(null, null, null));
    }

    [Fact]
    public void DefaultAuthenticator_AuthorizePublish_AlwaysTrue()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.True(auth.AuthorizePublish("client1", "test/topic"));
    }

    [Fact]
    public void DefaultAuthenticator_AuthorizePublish_NullClient_True()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.True(auth.AuthorizePublish(null, "test/topic"));
    }

    [Fact]
    public void DefaultAuthenticator_AuthorizeSubscribe_AlwaysTrue()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.True(auth.AuthorizeSubscribe("client1", "test/#"));
    }

    [Fact]
    public void DefaultAuthenticator_AuthorizeSubscribe_NullClient_True()
    {
        var auth = new DefaultMqttAuthenticator();

        Assert.True(auth.AuthorizeSubscribe(null, "test/#"));
    }
    #endregion

    #region ScramSha256Mechanism
    [Fact]
    public void ScramSha256_Name()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        Assert.Equal("SCRAM-SHA-256", mechanism.Name);
    }

    [Fact]
    public void ScramSha256_InitialState()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        Assert.False(mechanism.IsAuthenticated);
        Assert.Null(mechanism.AuthenticatedUser);
    }

    [Fact]
    public void ScramSha256_NullData_Step1_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        var step = mechanism.Process(null);

        Assert.True(step.IsComplete);
        Assert.False(step.Success);
    }

    [Fact]
    public void ScramSha256_EmptyData_Step1_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        var step = mechanism.Process([]);

        Assert.True(step.IsComplete);
        Assert.False(step.Success);
    }

    [Fact]
    public void ScramSha256_ValidClientFirst_ReturnsServerFirst()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        // 标准的 client-first-message
        var clientFirst = Encoding.UTF8.GetBytes("n,,n=user,r=rOprNGfwEbeRWgbNEkqO");
        var step = mechanism.Process(clientFirst);

        Assert.False(step.IsComplete);
        Assert.NotNull(step.ServerData);

        // 解析服务端响应
        var serverFirst = Encoding.UTF8.GetString(step.ServerData);
        Assert.Contains("r=rOprNGfwEbeRWgbNEkqO", serverFirst); // 包含客户端 nonce
        Assert.Contains("s=", serverFirst); // 包含 salt
        Assert.Contains("i=4096", serverFirst); // 包含迭代次数
    }

    [Fact]
    public void ScramSha256_MissingUsername_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        // 缺少 n= 用户名
        var clientFirst = Encoding.UTF8.GetBytes("n,,r=rOprNGfwEbeRWgbNEkqO");
        var step = mechanism.Process(clientFirst);

        Assert.True(step.IsComplete);
        Assert.False(step.Success);
    }

    [Fact]
    public void ScramSha256_MissingNonce_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        // 缺少 r= nonce
        var clientFirst = Encoding.UTF8.GetBytes("n,,n=user");
        var step = mechanism.Process(clientFirst);

        Assert.True(step.IsComplete);
        Assert.False(step.Success);
    }

    [Fact]
    public void ScramSha256_Step3_InvalidStep_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        // Step 1
        var clientFirst = Encoding.UTF8.GetBytes("n,,n=user,r=rOprNGfwEbeRWgbNEkqO");
        mechanism.Process(clientFirst);

        // Step 2 - 空数据
        var step2 = mechanism.Process(null);
        Assert.True(step2.IsComplete);
        Assert.False(step2.Success);

        // Step 3 - 无效步骤
        var step3 = mechanism.Process(null);
        Assert.True(step3.IsComplete);
        Assert.False(step3.Success);
    }

    [Fact]
    public void ScramSha256_UnknownUser_Fails()
    {
        var store = new TestCredentialStore();
        var mechanism = new ScramSha256Mechanism(store);

        // Step 1
        var clientFirst = Encoding.UTF8.GetBytes("n,,n=unknown_user,r=testNonce123");
        var step1 = mechanism.Process(clientFirst);
        Assert.False(step1.IsComplete);
        Assert.NotNull(step1.ServerData);

        // Step 2 - 会失败因为找不到用户密码
        var clientFinal = Encoding.UTF8.GetBytes("c=biws,r=wrongNonce,p=invalidProof");
        var step2 = mechanism.Process(clientFinal);
        Assert.True(step2.IsComplete);
        Assert.False(step2.Success);
    }

    [Fact]
    public void ScramSha256_FullHandshake_Success()
    {
        var store = new TestCredentialStore();
        store.Users["testuser"] = "testpassword";
        var mechanism = new ScramSha256Mechanism(store);

        // Step 1: client-first-message
        var cnonce = "rOprNGfwEbeRWgbNEkqO";
        var clientFirst = Encoding.UTF8.GetBytes($"n,,n=testuser,r={cnonce}");
        var step1 = mechanism.Process(clientFirst);
        Assert.False(step1.IsComplete);
        Assert.NotNull(step1.ServerData);

        // 解析 server-first-message
        var serverFirst = Encoding.UTF8.GetString(step1.ServerData);
        var serverParts = ParseScramResponse(serverFirst);
        var nonce = serverParts["r"];
        var salt = Convert.FromBase64String(serverParts["s"]);
        var iterations = Int32.Parse(serverParts["i"]);

        // 计算 SCRAM 证明
        var clientFirstBare = $"n=testuser,r={cnonce}";
        var password = "testpassword";

        // SaltedPassword = Hi(password, salt, iterations)
        Byte[] saltedPassword;
        saltedPassword = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            32);

        // ClientKey = HMAC(SaltedPassword, "Client Key")
        var clientKey = System.Security.Cryptography.HMACSHA256.HashData(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));

        // StoredKey = H(ClientKey)
        var storedKey = System.Security.Cryptography.SHA256.HashData(clientKey);

        // AuthMessage
        var clientFinalWithoutProof = $"c=biws,r={nonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        var authMessageBytes = Encoding.UTF8.GetBytes(authMessage);

        // ClientSignature = HMAC(StoredKey, AuthMessage)
        var clientSignature = System.Security.Cryptography.HMACSHA256.HashData(storedKey, authMessageBytes);

        // ClientProof = ClientKey XOR ClientSignature
        var proof = new Byte[clientKey.Length];
        for (var i = 0; i < proof.Length; i++)
            proof[i] = (Byte)(clientKey[i] ^ clientSignature[i]);

        var clientFinal = $"{clientFinalWithoutProof},p={Convert.ToBase64String(proof)}";
        var step2 = mechanism.Process(Encoding.UTF8.GetBytes(clientFinal));

        Assert.True(step2.IsComplete);
        Assert.True(step2.Success);
        Assert.True(mechanism.IsAuthenticated);
        Assert.Equal("testuser", mechanism.AuthenticatedUser);
        Assert.NotNull(step2.ServerData);

        // 验证 server-final-message 包含 v=
        var serverFinal = Encoding.UTF8.GetString(step2.ServerData);
        Assert.StartsWith("v=", serverFinal);
    }

    [Fact]
    public void ScramSha256_WrongProof_Fails()
    {
        var store = new TestCredentialStore();
        store.Users["testuser"] = "testpassword";
        var mechanism = new ScramSha256Mechanism(store);

        // Step 1
        var clientFirst = Encoding.UTF8.GetBytes("n,,n=testuser,r=testNonce");
        var step1 = mechanism.Process(clientFirst);
        Assert.False(step1.IsComplete);

        var serverFirst = Encoding.UTF8.GetString(step1.ServerData!);
        var serverParts = ParseScramResponse(serverFirst);
        var nonce = serverParts["r"];

        // Step 2 - 错误的证明
        var clientFinal = $"c=biws,r={nonce},p=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var step2 = mechanism.Process(Encoding.UTF8.GetBytes(clientFinal));

        Assert.True(step2.IsComplete);
        Assert.False(step2.Success);
        Assert.False(mechanism.IsAuthenticated);
    }
    #endregion

    #region SaslStep
    [Fact]
    public void SaslStep_DefaultValues()
    {
        var step = new SaslStep();

        Assert.Null(step.ServerData);
        Assert.False(step.IsComplete);
        Assert.False(step.Success);
    }
    #endregion

    #region 辅助
    private static System.Collections.Generic.Dictionary<String, String> ParseScramResponse(String input)
    {
        var result = new System.Collections.Generic.Dictionary<String, String>();
        foreach (var segment in input.Split(','))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
                result[segment[..eq]] = segment[(eq + 1)..];
        }
        return result;
    }

    /// <summary>测试用凭证存储</summary>
    private class TestCredentialStore : IMqttSaslCredentialStore
    {
        public System.Collections.Generic.Dictionary<String, String> Users { get; } = [];

        public String? GetPassword(String username)
        {
            Users.TryGetValue(username, out var pwd);
            return pwd;
        }
    }
    #endregion
}
