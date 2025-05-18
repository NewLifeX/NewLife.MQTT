using NewLife.Data;

namespace NewLife.MQTT.Messaging;

/// <summary>连接请求</summary>
/// <remarks>
/// 客户端和服务端建立网络连接后，第一个从客户端发送给服务端的包必须是CONNECT包。
/// 每个网络连接客户端只能发送一次CONNECT包。
/// 服务端必须把客户端发来的第二个CONNECT包当作违反协议处理，并断开与客户端的连接。
/// 载荷包含一个或多个编码字段，用来指定客户端的唯一标识，话题，信息，用户名和密码。
/// 除了客户端唯一标识，其他都是可选项，是否存在取决于可变包头里的标识。
/// </remarks>
public sealed class ConnectMessage : MqttMessage
{
    #region 属性
    /// <summary>协议名</summary>
    public String ProtocolName { get; set; } = "MQTT";

    /// <summary>协议级别</summary>
    /// <remarks>
    /// 3.1.1版本的协议等级是4（0x04）。
    /// 如果协议等级不被服务端支持，服务端必须响应一个包含代码0x01（不接受的协议等级）CONNACK包，然后断开和客户端的连接。
    /// </remarks>
    public Byte ProtocolLevel { get; set; } = 0x04;

    /// <summary>清理会话</summary>
    /// <remarks>
    /// 这个二进制位指定了会话状态的处理方式。
    /// 客户端和服务端可以保存会话状态，以支持跨网络连接的可靠消息传输。这个标志位用于控制会话状态的生存时间。
    /// 如果清理会话（CleanSession）标志被设置为0，服务端必须基于当前会话（使用客户端标识符识别）的状态恢复与客户端的通信。
    /// 如果没有与这个客户端标识符关联的会话，服务端必须创建一个新的会话。在连接断开之后，当连接断开后，客户端和服务端必须保存会话信息。
    /// 当清理会话标志为0的会话连接断开之后，服务端必须将之后的QoS 1和QoS 2级别的消息保存为会话状态的一部分，如果这些消息匹配断开连接时客户端的任何订阅。
    /// 服务端也可以保存满足相同条件的QoS 0级别的消息。
    /// 如果清理会话（CleanSession）标志被设置为1，客户端和服务端必须丢弃之前的任何会话并开始一个新的会话。会话仅持续和网络连接同样长的时间。
    /// 与这个会话关联的状态数据不能被任何之后的会话重用
    /// </remarks>
    public Boolean CleanSession { get; set; }

    /// <summary>遗嘱标志</summary>
    /// <remarks>
    /// 遗嘱标志（Will Flag）被设置为1，表示如果连接请求被接受了，遗嘱（Will Message）消息必须被存储在服务端并且与这个网络连接关联。
    /// 之后网络连接关闭时，服务端必须发布这个遗嘱消息，除非服务端收到DISCONNECT报文时删除了这个遗嘱消息
    /// </remarks>
    public Boolean HasWill { get; set; }

    /// <summary>遗嘱QoS</summary>
    /// <remarks>
    /// 这两位用于指定发布遗嘱消息时使用的服务质量等级。
    /// 如果遗嘱标志被设置为0，遗嘱QoS也必须设置为0(0x00)。
    /// 如果遗嘱标志被设置为1，遗嘱QoS的值可以等于0(0x00)，1(0x01)，2(0x02)。它的值不能等于3。
    /// </remarks>
    public QualityOfService WillQualityOfService { get; set; }

    /// <summary>遗嘱保留</summary>
    /// <remarks>
    /// 如果遗嘱消息被发布时需要保留，需要指定这一位的值。
    /// 如果遗嘱标志被设置为0，遗嘱保留（Will Retain）标志也必须设置为0。
    /// 如果遗嘱标志被设置为1：
    /// 如果遗嘱保留被设置为0，服务端必须将遗嘱消息当作非保留消息发布。
    /// 如果遗嘱保留被设置为1，服务端必须将遗嘱消息当作保留消息发布。
    /// </remarks>
    public Boolean WillRetain { get; set; }

    /// <summary>用户名标志</summary>
    public Boolean HasPassword { get; set; }

    /// <summary>密码标志</summary>
    public Boolean HasUsername { get; set; }

    /// <summary>保持连接</summary>
    /// <remarks>
    /// 保持连接（Keep Alive）是一个以秒为单位的时间间隔，表示为一个16位的字，它是指在客户端传输完成一个控制报文的时刻到发送下一个报文的时刻，
    /// 两者之间允许空闲的最大时间间隔。客户端负责保证控制报文发送的时间间隔不超过保持连接的值。
    /// 如果没有任何其它的控制报文可以发送，客户端必须发送一个PINGREQ报文
    /// </remarks>
    public UInt16 KeepAliveInSeconds { get; set; }

    /// <summary>用户名</summary>
    public String? Username { get; set; }

    /// <summary>密码</summary>
    public String? Password { get; set; }

    /// <summary>客户端标识。必填项</summary>
    public String? ClientId { get; set; }

    /// <summary>遗嘱主题</summary>
    public String? WillTopicName { get; set; }

    /// <summary>遗嘱消息</summary>
    public Byte[] WillMessage { get; set; }

    /// <summary>属性集合。MQTT5.0</summary>
    public IDictionary<Byte, UInt32>? Properties { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ConnectMessage() => Type = MqttType.Connect;

    /// <summary>已重载</summary>
    public override String ToString() => $"{Type}[ClientId={ClientId}, Username={Username}, CleanSession={CleanSession}]";
    #endregion

    #region 读写方法
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnRead(Stream stream, Object? context)
    {
        // 协议名
        ProtocolName = ReadString(stream);

        // 协议等级
        ProtocolLevel = (Byte)stream.ReadByte();

        // 连接标记 Connect Flags
        // 连接标志字节包含一些用于指定MQTT连接行为的参数。它还指出有效载荷中的字段是否存在。
        var flag = (Byte)stream.ReadByte();
        CleanSession = (flag & 0b0000_0010) > 0;
        HasWill = (flag & 0b0000_0100) > 0;
        WillQualityOfService = (QualityOfService)((flag & 0b0001_1000) >> 3);
        WillRetain = (flag & 0b0010_0000) > 0;
        HasPassword = (flag & 0b0100_0000) > 0;
        HasUsername = (flag & 0b1000_0000) > 0;

        // 连接超时
        KeepAliveInSeconds = stream.ReadBytes(2).ToUInt16(0, false);

        // MQTT5.0 属性集合
        if (ProtocolLevel >= 5)
        {
            var dic = new Dictionary<Byte, UInt32>();

            var len = stream.ReadByte();
            var buf = stream.ReadBytes(len);
            for (var i = 0; i < buf.Length / 5; i += 5)
            {
                //todo 这里有问题，不同ID的长度不同
                var id = buf[i];
                var val = buf.ToUInt32(i + 1, false);
                dic[id] = val;
            }

            Properties = dic;
        }

        // CONNECT报文的有效载荷（payload）包含一个或多个以长度为前缀的字段，可变报头中的标志决定是否包含这些字段。
        // 如果包含的话，必须按这个顺序出现：客户端标识符，遗嘱主题，遗嘱消息，用户名，密码 
        ClientId = ReadString(stream);
        if (HasWill)
        {
            WillTopicName = ReadString(stream);
            WillMessage = ReadData(stream);
        }
        if (HasUsername) Username = ReadString(stream);
        if (HasPassword) Password = ReadString(stream);

        return true;
    }

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    protected override Boolean OnWrite(Stream stream, Object? context)
    {
        // 协议名
        WriteString(stream, ProtocolName);

        // 协议等级
        stream.Write(ProtocolLevel);

        // 连接标识
        if (!WillTopicName.IsNullOrEmpty() || WillMessage != null) HasWill = true;
        if (!Username.IsNullOrEmpty()) HasUsername = true;
        if (!Password.IsNullOrEmpty()) HasPassword = true;

        var flag = 0;
        if (CleanSession) flag |= 0b0000_0010;
        if (HasWill) flag |= 0b0000_0100;
        flag |= ((Byte)WillQualityOfService << 3) & 0b0001_1000;
        if (WillRetain) flag |= 0b0010_0000;
        if (HasPassword) flag |= 0b0100_0000;
        if (HasUsername) flag |= 0b1000_0000;
        stream.Write((Byte)flag);

        // 连接超时
        stream.Write(KeepAliveInSeconds.GetBytes(false));

        // 扩展
        WriteString(stream, ClientId);
        if (HasWill)
        {
            WriteString(stream, WillTopicName);
            WriteData(stream, WillMessage);
        }
        if (HasUsername) WriteString(stream, Username);
        if (HasPassword) WriteString(stream, Password);

        return true;
    }

    /// <summary>获取计算的标识位。不同消息的有效标记位不同</summary>
    /// <returns></returns>
    protected override Byte GetFlag() => (Byte)((Byte)Type << 4);
    #endregion
}