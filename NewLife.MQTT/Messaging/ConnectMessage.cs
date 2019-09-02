using System;
using NewLife.Data;

namespace NewLife.MQTT.Messaging
{
    /// <summary>连接请求</summary>
    public sealed class ConnectMessage : MqttMessage
    {
        #region 属性
        public String ProtocolName { get; set; }

        public Int32 ProtocolLevel { get; set; }

        public Boolean CleanSession { get; set; }

        public Boolean HasWill { get; set; }

        public QualityOfService WillQualityOfService { get; set; }

        public Boolean WillRetain { get; set; }

        public Boolean HasPassword { get; set; }

        public Boolean HasUsername { get; set; }

        public Int32 KeepAliveInSeconds { get; set; }

        public String Username { get; set; }

        public String Password { get; set; }

        public String ClientId { get; set; }

        public String WillTopicName { get; set; }

        public Packet WillMessage { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public ConnectMessage()
        {
            Type = MqttType.Connect;
        }
        #endregion
    }
}