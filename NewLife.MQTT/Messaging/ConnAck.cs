namespace NewLife.MQTT.Messaging;

/// <summary>连接返回代码。MQTT 3.1.1</summary>
public enum ConnectReturnCode
{
    /// <summary>已接受</summary>
    Accepted = 0x00,

    /// <summary>拒绝不可用协议版本</summary>
    RefusedUnacceptableProtocolVersion = 0x01,

    /// <summary>拒绝标识</summary>
    RefusedIdentifierRejected = 0x02,

    /// <summary>服务不可用</summary>
    RefusedServerUnavailable = 0x03,

    /// <summary>错误用户名密码</summary>
    RefusedBadUsernameOrPassword = 0x04,

    /// <summary>未认证</summary>
    RefusedNotAuthorized = 0x05
}

/// <summary>MQTT 5.0 CONNACK原因码</summary>
public enum ConnAckReasonCode : Byte
{
    /// <summary>成功</summary>
    Success = 0x00,

    /// <summary>不指定的错误</summary>
    UnspecifiedError = 0x80,

    /// <summary>格式错误的报文</summary>
    MalformedPacket = 0x81,

    /// <summary>协议错误</summary>
    ProtocolError = 0x82,

    /// <summary>实现特定的错误</summary>
    ImplementationSpecificError = 0x83,

    /// <summary>不支持的协议版本</summary>
    UnsupportedProtocolVersion = 0x84,

    /// <summary>客户端标识符无效</summary>
    ClientIdentifierNotValid = 0x85,

    /// <summary>错误的用户名或密码</summary>
    BadUserNameOrPassword = 0x86,

    /// <summary>未授权</summary>
    NotAuthorized = 0x87,

    /// <summary>服务端不可用</summary>
    ServerUnavailable = 0x88,

    /// <summary>服务端正忙</summary>
    ServerBusy = 0x89,

    /// <summary>禁止</summary>
    Banned = 0x8A,

    /// <summary>错误的认证方法</summary>
    BadAuthenticationMethod = 0x8C,

    /// <summary>主题名无效</summary>
    TopicNameInvalid = 0x90,

    /// <summary>报文过大</summary>
    PacketTooLarge = 0x95,

    /// <summary>超出配额</summary>
    QuotaExceeded = 0x97,

    /// <summary>载荷格式无效</summary>
    PayloadFormatInvalid = 0x99,

    /// <summary>不支持保留消息</summary>
    RetainNotSupported = 0x9A,

    /// <summary>不支持的QoS</summary>
    QoSNotSupported = 0x9B,

    /// <summary>使用其他服务端</summary>
    UseAnotherServer = 0x9C,

    /// <summary>服务端迁移</summary>
    ServerMoved = 0x9D,

    /// <summary>超出连接速率限制</summary>
    ConnectionRateExceeded = 0x9F,
}

/// <summary>连接响应</summary>
public sealed class ConnAck : MqttMessage
{
    #region 属性
    /// <summary>会话</summary>
    /// <remarks>
    /// v3.1中是保留码。
    /// </remarks>
    public Boolean SessionPresent { get; set; }

    /// <summary>响应代码。MQTT 3.1.1</summary>
    public ConnectReturnCode ReturnCode { get; set; }

    /// <summary>MQTT 5.0 原因码</summary>
    public ConnAckReasonCode ReasonCode { get; set; }

    /// <summary>属性集合。MQTT 5.0</summary>
    public MqttProperties? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ConnAck() => Type = MqttType.ConnAck;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[SessionPresent={SessionPresent}, ReturnCode={ReturnCode}]";
    #endregion

    #region 方法
    /// <summary>子消息读取</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected override Boolean OnRead(Stream stream, Object? context)
    {
        SessionPresent = stream.ReadByte() > 0;

        var code = (Byte)stream.ReadByte();
        ReturnCode = (ConnectReturnCode)code;
        ReasonCode = (ConnAckReasonCode)code;

        // MQTT 5.0 属性
        if (stream.Position < stream.Length)
        {
            Properties = new MqttProperties();
            Properties.Read(stream);
        }

        return true;
    }

    /// <summary>子消息写入</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    protected override Boolean OnWrite(Stream stream, Object? context)
    {
        stream.Write((Byte)(SessionPresent ? 1 : 0));
        stream.Write((Byte)ReturnCode);

        // MQTT 5.0 属性
        if (Properties != null && Properties.Count > 0)
            Properties.Write(stream);

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}