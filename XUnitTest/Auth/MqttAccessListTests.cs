using System;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>黑名单/白名单认证器单元测试</summary>
public class MqttAccessListTests
{
    #region IP 黑白名单
    [System.ComponentModel.DisplayName("IP 白名单：白名单内 IP 允许连接")]
    [Fact]
    public void IpWhitelist_AllowedIp_Accepted()
    {
        var access = new MqttAccessList();
        access.IpWhitelist.Add("192.168.1.100");
        access.SetClientIp("client1", "192.168.1.100");

        var result = access.Authenticate("client1", "user", "pass");
        Assert.Equal(ConnectReturnCode.Accepted, result);
    }

    [System.ComponentModel.DisplayName("IP 白名单：白名单外 IP 拒绝连接")]
    [Fact]
    public void IpWhitelist_BlockedIp_Refused()
    {
        var access = new MqttAccessList();
        access.IpWhitelist.Add("192.168.1.100");
        access.SetClientIp("client1", "10.0.0.1");

        var result = access.Authenticate("client1", "user", "pass");
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [System.ComponentModel.DisplayName("IP 黑名单：黑名单内 IP 拒绝连接")]
    [Fact]
    public void IpBlacklist_BlockedIp_Refused()
    {
        var access = new MqttAccessList();
        access.IpBlacklist.Add("10.0.0.1");
        access.SetClientIp("client1", "10.0.0.1");

        var result = access.Authenticate("client1", "user", "pass");
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [System.ComponentModel.DisplayName("IP 黑名单：黑名单外 IP 允许连接")]
    [Fact]
    public void IpBlacklist_AllowedIp_Accepted()
    {
        var access = new MqttAccessList();
        access.IpBlacklist.Add("10.0.0.1");
        access.SetClientIp("client1", "192.168.1.1");

        var result = access.Authenticate("client1", "user", "pass");
        Assert.Equal(ConnectReturnCode.Accepted, result);
    }
    #endregion

    #region ClientId 黑白名单
    [System.ComponentModel.DisplayName("ClientId 黑名单：禁止特定客户端连接")]
    [Fact]
    public void ClientIdBlacklist_Blocked_Refused()
    {
        var access = new MqttAccessList();
        access.ClientIdBlacklist.Add("bad_client");

        var result = access.Authenticate("bad_client", "user", "pass");
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, result);
    }

    [System.ComponentModel.DisplayName("ClientId 白名单：仅允许白名单客户端")]
    [Fact]
    public void ClientIdWhitelist_OnlyAllowedClients()
    {
        var access = new MqttAccessList();
        access.ClientIdWhitelist.Add("good_client");

        Assert.Equal(ConnectReturnCode.Accepted, access.Authenticate("good_client", "user", "pass"));
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, access.Authenticate("other_client", "user", "pass"));
    }
    #endregion

    #region 主题黑白名单
    [System.ComponentModel.DisplayName("发布主题白名单：仅允许匹配主题发布")]
    [Fact]
    public void PublishTopicWhitelist_OnlyAllowedTopics()
    {
        var access = new MqttAccessList();
        access.PublishTopicWhitelist.Add("sensor/+/data");

        Assert.True(access.AuthorizePublish("client1", "sensor/temp/data"));
        Assert.False(access.AuthorizePublish("client1", "other/topic"));
    }

    [System.ComponentModel.DisplayName("发布主题黑名单：禁止匹配主题发布")]
    [Fact]
    public void PublishTopicBlacklist_BlockedTopics()
    {
        var access = new MqttAccessList();
        access.PublishTopicBlacklist.Add("admin/#");

        Assert.False(access.AuthorizePublish("client1", "admin/restart"));
        Assert.True(access.AuthorizePublish("client1", "sensor/data"));
    }

    [System.ComponentModel.DisplayName("订阅主题白名单：仅允许匹配主题订阅")]
    [Fact]
    public void SubscribeTopicWhitelist_OnlyAllowedTopics()
    {
        var access = new MqttAccessList();
        access.SubscribeTopicWhitelist.Add("device/+/status");

        Assert.True(access.AuthorizeSubscribe("client1", "device/abc/status"));
        Assert.False(access.AuthorizeSubscribe("client1", "admin/#"));
    }

    [System.ComponentModel.DisplayName("订阅主题黑名单：禁止系统主题订阅")]
    [Fact]
    public void SubscribeTopicBlacklist_BlockSystemTopics()
    {
        var access = new MqttAccessList();
        access.SubscribeTopicBlacklist.Add("$SYS/#");

        Assert.False(access.AuthorizeSubscribe("client1", "$SYS/broker/version"));
        Assert.True(access.AuthorizeSubscribe("client1", "sensor/data"));
    }
    #endregion

    #region 装饰器模式
    [System.ComponentModel.DisplayName("装饰器模式：先检查访问列表再委托内部认证器")]
    [Fact]
    public void DecoratorPattern_ChecksAccessThenDelegates()
    {
        var inner = new TestAuthenticator { AuthResult = ConnectReturnCode.RefusedBadUsernameOrPassword };
        var access = new MqttAccessList { Inner = inner };

        // 内部认证器拒绝
        Assert.Equal(ConnectReturnCode.RefusedBadUsernameOrPassword, access.Authenticate("client1", "bad", "pass"));

        // 修改内部认证器，再次通过
        inner.AuthResult = ConnectReturnCode.Accepted;
        Assert.Equal(ConnectReturnCode.Accepted, access.Authenticate("client1", "good", "pass"));
    }

    [System.ComponentModel.DisplayName("装饰器模式：黑名单优先于内部认证器")]
    [Fact]
    public void DecoratorPattern_BlacklistBlocksBeforeInner()
    {
        var inner = new TestAuthenticator { AuthResult = ConnectReturnCode.Accepted };
        var access = new MqttAccessList { Inner = inner };
        access.ClientIdBlacklist.Add("bad_client");

        // 黑名单拒绝，不会到达内部认证器
        Assert.Equal(ConnectReturnCode.RefusedNotAuthorized, access.Authenticate("bad_client", "user", "pass"));
    }
    #endregion

    #region 辅助
    private class TestAuthenticator : IMqttAuthenticator
    {
        public ConnectReturnCode AuthResult { get; set; } = ConnectReturnCode.Accepted;

        public ConnectReturnCode Authenticate(String? clientId, String? username, String? password) => AuthResult;
        public Boolean AuthorizePublish(String? clientId, String topic) => true;
        public Boolean AuthorizeSubscribe(String? clientId, String topicFilter) => true;
    }
    #endregion
}
