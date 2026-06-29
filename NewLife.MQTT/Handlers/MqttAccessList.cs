using System.Collections.Concurrent;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT.Handlers;

/// <summary>访问列表认证器。支持 IP/ClientId 黑名单白名单，可按主题级控制发布和订阅权限</summary>
/// <remarks>
/// 可作为独立认证器使用，也可装饰另一个 IMqttAuthenticator。
/// 白名单非空时仅允许白名单内条目通过（黑名单被忽略）；
/// 白名单为空时黑名单生效。
/// </remarks>
public class MqttAccessList : IMqttAuthenticator
{
    #region 属性
    /// <summary>内部认证器。设置后将先检查访问列表，再委托给内部认证器</summary>
    public IMqttAuthenticator? Inner { get; set; }

    /// <summary>IP 白名单</summary>
    public ICollection<String> IpWhitelist { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>IP 黑名单</summary>
    public ICollection<String> IpBlacklist { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>ClientId 白名单</summary>
    public ICollection<String> ClientIdWhitelist { get; } = new HashSet<String>(StringComparer.Ordinal);

    /// <summary>ClientId 黑名单</summary>
    public ICollection<String> ClientIdBlacklist { get; } = new HashSet<String>(StringComparer.Ordinal);

    /// <summary>允许发布的主题白名单。非空时仅允许匹配的主题发布</summary>
    public ICollection<String> PublishTopicWhitelist { get; } = new List<String>();

    /// <summary>禁止发布的主题黑名单</summary>
    public ICollection<String> PublishTopicBlacklist { get; } = new List<String>();

    /// <summary>允许订阅的主题白名单。非空时仅允许匹配的主题订阅</summary>
    public ICollection<String> SubscribeTopicWhitelist { get; } = new List<String>();

    /// <summary>禁止订阅的主题黑名单</summary>
    public ICollection<String> SubscribeTopicBlacklist { get; } = new List<String>();

    /// <summary>当前连接的客户端 IP 地址（由外部设置）</summary>
    private readonly ConcurrentDictionary<String, String> _clientIps = new();
    #endregion

    #region 方法
    /// <summary>设置客户端 IP 地址</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">IP 地址</param>
    public void SetClientIp(String clientId, String ip) => _clientIps[clientId] = ip;

    /// <summary>移除客户端 IP 地址</summary>
    /// <param name="clientId">客户端标识</param>
    public void RemoveClientIp(String clientId) => _clientIps.TryRemove(clientId, out _);

    /// <summary>认证客户端连接</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public ConnectReturnCode Authenticate(String? clientId, String? username, String? password)
    {
        // 检查 IP 访问列表
        if (clientId != null && _clientIps.TryGetValue(clientId, out var ip))
        {
            if (!IsIpAllowed(ip))
                return ConnectReturnCode.RefusedNotAuthorized;
        }

        // 检查 ClientId 访问列表
        if (!IsClientIdAllowed(clientId))
            return ConnectReturnCode.RefusedNotAuthorized;

        // 委托给内部认证器
        return Inner?.Authenticate(clientId, username, password) ?? ConnectReturnCode.Accepted;
    }

    /// <summary>检查发布权限</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topic">发布主题</param>
    /// <returns></returns>
    public Boolean AuthorizePublish(String? clientId, String topic)
    {
        // 检查 IP 访问列表
        if (clientId != null && _clientIps.TryGetValue(clientId, out var ip))
        {
            if (!IsIpAllowed(ip)) return false;
        }

        // 检查 ClientId 访问列表
        if (!IsClientIdAllowed(clientId)) return false;

        // 检查主题访问列表
        if (!IsTopicAllowed(PublishTopicWhitelist, PublishTopicBlacklist, topic)) return false;

        // 委托给内部认证器
        return Inner?.AuthorizePublish(clientId, topic) ?? true;
    }

    /// <summary>检查订阅权限</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="topicFilter">订阅主题过滤器</param>
    /// <returns></returns>
    public Boolean AuthorizeSubscribe(String? clientId, String topicFilter)
    {
        // 检查 IP 访问列表
        if (clientId != null && _clientIps.TryGetValue(clientId, out var ip))
        {
            if (!IsIpAllowed(ip)) return false;
        }

        // 检查 ClientId 访问列表
        if (!IsClientIdAllowed(clientId)) return false;

        // 检查主题访问列表
        if (!IsTopicAllowed(SubscribeTopicWhitelist, SubscribeTopicBlacklist, topicFilter)) return false;

        // 委托给内部认证器
        return Inner?.AuthorizeSubscribe(clientId, topicFilter) ?? true;
    }
    #endregion

    #region 辅助
    /// <summary>检查 IP 是否允许</summary>
    private Boolean IsIpAllowed(String ip)
    {
        // 白名单非空时，仅白名单内 IP 允许
        if (IpWhitelist.Count > 0)
            return IpWhitelist.Contains(ip);

        // 黑名单检查
        if (IpBlacklist.Count > 0 && IpBlacklist.Contains(ip))
            return false;

        return true;
    }

    /// <summary>检查 ClientId 是否允许</summary>
    private Boolean IsClientIdAllowed(String? clientId)
    {
        if (clientId == null) return true;

        // 白名单非空时，仅白名单内 ClientId 允许
        if (ClientIdWhitelist.Count > 0)
            return ClientIdWhitelist.Contains(clientId);

        // 黑名单检查
        if (ClientIdBlacklist.Count > 0 && ClientIdBlacklist.Contains(clientId))
            return false;

        return true;
    }

    /// <summary>安全匹配主题（IsMatch 参数顺序: topicName, topicFilter）</summary>
    private static Boolean MatchTopic(String topicName, String topicFilter)
    {
        try
        {
            return MqttTopicFilter.IsMatch(topicName, topicFilter);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检查主题是否允许。白名单非空时需匹配白名单，否则不被黑名单匹配</summary>
    private static Boolean IsTopicAllowed(ICollection<String> whitelist, ICollection<String> blacklist, String topic)
    {
        // 白名单非空时，仅匹配白名单的主题允许
        if (whitelist.Count > 0)
        {
            foreach (var pattern in whitelist)
            {
                if (MatchTopic(topic, pattern))
                    return true;
            }
            return false;
        }

        // 黑名单检查
        if (blacklist.Count > 0)
        {
            foreach (var pattern in blacklist)
            {
                if (MatchTopic(topic, pattern))
                    return false;
            }
        }

        return true;
    }
    #endregion
}
