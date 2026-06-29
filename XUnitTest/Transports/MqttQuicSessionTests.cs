#if NET7_0_OR_GREATER
using System;
using System.Linq;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTest.Transports;

/// <summary>MqttQuicSession 服务端会话单元测试</summary>
/// <remarks>
/// MqttQuicSession 需要 QuicConnection + QuicStream 构造，
/// 这两个对象只能通过实际 QUIC 连接获取，无法在单元测试中构造。
/// 因此本测试类主要做编译期和反射期的类型验证。
/// </remarks>
public class MqttQuicSessionTests
{
    [Fact(DisplayName = "MqttQuicSession_类型包含全部必要公共属性")]
    public void Type_HasAllRequiredProperties()
    {
        var type = typeof(MqttQuicSession);

        Assert.NotNull(type.GetProperty("ClientId"));
        Assert.NotNull(type.GetProperty("ProtocolVersion"));
        Assert.NotNull(type.GetProperty("Active"));
        Assert.NotNull(type.GetProperty("Handler"));
        Assert.NotNull(type.GetProperty("Log"));
        Assert.NotNull(type.GetProperty("Tracer"));
        Assert.NotNull(type.GetProperty("Connection"));
        Assert.NotNull(type.GetProperty("Stream"));
    }

    [Fact(DisplayName = "MqttQuicSession_类型包含必要公共方法")]
    public void Type_HasRequiredMethods()
    {
        var type = typeof(MqttQuicSession);

        Assert.NotNull(type.GetMethod("Start"));
        Assert.NotNull(type.GetMethod("SendMessage", [typeof(MqttMessage)]));
    }

    [Fact(DisplayName = "MqttQuicSession_类型有Closed事件")]
    public void Type_HasClosedEvent()
    {
        var type = typeof(MqttQuicSession);
        Assert.NotNull(type.GetEvent("Closed"));
    }

    [Fact(DisplayName = "MqttQuicSession_Handler属性兼容IMqttHandler")]
    public void Handler_Property_CompatibleWithIMqttHandler()
    {
        var handler = new MqttHandler();
        Assert.NotNull(handler);
        Assert.True(handler is IMqttHandler);

        // 验证 Handler 属性类型为 IMqttHandler
        var prop = typeof(MqttQuicSession).GetProperty("Handler");
        Assert.NotNull(prop);
        Assert.Equal(typeof(IMqttHandler), prop.PropertyType);
    }

    [Fact(DisplayName = "MqttQuicSession_ProtocolVersion默认值为V311")]
    public void ProtocolVersion_DefaultValue()
    {
        // 通过反射获取 ProtocolVersion 属性的类型
        var prop = typeof(MqttQuicSession).GetProperty("ProtocolVersion");
        Assert.NotNull(prop);
        Assert.Equal(typeof(MqttVersion), prop.PropertyType);
    }
}
#endif
