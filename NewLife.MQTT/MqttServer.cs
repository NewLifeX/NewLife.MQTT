using NewLife.Net;

namespace NewLife.MQTT;

/// <summary>MQTT服务端</summary>
public class MqttServer : NetServer<MqttSession>
{
    /// <summary>启动</summary>
    protected override void OnStart()
    {
        Add(new MqttCodec());

        base.OnStart();
    }
}

/// <summary>会话</summary>
public class MqttSession : NetSession
{

}