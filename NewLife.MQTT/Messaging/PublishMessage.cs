using System;
using NewLife.Data;

namespace NewLife.MQTT.Messaging
{
    /// <summary>发布消息</summary>
    public sealed class PublishMessage : MqttIdMessage
    {
        #region 属性
        /// <summary>主题</summary>
        public String TopicName { get; set; }

        /// <summary>负载数据</summary>
        public Packet Payload { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public PublishMessage()
        {
            Type = MqttType.Publish;
        }
        #endregion
    }
}