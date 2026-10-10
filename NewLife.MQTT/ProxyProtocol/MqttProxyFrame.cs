using NewLife.Messaging;
using NewLife.Net;

namespace NewLife.MQTT.ProxyProtocol;

/// <summary>PROXY 协议帧消息。协议泵把代理头定界为独立帧，交由会话层修正客户端地址</summary>
/// <remarks>PROXY 头只在连接建立初期出现，不属于 MQTT 协议；v12 协议泵下不能再用管线处理器剥离，改由编解码器定界为消息帧。</remarks>
public class MqttProxyFrame : Message
{
    #region 属性
    /// <summary>PROXY v1 头。与 <see cref="V2"/> 互斥</summary>
    public ProxyMessage? V1 { get; set; }

    /// <summary>PROXY v2 头。与 <see cref="V1"/> 互斥</summary>
    public ProxyMessageV2? V2 { get; set; }

    /// <summary>代理前的客户端地址。LOCAL 健康检测包为 null</summary>
    public NetUri? Client => V1?.GetClient() ?? V2?.Client;
    #endregion
}
