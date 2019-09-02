using System;

namespace NewLife.MQTT.Messaging
{
    /// <summary>发布已完成</summary>
    public sealed class PubComp : MqttIdMessage
    {
        #region 属性
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public PubComp()
        {
            Type = MqttType.PubComp;
        }
        #endregion
    }
}