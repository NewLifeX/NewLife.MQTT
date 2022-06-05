
namespace NewLife.MQTT.Messaging;

/// <summary>心跳响应</summary>
public sealed class PingResponse : MqttMessage
{
    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PingResponse() => Type = MqttType.PingResp;
    #endregion
}