using NewLife.MQTT.Messaging;

namespace NewLife.MQTT;

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
    #endregion

    #region 设备标签

    #endregion

    #region 属性上报
    /// <summary>上报属性</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual async Task PostProperty(Object data)
    {
        await SubscribeAsync($"/sys/{ProductKey}/{DeviceName}/thing/event/property/post_reply", OnPostProperty);
        await PublishAsync($"/sys/{ProductKey}/{DeviceName}/thing/event/property/post", new
        {
            //id = 1,
            @params = data,
            method = "thing.event.property.post"
        });

        await SubscribeAsync($"/sys/{ProductKey}/{DeviceName}/thing/service/property/set", OnSetProperty);
    }

    /// <summary>收到属性上报</summary>
    /// <param name="pm"></param>
    protected virtual void OnPostProperty(PublishMessage pm) => WriteLog("OnPostProperty:{0}", pm.Payload?.ToStr());

    /// <summary>收到属性设置</summary>
    /// <param name="pm"></param>
    protected virtual void OnSetProperty(PublishMessage pm) => WriteLog("OnSetProperty:{0}", pm.Payload?.ToStr());
    #endregion

    #region 时钟同步
    /// <summary>同步时间</summary>
    /// <returns></returns>
    public async Task SyncTime()
    {
        await SubscribeAsync($"/ext/ntp/{ProductKey}/{DeviceName}/response", OnSyncTime);
        await PublishAsync($"/ext/ntp/{ProductKey}/{DeviceName}/request", new { version = "1.0" });
    }

    /// <summary>收到时间同步</summary>
    /// <param name="pm"></param>
    protected void OnSyncTime(PublishMessage pm) => WriteLog("OnSyncTime:{0}", pm.Payload?.ToStr());
    #endregion
}