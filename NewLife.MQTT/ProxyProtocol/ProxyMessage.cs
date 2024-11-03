using System.Net;
using System.Text;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Net;

namespace NewLife.MQTT.ProxyProtocol;

/// <summary>ProxyProtocol协议消息</summary>
public class ProxyMessage
{
    #region 属性
    /// <summary>内部协议。TCP4等</summary>
    public String? Protocol { get; set; }

    /// <summary>客户端IP地址</summary>
    public String? ClientIP { get; set; }

    /// <summary>代理IP地址</summary>
    public String? ProxyIP { get; set; }

    /// <summary>客户端端口</summary>
    public Int32 ClientPort { get; set; }

    /// <summary>代理端口</summary>
    public Int32 ProxyPort { get; set; }
    #endregion

    #region 核心读写方法
    private static readonly Byte[] _Magic = [(Byte)'P', (Byte)'R', (Byte)'O', (Byte)'X', (Byte)'Y', (Byte)' '];
    private static readonly Byte[] _NewLine = [(Byte)'\r', (Byte)'\n'];

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

        var p = _Magic.Length;
        var p2 = data[p..].IndexOf(_NewLine);
        if (p2 <= 0) return -1;

        data = data[..(p + p2)];
        var ss = Encoding.ASCII.GetString(data).Split(' ');
        if (ss == null || ss.Length < 6) return -1;

        Protocol = ss[1];
        ClientIP = ss[2];
        ProxyIP = ss[3];
        ClientPort = ss[4].ToInt();
        ProxyPort = ss[5].ToInt();

        return p + p2 + _NewLine.Length;
    }

    /// <summary>转为数据包</summary>
    /// <returns></returns>
    public String ToPacket()
    {
        if (Protocol.IsNullOrEmpty()) Protocol = "TCP4";

        var sb = Pool.StringBuilder.Get();
        sb.Append("PROXY ");
        sb.Append(Protocol);
        sb.Append(' ');
        sb.Append(ClientIP);
        sb.Append(' ');
        sb.Append(ProxyIP);
        sb.Append(' ');
        sb.Append(ClientPort);
        sb.Append(' ');
        sb.Append(ProxyPort);
        sb.Append("\r\n");

        return sb.Return(true);
    }

    /// <summary>获取客户端结点</summary>
    /// <returns></returns>
    public NetUri GetClient()
    {
        var uri = new NetUri
        {
            Host = ClientIP,
            Port = ClientPort
        };

        return uri;
    }
    #endregion
}
