using System.Net;
using System.Net.Sockets;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Net;

namespace NewLife.MQTT.ProxyProtocol;

/// <summary>ProxyProtocol协议消息</summary>
public class ProxyMessageV2
{
    #region 属性
    /// <summary>版本</summary>
    public Byte Version { get; set; }

    /// <summary>命令</summary>
    public Byte Command { get; set; }

    /// <summary>客户端</summary>
    public NetUri? Client { get; set; }

    /// <summary>代理端</summary>
    public NetUri? Proxy { get; set; }
    #endregion

    #region 核心读写方法
    private static readonly Byte[] _Magic = "\r\n\r\n\0\r\nQUIT\n".GetBytes();
    private static readonly Byte[] _NewLine = "\r\n".GetBytes();

    /// <summary>快速验证协议头</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Boolean FastValidHeader(ReadOnlySpan<Byte> data) => data.StartsWith(_Magic);

    /// <summary>解析协议</summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public Int32 Read(ReadOnlySpan<Byte> data)
    {
        if (!data.StartsWith(_Magic)) return -1;

        var reader = new SpanReader(data) { IsLittleEndian = false };
        reader.Advance(_Magic.Length);

        var flag = reader.ReadByte();
        Version = (Byte)(flag >> 4);
        Command = (Byte)(flag & 0x0F);

        flag = reader.ReadByte();
        var len = (Int32)reader.ReadUInt16();

        var family = (flag >> 4) switch
        {
            0 => AddressFamily.Unspecified,
            1 => AddressFamily.InterNetwork,
            2 => AddressFamily.InterNetworkV6,
            3 => AddressFamily.Unix,
            _ => AddressFamily.Unspecified,
        };
        var protocol = (flag & 0x0F) switch
        {
            0 => NetType.Unknown,
            1 => NetType.Tcp,
            2 => NetType.Udp,
            _ => NetType.Unknown,
        };

        switch (family)
        {
            case AddressFamily.InterNetwork:
                {
                    var src_addr = reader.ReadBytes(4).ToArray();
                    var dst_addr = reader.ReadBytes(4).ToArray();
                    var src_port = reader.ReadUInt16();
                    var dst_port = reader.ReadUInt16();

                    Client = new NetUri(protocol, new IPAddress(src_addr), src_port);
                    Proxy = new NetUri(protocol, new IPAddress(dst_addr), dst_port);
                }
                break;
            case AddressFamily.InterNetworkV6:
                {
                    var src_addr = reader.ReadBytes(16).ToArray();
                    var dst_addr = reader.ReadBytes(16).ToArray();
                    var src_port = reader.ReadUInt16();
                    var dst_port = reader.ReadUInt16();

                    Client = new NetUri(protocol, new IPAddress(src_addr), src_port);
                    Proxy = new NetUri(protocol, new IPAddress(dst_addr), dst_port);
                }
                break;
            case AddressFamily.Unix:
                {
                    //var src_addr = reader.ReadBytes(16);
                    //var dst_addr = reader.ReadBytes(16);

                    throw new NotSupportedException();
                }
                //break;
        }

        // 后续TLV数据
        len = _Magic.Length + 1 + 1 + 2 + len - reader.Position;
        if (len > 0)
        {
            var vs = reader.ReadBytes(len);
            //todo: 支持解析TLV数据
        }

        return reader.Position;
    }

    /// <summary>转为数据包</summary>
    /// <returns></returns>
    public IPacket ToPacket()
    {
        var pk = new OwnerPacket(256);
        var writer = new SpanWriter(pk.GetSpan());

        writer.Write(_Magic);

        writer.WriteByte((Version << 4) | Command);

        var src = Client;
        var dst = Proxy;
        var flag = 0;
        switch (src!.Address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                flag |= 0x10;
                break;
            case AddressFamily.InterNetworkV6:
                flag |= 0x20;
                break;
            case AddressFamily.Unix:
                flag |= 0x30;
                break;
        }
        switch (src!.Type)
        {
            case NetType.Tcp:
                flag |= 0x01;
                break;
            case NetType.Udp:
                flag |= 0x02;
                break;
        }
        writer.WriteByte(flag);

        switch (src!.Address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                writer.Write(src!.Address.GetAddressBytes());
                writer.Write(dst!.Address.GetAddressBytes());
                writer.Write((UInt16)src.Port);
                writer.Write((UInt16)dst.Port);
                break;
            case AddressFamily.InterNetworkV6:
                writer.Write(src!.Address.GetAddressBytes());
                writer.Write(dst!.Address.GetAddressBytes());
                writer.Write((UInt16)src.Port);
                writer.Write((UInt16)dst.Port);
                break;
            case AddressFamily.Unix:
                break;
        }

        return pk;
    }
    #endregion
}
