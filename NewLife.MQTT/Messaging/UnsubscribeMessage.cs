using System;
using System.Collections.Generic;

namespace NewLife.MQTT.Messaging
{
    /// <summary>取消订阅</summary>
    public sealed class UnsubscribeMessage : MqttIdMessage
    {
        #region 属性
        /// <summary>主题过滤器</summary>
        public IEnumerable<String> TopicFilters { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public UnsubscribeMessage()
        {
            Type = MqttType.UnSubscribe;
            QoS = QualityOfService.AtLeastOnce;
        }
        #endregion
    }
}