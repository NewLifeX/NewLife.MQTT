using NewLife.Data;
using XCode;
using XCode.Membership;

namespace NewLife.MQTT.Storage.Database;

/// <summary>持久会话实体</summary>
public class MqttSessionEntity : Entity<MqttSessionEntity>
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>客户端标识</summary>
    public String ClientId { get; set; } = null!;

    /// <summary>会话标识</summary>
    public Int32 SessionId { get; set; }

    /// <summary>订阅关系（格式：topic1:0,topic2:1）</summary>
    public String? Subscriptions { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>过期时间</summary>
    public DateTime ExpireTime { get; set; } = DateTime.Now.AddDays(7);
    #endregion

    #region 方法
    /// <summary>根据客户端标识查找</summary>
    public static MqttSessionEntity? FindByClientId(String clientId) => FindAll().FirstOrDefault(e => e.ClientId == clientId);
    #endregion
}

/// <summary>离线消息实体</summary>
public class MqttOfflineMessageEntity : Entity<MqttOfflineMessageEntity>
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>客户端标识</summary>
    public String ClientId { get; set; } = null!;

    /// <summary>主题</summary>
    public String Topic { get; set; } = null!;

    /// <summary>负载</summary>
    public Byte[]? Payload { get; set; }

    /// <summary>服务质量</summary>
    public Int32 QoS { get; set; }

    /// <summary>保留标志</summary>
    public Boolean Retain { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
    #endregion

    #region 方法
    /// <summary>根据客户端标识查找所有离线消息</summary>
    public static IList<MqttOfflineMessageEntity> FindAllByClientId(String clientId) => FindAll().Where(e => e.ClientId == clientId).ToList();

    /// <summary>删除指定客户端的所有离线消息</summary>
    public static Int32 DeleteByClientId(String clientId) => FindAllByClientId(clientId).Delete();
    #endregion
}
