using System;

namespace NewLife.MQTT.Messaging
{
    /// <summary>发布已释放</summary>
    public sealed class PubRel : MqttIdMessage
    {
        #region 属性
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public PubRel()
        {
            Type = MqttType.PubRel;
            QoS = QualityOfService.AtLeastOnce;
        }
        #endregion
    }
}