namespace NewLife.MQTT.EventBus;

/// <summary>MQTT事件信封。事件总线在主题上传输的统一消息体</summary>
/// <remarks>
/// <para>默认发布模式会把事件包裹进信封，附加发布方标识 <see cref="ClientId"/>，订阅方据此排除自己发布的事件，避免回环。</para>
/// <para>设置 <c>AttachClientId = false</c> 后直接发布原始事件体（不带信封），用于与外部系统约定主题载荷格式的场景。</para>
/// </remarks>
/// <typeparam name="TEvent">事件类型</typeparam>
public class MqttEventEnvelope<TEvent>
{
    /// <summary>发布方客户标识。订阅方标识与之相同时跳过该事件</summary>
    public String? ClientId { get; set; }

    /// <summary>事件主题</summary>
    public String? Topic { get; set; }

    /// <summary>事件数据</summary>
    public TEvent? Data { get; set; }
}
