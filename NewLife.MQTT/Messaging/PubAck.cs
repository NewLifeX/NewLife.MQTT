using System;

namespace NewLife.MQTT.Messaging
{
    /// <summary>发布确认</summary>
    public sealed class PubAck : MqttIdMessage
    {
        #region 属性
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public PubAck()
        {
            Type = MqttType.PubAck;
        }
        #endregion
    }
}