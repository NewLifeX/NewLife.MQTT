using System;
using System.Collections.Generic;

namespace NewLife.MQTT.Messaging
{
    /// <summary>客户端订阅请求</summary>
    public sealed class SubscribeMessage : MqttIdMessage
    {
        #region 属性
        /// <summary>请求集合</summary>
        public IList<Subscription> Requests { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public SubscribeMessage()
        {
            Type = MqttType.Subscribe;
            QoS = QualityOfService.AtLeastOnce;
        }
        #endregion
    }
}