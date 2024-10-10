using NewLife.Data;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.MQTT.Messaging;
using NewLife.Net.Handlers;

namespace NewLife.MQTT;

/// <summary>编码器</summary>
public class MqttCodec : MessageCodec<MqttMessage>
{
    private readonly MqttFactory _Factory = new();

    /// <summary>实例化编码器</summary>
    public MqttCodec() => UserPacket = false;

    /// <summary>编码</summary>
    /// <param name="context"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    protected override Object? Encode(IHandlerContext context, MqttMessage msg)
    {
        if (msg is MqttMessage cmd) return cmd.ToPacket();

        return null;
    }

    /// <summary>加入队列</summary>
    /// <param name="context"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    protected override void AddToQueue(IHandlerContext context, MqttMessage msg)
    {
        if (!msg.Reply) base.AddToQueue(context, msg);
    }

    /// <summary>解码</summary>
    /// <param name="context"></param>
    /// <param name="pk"></param>
    /// <returns></returns>
    protected override IList<MqttMessage> Decode(IHandlerContext context, IPacket pk)
    {
        if (context.Owner is not IExtend ss) return [];

        if (ss["Codec"] is not PacketCodec pc)
            ss["Codec"] = pc = new PacketCodec { GetLength = p => GetLength(p, 1, 0), Offset = 1 };

        var pks = pc.Parse(pk);
        //var list = pks.Select(_Factory.ReadMessage).ToList();
        var list = new List<MqttMessage>();
        foreach (var item in pks)
        {
            var msg = _Factory.ReadMessage(item);
            if (msg != null) list.Add(msg);
        }

        return list;
    }

    /// <summary>连接关闭时，清空粘包编码器</summary>
    /// <param name="context"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend ss) ss["Codec"] = null;

        return base.Close(context, reason);
    }

    /// <summary>是否匹配响应</summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <returns></returns>
    protected override Boolean IsMatch(Object? request, Object? response)
    {
        if (request is not MqttMessage req || response is not MqttMessage res) return false;

        // 请求响应前后配对
        if (req.Reply || !res.Reply) return false;

        // 要求Id匹配
        if (request is not MqttIdMessage req2 ||
            response is not MqttIdMessage res2 ||
            req2.Id == res2.Id)
        {
            if (req.Type + 1 == res.Type) return true;

            // 特殊处理Public
            if (req.Type == MqttType.Publish && (res.Type == MqttType.PubAck || res.Type == MqttType.PubRec)) return true;
        }

        return false;
    }
}