using System;
using System.ComponentModel;
using NewLife;

namespace NewLife.MQTTServer;

/// <summary>可本地化显示名特性</summary>
/// <remarks>显示名以英文源文为键，读取 <see cref="DisplayNameAttribute.DisplayName"/> 时按当前语言翻译。</remarks>
public class LocalizedDisplayNameAttribute : DisplayNameAttribute
{
    /// <summary>实例化</summary>
    /// <param name="displayName">英文源文</param>
    public LocalizedDisplayNameAttribute(String displayName) : base(displayName) { }

    /// <summary>获取本地化显示名</summary>
    public override String DisplayName => Localization.Translate(base.DisplayName);
}
