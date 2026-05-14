using System;
using NewLife;
using NewLife.MQTT;
using Xunit;

namespace XUnitTest.Auth;

/// <summary>MqttSign 阿里云 IoT HMAC 签名计算单元测试（纯本地，无需联网）</summary>
public class MqttSignTests
{
    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：基础签名计算 UserName/Password/ClientId 格式正确")]
    public void Test()
    {
        var productKey = "a1X2bEn****";
        var deviceName = "example1";
        var deviceSecret = "ga7XA6KdlEeiPXQPpRbAjOZXwG8y****";
        //var timestamp = DateTime.UtcNow.ToLong();

        // 计算MQTT连接参数
        var sign = new MqttSign();
        sign.Calculate(productKey, deviceName, deviceSecret);

        Assert.Equal("example1&a1X2bEn****", sign.UserName);
        //Assert.Equal("B144BAE52CF8A5B5F2B37743D8440C85CDC05861A1BF87C564E4FB2CBF43FAFB", sign.Password);
        //Assert.Equal("a1X2bEn****.example1|timestamp=1578321210292,_v=newlife-mqtt-1.0,securemode=2,signmethod=hmacsha256|", sign.ClientId);
        Assert.NotEmpty(sign.Password);
        Assert.Equal(32, sign.Password.ToHex().Length);
        Assert.NotEmpty(sign.ClientId);

        var cs = sign.ClientId.Split('|', ',');
        Assert.Equal(8, cs.Length);
        Assert.Equal($"{productKey}.{deviceName}", cs[0]);
        Assert.StartsWith("_v=newlife-mqtt-", cs[2]);
        Assert.Equal("language=C#", cs[3]);
        Assert.Equal("_m=NewLife.MQTT", cs[4]);
        Assert.Equal("securemode=3", cs[5]);
        Assert.Equal("signmethod=hmacsha256", cs[6]);
        Assert.Empty(cs[7]);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：AliyunMqttClient 构造时签名参数正确")]
    public void Test2()
    {
        var productKey = "a1X2bEn****";
        var deviceName = "example1";
        var deviceSecret = "ga7XA6KdlEeiPXQPpRbAjOZXwG8y****";

        var client = new AliyunMqttClient(productKey, deviceName, deviceSecret);

        Assert.Equal("example1&a1X2bEn****", client.UserName);
        Assert.NotEmpty(client.Password);
        Assert.Equal(32, client.Password.ToHex().Length);
        Assert.NotEmpty(client.ClientId);

        var cs = client.ClientId.Split('|', ',');
        Assert.Equal(8, cs.Length);
        Assert.Equal($"{productKey}.{deviceName}", cs[0]);
        Assert.StartsWith("_v=newlife-mqtt-", cs[2]);
        Assert.Equal("language=C#", cs[3]);
        Assert.Equal("_m=NewLife.MQTT", cs[4]);
        Assert.Equal("securemode=3", cs[5]);
        Assert.Equal("signmethod=hmacsha256", cs[6]);
        Assert.Empty(cs[7]);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：Calculate 返回 true（参数合法）")]
    public void Calculate_ValidParams_ReturnsTrue()
    {
        var sign = new MqttSign();
        var result = sign.Calculate("pk_test", "dn_test", "secret_test");

        Assert.True(result);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：Calculate 任一参数为 null 返回 false")]
    public void Calculate_NullParams_ReturnsFalse()
    {
        var sign = new MqttSign();

        Assert.False(sign.Calculate(null!, "dn", "secret"));
        Assert.False(sign.Calculate("pk", null!, "secret"));
        Assert.False(sign.Calculate("pk", "dn", null!));
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：HmacSha256 静态方法生成 64 字符十六进制结果")]
    public void HmacSha256_Returns64HexChars()
    {
        var result = MqttSign.HmacSha256("hello world", "my_secret_key");

        Assert.Equal(64, result.Length);
        // 只含十六进制字符（大写）
        foreach (var c in result)
            Assert.True((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'), $"非法字符: {c}");
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：HmacSha256 相同输入每次结果一致（确定性）")]
    public void HmacSha256_SameInput_ConsistentOutput()
    {
        var result1 = MqttSign.HmacSha256("test_data", "test_key");
        var result2 = MqttSign.HmacSha256("test_data", "test_key");

        Assert.Equal(result1, result2);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：HmacSha256 不同密钥生成不同结果")]
    public void HmacSha256_DifferentKeys_DifferentOutput()
    {
        var result1 = MqttSign.HmacSha256("same_data", "key1");
        var result2 = MqttSign.HmacSha256("same_data", "key2");

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：SSL=true 时 securemode=2")]
    public void Calculate_SSL_True_SecureMode2()
    {
        var sign = new MqttSign { SSL = true };
        sign.Calculate("pk_ssl", "dn_ssl", "secret_ssl");

        Assert.NotNull(sign.ClientId);
        Assert.Contains("securemode=2", sign.ClientId);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：SSL=false（默认）时 securemode=3")]
    public void Calculate_SSL_False_SecureMode3()
    {
        var sign = new MqttSign();
        sign.Calculate("pk_notls", "dn_notls", "secret_notls");

        Assert.NotNull(sign.ClientId);
        Assert.Contains("securemode=3", sign.ClientId);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：ClientId 中包含 timestamp 字段")]
    public void Calculate_ClientId_ContainsTimestamp()
    {
        var sign = new MqttSign();
        sign.Calculate("pk_ts", "dn_ts", "secret_ts");

        Assert.NotNull(sign.ClientId);
        Assert.Contains("timestamp=", sign.ClientId);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：自定义 Module 和 Version 时 ClientId 中体现")]
    public void Calculate_CustomModuleAndVersion()
    {
        var sign = new MqttSign
        {
            Module = "MyApp",
            Version = "my-sdk-1.0",
        };
        sign.Calculate("pk_custom", "dn_custom", "secret_custom");

        Assert.NotNull(sign.ClientId);
        Assert.Contains("_v=my-sdk-1.0", sign.ClientId);
        Assert.Contains("_m=MyApp", sign.ClientId);
    }

    [Fact]
    [System.ComponentModel.DisplayName("MqttSign：UserName 格式为 deviceName&productKey")]
    public void Calculate_UserName_FormatCheck()
    {
        var sign = new MqttSign();
        sign.Calculate("myPK", "myDN", "mySecret");

        Assert.Equal("myDN&myPK", sign.UserName);
    }
}