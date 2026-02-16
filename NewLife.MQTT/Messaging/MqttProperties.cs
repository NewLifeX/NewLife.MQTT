using System.Text;

namespace NewLife.MQTT.Messaging;

/// <summary>MQTT 5.0 属性集合</summary>
/// <remarks>
/// MQTT 5.0 在可变报头中引入属性（Properties）系统。
/// 每个属性由属性标识符（1字节）和对应类型的值组成。
/// 属性值类型包括：Byte、UInt16、UInt32、变长整数、UTF-8字符串、二进制数据、UTF-8字符串对。
/// 用户属性（UserProperty）可以出现多次。
/// </remarks>
public class MqttProperties
{
    #region 属性
    private readonly Dictionary<MqttPropertyId, Object> _props = [];

    /// <summary>用户属性集合。MQTT 5.0 支持多个同名用户属性</summary>
    public IList<KeyValuePair<String, String>> UserProperties { get; set; } = [];

    /// <summary>属性数量</summary>
    public Int32 Count => _props.Count + UserProperties.Count;
    #endregion

    #region 索引器
    /// <summary>获取或设置属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public Object? this[MqttPropertyId id]
    {
        get => _props.TryGetValue(id, out var val) ? val : null;
        set
        {
            if (value == null)
                _props.Remove(id);
            else
                _props[id] = value;
        }
    }
    #endregion

    #region 类型化访问
    /// <summary>获取Byte类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public Byte? GetByte(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is Byte b ? b : null;

    /// <summary>设置Byte类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetByte(MqttPropertyId id, Byte value) => _props[id] = value;

    /// <summary>获取UInt16类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public UInt16? GetUInt16(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is UInt16 v ? v : null;

    /// <summary>设置UInt16类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetUInt16(MqttPropertyId id, UInt16 value) => _props[id] = value;

    /// <summary>获取UInt32类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public UInt32? GetUInt32(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is UInt32 v ? v : null;

    /// <summary>设置UInt32类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetUInt32(MqttPropertyId id, UInt32 value) => _props[id] = value;

    /// <summary>获取字符串类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public String? GetString(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is String s ? s : null;

    /// <summary>设置字符串类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetString(MqttPropertyId id, String value) => _props[id] = value;

    /// <summary>获取二进制数据属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public Byte[]? GetBinary(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is Byte[] b ? b : null;

    /// <summary>设置二进制数据属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetBinary(MqttPropertyId id, Byte[] value) => _props[id] = value;

    /// <summary>获取变长整数类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public Int32? GetVariableInt(MqttPropertyId id) => _props.TryGetValue(id, out var val) && val is Int32 v ? v : null;

    /// <summary>设置变长整数类型属性值</summary>
    /// <param name="id">属性标识符</param>
    /// <param name="value">属性值</param>
    public void SetVariableInt(MqttPropertyId id, Int32 value) => _props[id] = value;

    /// <summary>是否包含指定属性</summary>
    /// <param name="id">属性标识符</param>
    /// <returns></returns>
    public Boolean Contains(MqttPropertyId id) => _props.ContainsKey(id);
    #endregion

    #region 读写方法
    /// <summary>从流中读取属性集合</summary>
    /// <param name="stream">数据流</param>
    /// <returns></returns>
    public Boolean Read(Stream stream)
    {
        // 读取属性总长度（变长整数编码）
        var totalLen = ReadVariableInt(stream);
        if (totalLen <= 0) return true;

        var endPos = stream.Position + totalLen;
        while (stream.Position < endPos)
        {
            var idByte = (Byte)stream.ReadByte();
            var id = (MqttPropertyId)idByte;

            switch (id)
            {
                // Byte 类型
                case MqttPropertyId.PayloadFormatIndicator:
                case MqttPropertyId.RequestProblemInformation:
                case MqttPropertyId.RequestResponseInformation:
                case MqttPropertyId.MaximumQoS:
                case MqttPropertyId.RetainAvailable:
                case MqttPropertyId.WildcardSubscriptionAvailable:
                case MqttPropertyId.SubscriptionIdentifierAvailable:
                case MqttPropertyId.SharedSubscriptionAvailable:
                    _props[id] = (Byte)stream.ReadByte();
                    break;

                // UInt16 类型
                case MqttPropertyId.ServerKeepAlive:
                case MqttPropertyId.ReceiveMaximum:
                case MqttPropertyId.TopicAliasMaximum:
                case MqttPropertyId.TopicAlias:
                    _props[id] = stream.ReadBytes(2).ToUInt16(0, false);
                    break;

                // UInt32 类型
                case MqttPropertyId.MessageExpiryInterval:
                case MqttPropertyId.SessionExpiryInterval:
                case MqttPropertyId.WillDelayInterval:
                case MqttPropertyId.MaximumPacketSize:
                    _props[id] = stream.ReadBytes(4).ToUInt32(0, false);
                    break;

                // 变长整数类型
                case MqttPropertyId.SubscriptionIdentifier:
                    _props[id] = ReadVariableInt(stream);
                    break;

                // UTF-8 字符串类型
                case MqttPropertyId.ContentType:
                case MqttPropertyId.ResponseTopic:
                case MqttPropertyId.AssignedClientIdentifier:
                case MqttPropertyId.AuthenticationMethod:
                case MqttPropertyId.ResponseInformation:
                case MqttPropertyId.ServerReference:
                case MqttPropertyId.ReasonString:
                    _props[id] = ReadUtf8String(stream);
                    break;

                // 二进制数据类型
                case MqttPropertyId.CorrelationData:
                case MqttPropertyId.AuthenticationData:
                    _props[id] = ReadBinaryData(stream);
                    break;

                // UTF-8 字符串对（用户属性，可出现多次）
                case MqttPropertyId.UserProperty:
                    var key = ReadUtf8String(stream);
                    var value = ReadUtf8String(stream);
                    UserProperties.Add(new KeyValuePair<String, String>(key, value));
                    break;

                default:
                    // 未知属性，跳到结尾避免解析异常
                    stream.Position = endPos;
                    break;
            }
        }

        return true;
    }

    /// <summary>写入属性集合到流中</summary>
    /// <param name="stream">数据流</param>
    /// <returns></returns>
    public Boolean Write(Stream stream)
    {
        // 先写入临时流计算属性总长度
        var ms = new MemoryStream();
        foreach (var item in _props)
        {
            ms.WriteByte((Byte)item.Key);

            switch (item.Key)
            {
                // Byte 类型
                case MqttPropertyId.PayloadFormatIndicator:
                case MqttPropertyId.RequestProblemInformation:
                case MqttPropertyId.RequestResponseInformation:
                case MqttPropertyId.MaximumQoS:
                case MqttPropertyId.RetainAvailable:
                case MqttPropertyId.WildcardSubscriptionAvailable:
                case MqttPropertyId.SubscriptionIdentifierAvailable:
                case MqttPropertyId.SharedSubscriptionAvailable:
                    ms.WriteByte((Byte)item.Value);
                    break;

                // UInt16 类型
                case MqttPropertyId.ServerKeepAlive:
                case MqttPropertyId.ReceiveMaximum:
                case MqttPropertyId.TopicAliasMaximum:
                case MqttPropertyId.TopicAlias:
                    ms.Write(((UInt16)item.Value).GetBytes(false));
                    break;

                // UInt32 类型
                case MqttPropertyId.MessageExpiryInterval:
                case MqttPropertyId.SessionExpiryInterval:
                case MqttPropertyId.WillDelayInterval:
                case MqttPropertyId.MaximumPacketSize:
                    ms.Write(((UInt32)item.Value).GetBytes(false));
                    break;

                // 变长整数类型
                case MqttPropertyId.SubscriptionIdentifier:
                    WriteVariableInt(ms, (Int32)item.Value);
                    break;

                // UTF-8 字符串类型
                case MqttPropertyId.ContentType:
                case MqttPropertyId.ResponseTopic:
                case MqttPropertyId.AssignedClientIdentifier:
                case MqttPropertyId.AuthenticationMethod:
                case MqttPropertyId.ResponseInformation:
                case MqttPropertyId.ServerReference:
                case MqttPropertyId.ReasonString:
                    WriteUtf8String(ms, (String)item.Value);
                    break;

                // 二进制数据类型
                case MqttPropertyId.CorrelationData:
                case MqttPropertyId.AuthenticationData:
                    WriteBinaryData(ms, (Byte[])item.Value);
                    break;
            }
        }

        // 写入用户属性（可出现多次）
        foreach (var item in UserProperties)
        {
            ms.WriteByte((Byte)MqttPropertyId.UserProperty);
            WriteUtf8String(ms, item.Key);
            WriteUtf8String(ms, item.Value);
        }

        // 写入属性总长度（变长整数编码）+ 属性数据
        WriteVariableInt(stream, (Int32)ms.Length);
        ms.Position = 0;
        ms.CopyTo(stream);

        return true;
    }
    #endregion

    #region 辅助方法
    /// <summary>读取变长整数</summary>
    /// <param name="stream">数据流</param>
    /// <returns></returns>
    public static Int32 ReadVariableInt(Stream stream)
    {
        var multiplier = 1;
        var value = 0;
        Byte encodedByte;

        do
        {
            encodedByte = (Byte)stream.ReadByte();
            value += (encodedByte & 0x7F) * multiplier;
            if (multiplier > 128 * 128 * 128) throw new InvalidDataException("变长整数编码异常，超过4字节限制");
            multiplier *= 128;
        }
        while ((encodedByte & 0x80) != 0);

        return value;
    }

    /// <summary>写入变长整数</summary>
    /// <param name="stream">数据流</param>
    /// <param name="value">值</param>
    public static void WriteVariableInt(Stream stream, Int32 value)
    {
        do
        {
            var encodedByte = (Byte)(value % 128);
            value /= 128;
            if (value > 0) encodedByte |= 0x80;
            stream.WriteByte(encodedByte);
        }
        while (value > 0);
    }

    private static String ReadUtf8String(Stream stream)
    {
        var len = stream.ReadBytes(2).ToUInt16(0, false);
        if (len == 0) return String.Empty;

        var buf = stream.ReadBytes(len);
        return Encoding.UTF8.GetString(buf);
    }

    private static void WriteUtf8String(Stream stream, String? value)
    {
        var buf = value?.GetBytes() ?? [];
        stream.Write(((UInt16)buf.Length).GetBytes(false));
        if (buf.Length > 0) stream.Write(buf);
    }

    private static Byte[] ReadBinaryData(Stream stream)
    {
        var len = stream.ReadBytes(2).ToUInt16(0, false);
        return len > 0 ? stream.ReadBytes(len) : [];
    }

    private static void WriteBinaryData(Stream stream, Byte[]? data)
    {
        var len = data?.Length ?? 0;
        stream.Write(((UInt16)len).GetBytes(false));
        if (len > 0 && data != null) stream.Write(data);
    }
    #endregion
}
