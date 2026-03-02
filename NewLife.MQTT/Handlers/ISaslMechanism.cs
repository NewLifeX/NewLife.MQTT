namespace NewLife.MQTT.Handlers;

/// <summary>SASL 凭证存储接口。用于 SCRAM 等需要服务端存储密码的认证机制</summary>
/// <remarks>
/// 实现此接口后注入到 <see cref="MqttHandler.SaslCredentialStore"/>，即可启用 SASL 增强认证。
/// </remarks>
public interface IMqttSaslCredentialStore
{
    /// <summary>根据用户名获取明文密码</summary>
    /// <param name="username">客户端提供的用户名</param>
    /// <returns>明文密码；用户不存在时返回 null</returns>
    String? GetPassword(String username);
}

/// <summary>SASL 机制接口</summary>
/// <remarks>
/// 每个连接创建一个独立实例，负责一次 SASL 握手的全部状态管理。
/// </remarks>
public interface ISaslMechanism
{
    /// <summary>机制名称（如 "SCRAM-SHA-256"）</summary>
    String Name { get; }

    /// <summary>是否认证成功（握手完成后可读）</summary>
    Boolean IsAuthenticated { get; }

    /// <summary>认证成功后的用户名</summary>
    String? AuthenticatedUser { get; }

    /// <summary>处理来自客户端的数据，返回发送给客户端的数据</summary>
    /// <param name="clientData">客户端发来的 AuthenticationData；第一步可为 null</param>
    /// <returns>
    /// <see cref="SaslStep"/>.ServerData 发给客户端；
    /// <see cref="SaslStep"/>.IsComplete 为 true 表示握手结束；
    /// <see cref="SaslStep"/>.Success 表示认证成功（IsComplete=true 时有效）。
    /// </returns>
    SaslStep Process(Byte[]? clientData);
}

/// <summary>SASL 一步握手结果</summary>
public sealed class SaslStep
{
    /// <summary>发送给客户端的数据（base64 编码前的原始字节）</summary>
    public Byte[]? ServerData { get; set; }

    /// <summary>是否握手结束</summary>
    public Boolean IsComplete { get; set; }

    /// <summary>认证是否成功（IsComplete=true 时有效）</summary>
    public Boolean Success { get; set; }
}
