using System;
using System.Collections.Generic;
using NewLife.Data;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttWebHook 和 WebHookEventData 单元测试</summary>
public class WebHookTests
{
    #region WebHookEventData
    [Fact]
    public void WebHookEventData_DefaultValues()
    {
        var data = new WebHookEventData();

        Assert.Equal(WebHookEvent.ClientConnected, data.Event);
        Assert.Null(data.ClientId);
        Assert.Null(data.UserName);
        Assert.Null(data.Topic);
        Assert.Null(data.Payload);
        Assert.Equal(0, data.QoS);
        Assert.False(data.Retain);
        Assert.True(data.Timestamp <= DateTime.Now);
        Assert.Null(data.Reason);
    }

    [Fact]
    public void WebHookEventData_SetProperties()
    {
        var data = new WebHookEventData
        {
            Event = WebHookEvent.MessagePublish,
            ClientId = "client1",
            UserName = "user1",
            Topic = "test/topic",
            Payload = Convert.ToBase64String("hello"u8.ToArray()),
            QoS = 1,
            Retain = true,
            Reason = "test reason",
        };

        Assert.Equal(WebHookEvent.MessagePublish, data.Event);
        Assert.Equal("client1", data.ClientId);
        Assert.Equal("user1", data.UserName);
        Assert.Equal("test/topic", data.Topic);
        Assert.NotNull(data.Payload);
        Assert.Equal(1, data.QoS);
        Assert.True(data.Retain);
        Assert.Equal("test reason", data.Reason);
    }
    #endregion

    #region WebHookEndpoint
    [Fact]
    public void WebHookEndpoint_DefaultValues()
    {
        var endpoint = new WebHookEndpoint();

        Assert.Null(endpoint.Events);
        Assert.Null(endpoint.Headers);
    }

    [Fact]
    public void WebHookEndpoint_SetUrl()
    {
        var endpoint = new WebHookEndpoint { Url = "http://example.com/webhook" };

        Assert.Equal("http://example.com/webhook", endpoint.Url);
    }

    [Fact]
    public void WebHookEndpoint_EventFilter()
    {
        var endpoint = new WebHookEndpoint
        {
            Url = "http://example.com",
            Events = [WebHookEvent.ClientConnected, WebHookEvent.ClientDisconnected],
        };

        Assert.Equal(2, endpoint.Events.Count);
        Assert.Contains(WebHookEvent.ClientConnected, endpoint.Events);
        Assert.Contains(WebHookEvent.ClientDisconnected, endpoint.Events);
    }

    [Fact]
    public void WebHookEndpoint_Headers()
    {
        var endpoint = new WebHookEndpoint
        {
            Url = "http://example.com",
            Headers = new Dictionary<String, String>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Custom"] = "value",
            },
        };

        Assert.Equal(2, endpoint.Headers.Count);
        Assert.Equal("Bearer token123", endpoint.Headers["Authorization"]);
    }
    #endregion

    #region MqttWebHook
    [Fact]
    public void MqttWebHook_DefaultValues()
    {
        using var hook = new MqttWebHook();

        Assert.NotNull(hook.Endpoints);
        Assert.Empty(hook.Endpoints);
        Assert.Equal(3, hook.MaxRetries);
        Assert.Equal(5000, hook.Timeout);
    }

    [Fact]
    public void MqttWebHook_Fire_NullData_NoException()
    {
        using var hook = new MqttWebHook();

        // 传 null 不应抛异常
        hook.Fire(null!);
    }

    [Fact]
    public void MqttWebHook_Fire_NoEndpoints_NoException()
    {
        using var hook = new MqttWebHook();
        var data = new WebHookEventData { Event = WebHookEvent.ClientConnected };

        // 没有端点不应抛异常
        hook.Fire(data);
    }

    [Fact]
    public void MqttWebHook_OnClientConnected_NoEndpoints_NoException()
    {
        // 没有端点时调用便捷方法不应抛异常
        using var hook = new MqttWebHook();
        hook.OnClientConnected("client1", "user1");
    }

    [Fact]
    public void MqttWebHook_OnClientDisconnected_NoEndpoints_NoException()
    {
        using var hook = new MqttWebHook();
        hook.OnClientDisconnected("client1", "normal");
    }

    [Fact]
    public void MqttWebHook_OnMessagePublish_NoEndpoints_NoException()
    {
        using var hook = new MqttWebHook();
        var msg = new PublishMessage
        {
            Topic = "test/topic",
            Payload = (ArrayPacket)"hello"u8.ToArray(),
            QoS = QualityOfService.AtLeastOnce,
            Retain = true,
        };

        hook.OnMessagePublish("client1", msg);
    }

    [Fact]
    public void MqttWebHook_OnClientSubscribe_NoEndpoints_NoException()
    {
        using var hook = new MqttWebHook();
        hook.OnClientSubscribe("client1", "test/#", QualityOfService.AtLeastOnce);
    }
    #endregion

    #region WebHookEvent 枚举
    [Theory]
    [InlineData(WebHookEvent.ClientConnected)]
    [InlineData(WebHookEvent.ClientDisconnected)]
    [InlineData(WebHookEvent.MessagePublish)]
    [InlineData(WebHookEvent.MessageDelivered)]
    [InlineData(WebHookEvent.ClientSubscribe)]
    [InlineData(WebHookEvent.ClientUnsubscribe)]
    public void WebHookEvent_AllValues(WebHookEvent eventType)
    {
        var data = new WebHookEventData { Event = eventType };

        Assert.Equal(eventType, data.Event);
    }
    #endregion
}
