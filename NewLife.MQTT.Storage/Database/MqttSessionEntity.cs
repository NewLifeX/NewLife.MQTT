using System.ComponentModel;
using XCode;
using XCode.DataAccessLayer;

namespace NewLife.MQTT.Storage.Database;

/// <summary>持久会话实体</summary>
[BindTable("MqttSession", "持久会话", Description = "MQTT持久会话。保存CleanSession=0客户端的订阅关系")]
public class MqttSessionEntity : Entity<MqttSessionEntity>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, -1)]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _ClientId = null!;
    /// <summary>客户端标识</summary>
    [DisplayName("客户端标识")]
    [Description("客户端标识")]
    [DataObjectField(false, false, true, 200)]
    public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private Int32 _SessionId;
    /// <summary>会话标识</summary>
    [DisplayName("会话标识")]
    [Description("会话标识")]
    [DataObjectField(false, false, false, -1)]
    public Int32 SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String? _Subscriptions;
    /// <summary>订阅关系（格式：topic1:0,topic2:1）</summary>
    [DisplayName("订阅关系")]
    [Description("订阅关系")]
    [DataObjectField(false, false, true, -1)]
    public String? Subscriptions { get => _Subscriptions; set { if (OnPropertyChanging("Subscriptions", value)) { _Subscriptions = value; OnPropertyChanged("Subscriptions"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, -1)]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _ExpireTime;
    /// <summary>过期时间</summary>
    [DisplayName("过期时间")]
    [Description("过期时间")]
    [DataObjectField(false, false, false, -1)]
    public DateTime ExpireTime { get => _ExpireTime; set { if (OnPropertyChanging("ExpireTime", value)) { _ExpireTime = value; OnPropertyChanged("ExpireTime"); } } }
    #endregion

    #region 索引器
    /// <summary>索引器</summary>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "ClientId" => _ClientId,
            "SessionId" => _SessionId,
            "Subscriptions" => _Subscriptions,
            "CreateTime" => _CreateTime,
            "ExpireTime" => _ExpireTime,
            _ => base[name],
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ClientId": _ClientId = (String)value!; break;
                case "SessionId": _SessionId = value.ToInt(); break;
                case "Subscriptions": _Subscriptions = (String?)value; break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "ExpireTime": _ExpireTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>根据客户端标识查找（数据库实时查询）</summary>
    public static MqttSessionEntity? FindByClientId(String clientId) => Find("ClientId", clientId);
    #endregion
}

/// <summary>离线消息实体</summary>
[BindTable("MqttOfflineMessage", "离线消息", Description = "MQTT离线消息。暂存离线持久会话的QoS>0消息")]
public class MqttOfflineMessageEntity : Entity<MqttOfflineMessageEntity>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, -1)]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _ClientId = null!;
    /// <summary>客户端标识</summary>
    [DisplayName("客户端标识")]
    [Description("客户端标识")]
    [DataObjectField(false, false, true, 200)]
    public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private String _Topic = null!;
    /// <summary>主题</summary>
    [DisplayName("主题")]
    [Description("主题")]
    [DataObjectField(false, false, true, 200)]
    public String Topic { get => _Topic; set { if (OnPropertyChanging("Topic", value)) { _Topic = value; OnPropertyChanged("Topic"); } } }

    private Byte[]? _Payload;
    /// <summary>负载</summary>
    [DisplayName("负载")]
    [Description("负载")]
    [DataObjectField(false, false, true, -1)]
    public Byte[]? Payload { get => _Payload; set { if (OnPropertyChanging("Payload", value)) { _Payload = value; OnPropertyChanged("Payload"); } } }

    private Int32 _QoS;
    /// <summary>服务质量</summary>
    [DisplayName("服务质量")]
    [Description("服务质量")]
    [DataObjectField(false, false, false, -1)]
    public Int32 QoS { get => _QoS; set { if (OnPropertyChanging("QoS", value)) { _QoS = value; OnPropertyChanged("QoS"); } } }

    private Boolean _Retain;
    /// <summary>保留标志</summary>
    [DisplayName("保留标志")]
    [Description("保留标志")]
    [DataObjectField(false, false, false, -1)]
    public Boolean Retain { get => _Retain; set { if (OnPropertyChanging("Retain", value)) { _Retain = value; OnPropertyChanged("Retain"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, -1)]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
    #endregion

    #region 索引器
    /// <summary>索引器</summary>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "ClientId" => _ClientId,
            "Topic" => _Topic,
            "Payload" => _Payload,
            "QoS" => _QoS,
            "Retain" => _Retain,
            "CreateTime" => _CreateTime,
            _ => base[name],
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ClientId": _ClientId = (String)value!; break;
                case "Topic": _Topic = (String)value!; break;
                case "Payload": _Payload = (Byte[]?)value; break;
                case "QoS": _QoS = value.ToInt(); break;
                case "Retain": _Retain = value.ToBoolean(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>根据客户端标识查找所有离线消息（数据库实时查询）。转义单引号避免注入</summary>
    /// <remarks>条件交由 Where 子句重载处理；单参 FindAll(String) 是整条 SQL 语义，不可传裸条件</remarks>
    public static IList<MqttOfflineMessageEntity> FindAllByClientId(String clientId) => FindAll($"ClientId='{clientId.Replace("'", "''")}'", null, null, 0, 0);

    /// <summary>删除指定客户端的所有离线消息</summary>
    public static Int32 DeleteByClientId(String clientId) => Delete($"ClientId='{clientId.Replace("'", "''")}'");
    #endregion
}
