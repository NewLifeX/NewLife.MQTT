using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT 认证与权限控制接口</summary>
/// <remarks>
/// 提供可插拔的认证和授权框架，支持：
/// - 连接认证（用户名/密码/证书）
/// - 发布权限检查（ACL）
/// - 订阅权限检查（ACL）
/// 实现此接口并注入到 MqttHandler 中即可启用权限控制。
/// </remarks>
public interface IMqttAuthenticator
{
    /// <summary>认证客户端连接</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>认证结果，返回 Accepted 表示通过</returns>
    ConnectReturnCode Authenticate(String? clientId, String? username, String? password);

    /// <summary>检查发布权限</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topic">发布主题</param>
    /// <returns>是否允许发布</returns>
    Boolean AuthorizePublish(String? clientId, String topic);

    /// <summary>检查订阅权限</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topicFilter">订阅主题过滤器</param>
    /// <returns>是否允许订阅</returns>
    Boolean AuthorizeSubscribe(String? clientId, String topicFilter);
}

/// <summary>默认认证器。允许所有操作</summary>
public class DefaultMqttAuthenticator : IMqttAuthenticator
{
    /// <summary>认证客户端连接。默认全部通过</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public virtual ConnectReturnCode Authenticate(String? clientId, String? username, String? password) => ConnectReturnCode.Accepted;

    /// <summary>检查发布权限。默认全部允许</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topic">发布主题</param>
    /// <returns></returns>
    public virtual Boolean AuthorizePublish(String? clientId, String topic) => true;

    /// <summary>检查订阅权限。默认全部允许</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topicFilter">订阅主题过滤器</param>
    /// <returns></returns>
    public virtual Boolean AuthorizeSubscribe(String? clientId, String topicFilter) => true;
}
