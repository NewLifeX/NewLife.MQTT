using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace NewLife.MQTT;

/// <summary>用于计算设备接入物联网平台的MQTT连接参数username、password和clientid</summary>
/// <remarks>
/// 来自阿里云手册，https://help.aliyun.com/document_detail/146505.html?spm=a2c4g.11186623.2.26.11a22cf0eJS5xw#task-2360906
/// </remarks>
public class MqttSign
{
    /// <summary>用户名</summary>
    public String? UserName { get; private set; }

    /// <summary>密码</summary>
    public String? Password { get; private set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; private set; }

    /// <summary>模块</summary>
    public String? Module { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>使用SSL</summary>
    public Boolean SSL { get; set; }

    /// <summary>计算MQTT参数</summary>
    /// <param name="productKey"></param>
    /// <param name="deviceName"></param>
    /// <param name="deviceSecret"></param>
    /// <returns></returns>
    public Boolean Calculate(String productKey, String deviceName, String deviceSecret)
    {
        if (productKey == null || deviceName == null || deviceSecret == null) return false;

        //MQTT用户名
        UserName = $"{deviceName}&{productKey}";

        //MQTT密码
        var timestamp = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString();
        // 这里deviceName是不是重复了？
        var plainPasswd = $"clientId{productKey}.{deviceName}deviceName{deviceName}productKey{productKey}timestamp{timestamp}";
        Password = HmacSha256(plainPasswd, deviceSecret);

        var asm = Assembly.GetEntryAssembly();
        if (asm == null || asm.GetName().Name.StartsWith("testhost")) asm = Assembly.GetExecutingAssembly();
        if (asm != null)
        {
            if (Module.IsNullOrEmpty()) Module = asm.GetName().Name;
            if (Version.IsNullOrEmpty())
            {
                var ver = asm.GetName().Version;
                Version = $"newlife-mqtt-{ver.Major}.{ver.Minor}";
            }
        }

        //MQTT ClientId
        var mode = SSL ? 2 : 3;
        ClientId = $"{productKey}.{deviceName}|timestamp={timestamp},_v={Version},language=C#,_m={Module},securemode={mode},signmethod=hmacsha256|";

        return true;
    }

    /// <summary>计算SHA256</summary>
    /// <param name="plainText"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static String HmacSha256(String plainText, String key)
    {
        var encoding = new UTF8Encoding();
        var plainTextBytes = encoding.GetBytes(plainText);
        var keyBytes = encoding.GetBytes(key);

        var hmac = new HMACSHA256(keyBytes);
        var sign = hmac.ComputeHash(plainTextBytes);
        return BitConverter.ToString(sign).Replace("-", String.Empty);
    }
}