using NewLife.Http;

namespace NewLife.MQTT.Handlers;

/// <summary>Http 请求令牌读取</summary>
internal static class HttpTokenHelper
{
    /// <summary>取请求令牌。依次尝试 x-token 头、Authorization 头（Bearer 前缀自动剥离）与查询参数 token</summary>
    /// <param name="context">Http 上下文</param>
    /// <returns></returns>
    public static String? GetToken(IHttpContext context)
    {
        var value = context.Request.Headers["x-token"];
        if (value.IsNullOrEmpty()) value = context.Request.Headers["Authorization"];
        if (!value.IsNullOrEmpty() && value.StartsWithIgnoreCase("Bearer ")) value = value[7..];

        if (value.IsNullOrEmpty() && context.Parameters.TryGetValue("token", out var arg)) value = arg as String;

        return value;
    }
}
