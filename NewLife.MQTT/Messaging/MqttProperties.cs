using NewLife.Buffers;
using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.MQTT.Messaging;

/// <summary>MQTT 5.0 属性集合</summary>
/// <remarks>
/// MQTT 5.0 在可变报头中引入属性（Properties）系统。
/// 每个属性由属性标识符（1字节）和对应类型的值组成。
/// 属性值类型包括：Byte、UInt16、UInt32、变长整数、UTF-8字符串、二进制数据、UTF-8字符串对。
/// 用户属性（UserProperty）可以出现多次。
/// </remarks>
public class MqttProperties : ISpanSerializable
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
    /// <summary>从SpanReader读取属性集合</summary>
    /// <param name="reader">Span读取器</param>
    /// <returns></returns>
    public void Read(ref SpanReader reader)
    {
        // 读取属性总长度（变长整数编码）
        var totalLen = reader.ReadEncodedInt();
        if (totalLen <= 0) return;

        var startPos = reader.Position;
        while (reader.Position - startPos < totalLen)
        {
            var idByte = reader.ReadByte();
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
                    _props[id] = reader.ReadByte();
                    break;

                // UInt16 类型
                case MqttPropertyId.ServerKeepAlive:
                case MqttPropertyId.ReceiveMaximum:
                case MqttPropertyId.TopicAliasMaximum:
                case MqttPropertyId.TopicAlias:
                    _props[id] = reader.ReadUInt16();
                    break;

                // UInt32 类型
                case MqttPropertyId.MessageExpiryInterval:
                case MqttPropertyId.SessionExpiryInterval:
                case MqttPropertyId.WillDelayInterval:
                case MqttPropertyId.MaximumPacketSize:
                    _props[id] = reader.ReadUInt32();
                    break;

                // 变长整数类型
                case MqttPropertyId.SubscriptionIdentifier:
                    _props[id] = reader.ReadEncodedInt();
                    break;

                // UTF-8 字符串类型
                case MqttPropertyId.ContentType:
                case MqttPropertyId.ResponseTopic:
                case MqttPropertyId.AssignedClientIdentifier:
                case MqttPropertyId.AuthenticationMethod:
                case MqttPropertyId.ResponseInformation:
                case MqttPropertyId.ServerReference:
                case MqttPropertyId.ReasonString:
                    _props[id] = reader.ReadLengthString(2);
                    break;

                // 二进制数据类型
                case MqttPropertyId.CorrelationData:
                case MqttPropertyId.AuthenticationData:
                    _props[id] = reader.ReadArray(2).ToArray();
                    break;

                // UTF-8 字符串对（用户属性，可出现多次）
                case MqttPropertyId.UserProperty:
                    var key = reader.ReadLengthString(2);
                    var value = reader.ReadLengthString(2);
                    UserProperties.Add(new KeyValuePair<String, String>(key, value));
                    break;

                default:
                    // 未知属性，跳到结尾避免解析异常
                    reader.Advance(totalLen - (reader.Position - startPos));
                    break;
            }
        }
    }

    /// <summary>写入属性集合到SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <returns></returns>
    public void Write(ref SpanWriter writer)
    {
        // 使用池化缓冲区写入属性数据，避免 GC 分配
        using var propPk = new OwnerPacket(GetEstimatedSize());
        var propWriter = new SpanWriter(propPk) { IsLittleEndian = false };

        foreach (var item in _props)
        {
            propWriter.WriteByte((Byte)item.Key);

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
                    propWriter.WriteByte((Byte)item.Value);
                    break;

                // UInt16 类型
                case MqttPropertyId.ServerKeepAlive:
                case MqttPropertyId.ReceiveMaximum:
                case MqttPropertyId.TopicAliasMaximum:
                case MqttPropertyId.TopicAlias:
                    propWriter.Write((UInt16)item.Value);
                    break;

                // UInt32 类型
                case MqttPropertyId.MessageExpiryInterval:
                case MqttPropertyId.SessionExpiryInterval:
                case MqttPropertyId.WillDelayInterval:
                case MqttPropertyId.MaximumPacketSize:
                    propWriter.Write((UInt32)item.Value);
                    break;

                // 变长整数类型
                case MqttPropertyId.SubscriptionIdentifier:
                    propWriter.WriteEncodedInt((Int32)item.Value);
                    break;

                // UTF-8 字符串类型
                case MqttPropertyId.ContentType:
                case MqttPropertyId.ResponseTopic:
                case MqttPropertyId.AssignedClientIdentifier:
                case MqttPropertyId.AuthenticationMethod:
                case MqttPropertyId.ResponseInformation:
                case MqttPropertyId.ServerReference:
                case MqttPropertyId.ReasonString:
                    propWriter.WriteLengthString((String)item.Value, 2);
                    break;

                // 二进制数据类型
                case MqttPropertyId.CorrelationData:
                case MqttPropertyId.AuthenticationData:
                    propWriter.WriteArray((Byte[])item.Value, 2);
                    break;
            }
        }

        // 写入用户属性（可出现多次）
        foreach (var item in UserProperties)
        {
            propWriter.WriteByte((Byte)MqttPropertyId.UserProperty);
            propWriter.WriteLengthString(item.Key, 2);
            propWriter.WriteLengthString(item.Value, 2);
        }

        // 写入属性总长度（变长整数编码）+ 属性数据
        writer.WriteEncodedInt(propWriter.WrittenCount);
        if (propWriter.WrittenCount > 0) writer.Write(propWriter.WrittenSpan);
    }

    /// <summary>获取属性估算大小</summary>
    /// <returns></returns>
    public Int32 GetEstimatedSize()
    {
        var size = 64;
        foreach (var item in _props)
        {
            size += 1; // id byte
            if (item.Value is String s)
                size += 2 + s.Length * 3;
            else if (item.Value is Byte[])
                size += 2 + ((Byte[])item.Value).Length;
            else
                size += 5;
        }
        foreach (var item in UserProperties)
        {
            size += 1 + 2 + item.Key.Length * 3 + 2 + item.Value.Length * 3;
        }
        return size;
    }
    #endregion
}
