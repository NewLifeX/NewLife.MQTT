using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace NewLife.MQTT.Clusters;

/// <summary>订阅关系</summary>
public class SubscriptionInfo
{
    #region 属性
    /// <summary>主题</summary>
    public String Topic { get; set; } = null!;

    /// <summary>节点地址。提供接入服务的集群节点</summary>
    public String Endpoint { get; set; } = null!;

    /// <summary>远程地址。订阅该主题的客户端远程地址</summary>
    public String? RemoteEndpoint { get; set; }

    /// <summary>创建时间</summary>
    [XmlIgnore, IgnoreDataMember]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    [XmlIgnore, IgnoreDataMember]
    public DateTime UpdateTime { get; set; }

    //[XmlIgnore, IgnoreDataMember]
    //public ClusterNode? Node { get; set; }
    #endregion
}
