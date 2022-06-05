
namespace NewLife.MQTT.Messaging;

/// <summary>心跳请求</summary>
public sealed class PingRequest : MqttMessage
{
    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public PingRequest() => Type = MqttType.PingReq;
    #endregion
}