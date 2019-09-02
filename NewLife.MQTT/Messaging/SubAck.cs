using System;
using System.Collections.Generic;

namespace NewLife.MQTT.Messaging
{
    /// <summary>发布确认</summary>
    public sealed class SubAck : MqttIdMessage
    {
        #region 属性
        /// <summary>返回代码</summary>
        public IList<QualityOfService> ReturnCodes { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public SubAck()
        {
            Type = MqttType.SubAck;
        }
        #endregion

        /// <summary>为订阅创建响应</summary>
        /// <param name="msg"></param>
        /// <param name="maxQoS"></param>
        /// <returns></returns>
        public static SubAck Reply(SubscribeMessage msg, QualityOfService maxQoS)
        {
            var ack = new SubAck
            {
                Id = msg.Id
            };
            var reqs = msg.Requests;
            var codes = new QualityOfService[reqs.Count];
            for (var i = 0; i < reqs.Count; i++)
            {
                var qos = reqs[i].QualityOfService;
                codes[i] = qos <= maxQoS ? qos : maxQoS;
            }

            ack.ReturnCodes = codes;

            return ack;
        }
    }
}