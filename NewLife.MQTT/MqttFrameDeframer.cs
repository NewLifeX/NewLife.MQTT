#nullable enable
using System.Buffers;
using NewLife.Data;
using NewLife.Messaging;

namespace NewLife.MQTT;

/// <summary>WebSocket 负载的 MQTT 帧定界器</summary>
/// <remarks>
/// <para>一条 WebSocket 消息可能承载多个 MQTT 帧，也可能只承载半个帧。残余字节必须保留到下一轮拼接后再定界，
/// 否则跨消息边界的帧会被整体丢弃（发送方分批发送大报文时表现为对端收不到任何响应）。</para>
/// <para>每条连接持有独立实例，与连接同生命周期。</para>
/// </remarks>
internal sealed class MqttFrameDeframer
{
    /// <summary>单个未完整帧的残余长度上限。超过该值说明对端帧异常巨大或数据已错乱，调用方应断开连接</summary>
    public const Int32 MaxResidualSize = 16 * 1024 * 1024;

    private Byte[] _residual = [];
    private Int32 _length;

    /// <summary>本轮是否出现残余超限，调用方应断开连接</summary>
    public Boolean Overflow { get; private set; }

    /// <summary>清空残余缓冲</summary>
    public void Reset()
    {
        _length = 0;
        Overflow = false;
    }

    /// <summary>定界一轮 WebSocket 负载，逐帧回调完整帧</summary>
    /// <param name="codec">MQTT 帧编解码器</param>
    /// <param name="payload">本轮 WebSocket 二进制负载</param>
    /// <param name="handler">完整帧回调。帧体已指向本轮数据，回调返回后不得跨轮持有</param>
    /// <returns>是否正常定界。false 表示数据损坏或残余超限，调用方应丢弃本轮并停止处理</returns>
    public Boolean Process(MqttCodec codec, IPacket payload, Action<IMessage> handler)
    {
        Overflow = false;

        var incoming = payload.ToArray();
        var offset = 0;
        Byte[] bytes;

        // 与上一轮残余拼接，使跨消息边界的帧可见
        if (_length > 0)
        {
            bytes = new Byte[_length + incoming.Length];
            Array.Copy(_residual, 0, bytes, 0, _length);
            Array.Copy(incoming, 0, bytes, _length, incoming.Length);
        }
        else
        {
            bytes = incoming;
        }
        _length = 0;

        while (offset < bytes.Length)
        {
            var rs = codec.TryParse(new ReadOnlySequence<Byte>(bytes, offset, bytes.Length - offset));
            if (rs == null) break;

            var r = rs.Value;
            if (r.Invalid || r.Message == null)
            {
                // 数据损坏后无法重新定位帧边界，丢弃全部残余
                r.Message.TryDispose();
                return false;
            }

            var total = r.HeaderSize + r.BodyLength;
            if (total <= 0 || offset + total > bytes.Length)
            {
                // 帧未完整，整帧留到下一轮
                r.Message.TryDispose();
                break;
            }

            r.Message.SetBody(new ArrayPacket(bytes, offset + (Int32)r.HeaderSize, (Int32)r.BodyLength));
            offset += (Int32)total;

            handler(r.Message);
        }

        var left = bytes.Length - offset;
        if (left > MaxResidualSize)
        {
            Overflow = true;
            return false;
        }

        if (left > 0)
        {
            if (_residual.Length < left) _residual = new Byte[left];
            Array.Copy(bytes, offset, _residual, 0, left);
            _length = left;
        }

        return true;
    }
}
