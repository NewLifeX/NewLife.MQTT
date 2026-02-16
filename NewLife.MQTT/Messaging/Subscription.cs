using System.Diagnostics.Contracts;

namespace NewLife.MQTT.Messaging;

/// <summary>订阅</summary>
public class Subscription : IEquatable<Subscription>
{
    #region 属性
    /// <summary>主题过滤</summary>
    public String TopicFilter { get; }

    /// <summary>服务质量</summary>
    public QualityOfService QualityOfService { get; }

    /// <summary>不转发自身发布的消息。MQTT 5.0 订阅选项</summary>
    public Boolean NoLocal { get; set; }

    /// <summary>保留消息按发布时的状态转发。MQTT 5.0 订阅选项</summary>
    public Boolean RetainAsPublished { get; set; }

    /// <summary>保留消息处理方式。MQTT 5.0 订阅选项</summary>
    /// <remarks>
    /// 0=订阅时发送保留消息，1=仅新订阅时发送保留消息，2=不发送保留消息
    /// </remarks>
    public Byte RetainHandling { get; set; }

    /// <summary>消息处理方法</summary>
    public Action<PublishMessage>? Callback { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="topicFilter"></param>
    /// <param name="qualityOfService"></param>
    public Subscription(String topicFilter, QualityOfService qualityOfService)
    {
        Contract.Requires(!String.IsNullOrEmpty(topicFilter));

        TopicFilter = topicFilter;
        QualityOfService = qualityOfService;
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => $"{GetType().Name}[TopicFilter={TopicFilter}, QualityOfService={QualityOfService}]";
    #endregion

    #region 辅助
    /// <summary>相等比较</summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public Boolean Equals(Subscription other)
    {
        return QualityOfService == other.QualityOfService
            && TopicFilter.Equals(other.TopicFilter, StringComparison.Ordinal);
    }
    #endregion
}