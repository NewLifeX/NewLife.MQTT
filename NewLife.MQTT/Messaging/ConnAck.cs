namespace NewLife.MQTT.Messaging;

/// <summary>连接返回代码</summary>
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

/// <summary>连接响应</summary>
public sealed class ConnAck : MqttMessage
{
    #region 属性
    /// <summary>会话</summary>
    /// <remarks>
    /// v3.1中是保留码。
    /// </remarks>
    public Boolean SessionPresent { get; set; }

    /// <summary>响应代码</summary>
    public ConnectReturnCode ReturnCode { get; set; }
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
        ReturnCode = (ConnectReturnCode)stream.ReadByte();

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

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}