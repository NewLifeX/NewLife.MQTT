using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife.MQTT.Messaging;

namespace NewLife.MQTT
{
    /// <summary>阿里云物联网平台</summary>
    public class AliyunMqttClient : MqttClient
    {
        #region 属性
        /// <summary>产品</summary>
        public String ProductKey { get; private set; }

        /// <summary>设备</summary>
        public String DeviceName { get; private set; }
        #endregion

        #region 构造
        /// <summary>使用阿里云物联网平台参数</summary>
        /// <param name="productKey">产品</param>
        /// <param name="deviceName">设备</param>
        /// <param name="deviceSecret">密钥</param>
        public AliyunMqttClient(String productKey, String deviceName, String deviceSecret)
        {
            ProductKey = productKey;
            DeviceName = deviceName;

            var sign = new MqttSign();
            if (sign.Calculate(productKey, deviceName, deviceSecret))
            {
                UserName = sign.UserName;
                Password = sign.Password;
                ClientId = sign.ClientId;
            }
        }
        #endregion

        #region 接收数据
        protected override MqttMessage OnReceive(MqttMessage msg)
        {
            return base.OnReceive(msg);
        }
        #endregion

        #region 设备标签

        #endregion

        #region 时钟同步
        public async Task SyncTime()
        {
            var topic1 = $"/ext/ntp/{ProductKey}/{DeviceName}/response";
            await SubscribeAsync(topic1, OnSyncTime);

            var topic2 = $"/ext/ntp/{ProductKey}/{DeviceName}/request";
            await PublicAsync(topic2, null);
        }

        protected void OnSyncTime(PublishMessage pm)
        {
            WriteLog("SyncTime:{0}", pm.Payload.ToStr());
        }
        #endregion
    }
}