using System;
using System.Collections.Generic;
using System.IO;

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
        public SubAck() => Type = MqttType.SubAck;
        #endregion

        #region 读写方法
        /// <summary>从数据流中读取消息</summary>
        /// <param name="stream">数据流</param>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        protected override Boolean OnRead(Stream stream, Object context)
        {
            if (!base.OnRead(stream, context)) return false;

            var list = new List<QualityOfService>();
            while (stream.Position < stream.Length)
            {
                list.Add((QualityOfService)stream.ReadByte());
            }
            ReturnCodes = list;

            return true;
        }

        /// <summary>把消息写入到数据流中</summary>
        /// <param name="stream">数据流</param>
        /// <param name="context">上下文</param>
        protected override Boolean OnWrite(Stream stream, Object context)
        {
            if (!base.OnWrite(stream, context)) return false;

            foreach (var item in ReturnCodes)
            {
                stream.Write((Byte)item);
            }

            return true;
        }
        #endregion

        /// <summary>为订阅创建响应</summary>
        /// <param name="msg"></param>
        /// <param name="maxQoS"></param>
        /// <returns></returns>
        public static SubAck CreateReply(SubscribeMessage msg, QualityOfService maxQoS)
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