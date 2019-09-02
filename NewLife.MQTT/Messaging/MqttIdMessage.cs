using System;
using System.IO;

namespace NewLife.MQTT.Messaging
{
    /// <summary>带Id的消息</summary>
    public class MqttIdMessage : MqttMessage
    {
        #region 属性
        /// <summary>标识</summary>
        public UInt16 Id { get; set; }
        #endregion

        #region 读写方法
        /// <summary>从数据流中读取消息</summary>
        /// <param name="stream">数据流</param>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override Boolean Read(Stream stream, Object context)
        {
            if (!base.Read(stream, context)) return false;

            // 读Id

            return true;
        }

        /// <summary>把消息写入到数据流中</summary>
        /// <param name="stream">数据流</param>
        /// <param name="context">上下文</param>
        public override Boolean Write(Stream stream, Object context)
        {
            if (!base.Write(stream, context)) return false;

            // 写Id

            return true;
        }
        #endregion
    }
}