using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.MQTT.Messaging;
using NewLife.Net.Handlers;

namespace NewLife.MQTT
{
    /// <summary>编码器</summary>
    class MqttCodec : MessageCodec<MqttMessage>
    {
        private MqttFactory _Factory = new MqttFactory();

        /// <summary>实例化编码器</summary>
        public MqttCodec() => UserPacket = false;

        /// <summary>编码</summary>
        /// <param name="context"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        protected override Object Encode(IHandlerContext context, MqttMessage msg)
        {
            if (msg is MqttMessage cmd) return cmd.ToPacket();

            return null;
        }

        /// <summary>加入队列</summary>
        /// <param name="context"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        protected override void AddToQueue(IHandlerContext context, MqttMessage msg)
        {
            if (!msg.Reply) base.AddToQueue(context, msg);
        }

        /// <summary>解码</summary>
        /// <param name="context"></param>
        /// <param name="pk"></param>
        /// <returns></returns>
        protected override IList<MqttMessage> Decode(IHandlerContext context, Packet pk)
        {
            var ss = context.Owner as IExtend;
            var pc = ss["Codec"] as PacketCodec;
            if (pc == null) ss["Codec"] = pc = new PacketCodec { GetLength = p => GetLength(p, 1, -4) };

            var pks = pc.Parse(pk);
            var list = pks.Select(_Factory.ReadMessage).ToList();

            return list;
        }

        /// <summary>连接关闭时，清空粘包编码器</summary>
        /// <param name="context"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public override Boolean Close(IHandlerContext context, String reason)
        {
            if (context.Owner is IExtend ss) ss["Codec"] = null;

            return base.Close(context, reason);
        }

        /// <summary>是否匹配响应</summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        protected override Boolean IsMatch(Object request, Object response)
        {
            return request is MqttMessage req &&
                response is MqttMessage res &&
                req.Type + 1 == res.Type;
        }
    }
}