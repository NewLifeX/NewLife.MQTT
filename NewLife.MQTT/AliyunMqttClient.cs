using System;
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
        protected override MqttMessage OnReceive(MqttMessage msg) => base.OnReceive(msg);
        #endregion

        #region 设备标签

        #endregion

        #region 属性上报
        public async Task PostProperty(Object data)
        {
            await SubscribeAsync($"/sys/{ProductKey}/{DeviceName}/thing/event/property/post_reply", OnPostProperty);
            await PublicAsync($"/sys/{ProductKey}/{DeviceName}/thing/event/property/post", data);

            await SubscribeAsync($"/sys/{ProductKey}/{DeviceName}/thing/service/property/set", OnSetProperty);
        }

        protected void OnPostProperty(PublishMessage pm) => WriteLog("OnPostProperty:{0}", pm.Payload.ToStr());
        protected void OnSetProperty(PublishMessage pm) => WriteLog("OnSetProperty:{0}", pm.Payload.ToStr());
        #endregion

        #region 时钟同步
        public async Task SyncTime()
        {
            await SubscribeAsync($"/ext/ntp/{ProductKey}/{DeviceName}/response", OnSyncTime);
            await PublicAsync($"/ext/ntp/{ProductKey}/{DeviceName}/request", new { version = "1.0" });
        }

        protected void OnSyncTime(PublishMessage pm) => WriteLog("OnSyncTime:{0}", pm.Payload.ToStr());
        #endregion
    }
}