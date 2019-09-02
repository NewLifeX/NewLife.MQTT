using System;

namespace NewLife.MQTT.Messaging
{
    /// <summary>消息类型</summary>
    public enum MqttType : Byte
    {
        ///// <summary>保留</summary>
        //Reserved = 0,

        /// <summary>连接</summary>
        Connect = 1,

        /// <summary>连接确认</summary>
        ConnAck = 2,

        /// <summary>发布消息</summary>
        Publish = 3,

        /// <summary>发布确认</summary>
        PubAck = 4,

        /// <summary>发布已接收</summary>
        PubRec = 5,

        /// <summary>发布已释放</summary>
        PubRel = 6,

        /// <summary>发布已完成</summary>
        PubComp = 7,

        /// <summary>客户端订阅请求</summary>
        Subscribe = 8,

        /// <summary>订阅确认</summary>
        SubAck = 9,

        /// <summary>取消订阅</summary>
        UnSubscribe = 10,

        /// <summary>取消订阅确认</summary>
        UnSubAck = 11,

        /// <summary>Ping请求</summary>
        PingReq = 12,

        /// <summary>Ping响应</summary>
        PingResp = 13,

        /// <summary>断开连接</summary>
        Disconnect = 14,

        /// <summary>保留</summary>
        Reserved2 = 15
    }
}