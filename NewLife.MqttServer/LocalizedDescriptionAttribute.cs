using System;
using System.ComponentModel;
using NewLife;

namespace NewLife.MQTTServer;

/// <summary>可本地化描述特性</summary>
/// <remarks>描述文本以英文源文为键，读取 <see cref="DescriptionAttribute.Description"/> 时按当前语言翻译。</remarks>
public class LocalizedDescriptionAttribute : DescriptionAttribute
{
    /// <summary>实例化</summary>
    /// <param name="description">英文源文</param>
    public LocalizedDescriptionAttribute(String description) : base(description) { }

    /// <summary>获取本地化描述</summary>
    public override String Description => Localization.Translate(base.Description);
}
