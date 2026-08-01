using System;
using System.Collections.Generic;
using System.Reflection;
using NewLife.MQTT.Handlers;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>管理 API 访问令牌鉴权测试。验证修复 S1</summary>
public class MqttManagementAuthTests
{
    /// <summary>模拟 IApiSession，提供 Host 与 Token。Host 置空走控制器反射注入 _server 路径</summary>
    private class FakeSession : IApiSession
    {
        public IApiHost Host { get; set; } = null!;
        public DateTime LastActive { get; set; } = DateTime.Now;
        public IApiSession[] AllSessions => [this];
        public String? Token { get; set; }
        public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();
        public Object? this[String key] { get => Items.TryGetValue(key, out var v) ? v : null; set => Items[key] = value; }
        public Int32 InvokeOneWay(String action, Object? args, Byte flag = 0) => 0;
        public Int32 SendMessage(NewLife.Messaging.IMessage msg) => 0;
    }

    /// <summary>构造带 Host 的控制器上下文</summary>
    private static (MqttManagementServer server, MqttManagementController ctrl, FakeSession session) Create()
    {
        var server = new MqttManagementServer { Exchange = new MqttExchange(), Port = 0 };
        var ctrl = new MqttManagementController();
        var session = new FakeSession();

        // 反射注入 _server（与现有测试的 InjectServer 一致）
        var field = typeof(MqttManagementController).GetField("_server", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(ctrl, server);

        return (server, ctrl, session);
    }

    /// <summary>构造 ControllerContext</summary>
    private static ControllerContext BuildContext(FakeSession session)
    {
        var ctx = new ControllerContext { Session = session };
        return ctx;
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：未配置 Token 时请求放行（向后兼容）")]
    public void NoTokenConfigured_AllowsRequest()
    {
        var (_, ctrl, session) = Create();

        // 不抛异常即通过
        var ex = Record.Exception(() => ctrl.OnActionExecuting(BuildContext(session)));
        Assert.Null(ex);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 且请求携带正确 Token 时放行")]
    public void CorrectToken_AllowsRequest()
    {
        var (server, ctrl, session) = Create();
        server.Token = "secret-123";
        session.Token = "secret-123";

        var ex = Record.Exception(() => ctrl.OnActionExecuting(BuildContext(session)));
        Assert.Null(ex);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 但请求未携带 Token 时拒绝")]
    public void MissingToken_RejectsRequest()
    {
        var (server, ctrl, session) = Create();
        server.Token = "secret-123";

        var ex = Record.Exception(() => ctrl.OnActionExecuting(BuildContext(session)));
        Assert.NotNull(ex);
        Assert.IsType<ApiException>(ex);
    }

    [Fact]
    [System.ComponentModel.DisplayName("鉴权：配置 Token 但请求携带错误 Token 时拒绝")]
    public void WrongToken_RejectsRequest()
    {
        var (server, ctrl, session) = Create();
        server.Token = "secret-123";
        session.Token = "wrong-token";

        var ex = Record.Exception(() => ctrl.OnActionExecuting(BuildContext(session)));
        Assert.NotNull(ex);
        Assert.IsType<ApiException>(ex);
    }
}
