// NewLife.MQTT AOT 冒烟：覆盖主链路（TCP/WS 收发、管理 API、签名、JSON、配置、看板）与认证/授权/保留/离线/SN 场景，逐场景 PASS/FAIL。
// 用法：dotnet run -c Release（JIT 基线）；dotnet publish -c Release -r win-x64（NativeAOT 真编译后运行产物）。
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Configuration;
using NewLife.Data;
using NewLife.Http;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Clusters;
using NewLife.MQTT.Handlers;
using NewLife.MQTT.Messaging;
using NewLife.MQTT.MqttSn;
using NewLife.Serialization;

var failures = new List<String>();
void Check(String name, Boolean ok)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
    if (!ok) failures.Add(name);
}

Console.WriteLine($"IsDynamicCodeSupported={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"Assembly.Location={typeof(MqttServer).Assembly.Location}");

// 0. 本地化：编译期语言表与程序集元数据在 AOT 下可用
try
{
    var prev = Localization.Culture;
    Localization.Culture = new System.Globalization.CultureInfo("zh-Hans");
    var zh = Localization.Translate("Connecting to [{0}]", "tcp://localhost:1883");
    Localization.Culture = new System.Globalization.CultureInfo("de");
    var de = Localization.Translate("NewLife.MQTT - MQTT Protocol Library");
    Localization.Culture = prev;

    Check("本地化：运行时消息与程序集元数据在 AOT 下可用", zh == "正在连接[tcp://localhost:1883]" && de == "NewLife.MQTT - MQTT-Protokollbibliothek");
}
catch (Exception ex) { Check($"本地化（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 1. TCP 回环：连接/订阅/发布/接收
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    using var client = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_tcp", Timeout = 5000, Reconnect = false };
    await client.ConnectAsync();
    await client.SubscribeAsync("aot/tcp");

    var tcs = new TaskCompletionSource<String?>();
    client.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());

    await client.PublishAsync("aot/tcp", "hello");
    var ok = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task && tcs.Task.Result == "hello";

    await client.DisconnectAsync();
    Check("TCP 回环：连接/订阅/发布/接收", ok);
}
catch (Exception ex) { Check($"TCP 回环（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 2. WebSocket 桥接
try
{
    using var server = new MqttServer { Port = 0, EnableWebSocket = true };
    server.Start();

    using var client = new MqttClient { Server = $"ws://127.0.0.1:{server.WebSocketPort}{server.WebSocketPath}", ClientId = "aot_ws", Timeout = 5000, Reconnect = false };
    await client.ConnectAsync();
    await client.SubscribeAsync("aot/ws");

    var tcs = new TaskCompletionSource<String?>();
    client.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());

    await client.PublishAsync("aot/ws", "ws-hello");
    var ok = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task && tcs.Task.Result == "ws-hello";

    await client.DisconnectAsync();
    Check("WebSocket 桥接：连接/订阅/发布/接收", ok);
}
catch (Exception ex) { Check($"WebSocket 桥接（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 3. 管理 API（手工 ControllerHandler 反射分发路径）
try
{
    using var mgmt = new MqttManagementServer { Port = 0, Exchange = new MqttExchange() };
    mgmt.Start();

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var body = await http.GetStringAsync($"http://127.0.0.1:{mgmt.Port}/Api/Stats");

    var head = body.Length > 120 ? body[..120] : body;
    Check($"管理 API：/Api/Stats 返回统计 JSON（body前120字符=[{head}]）", body.Contains("ConnectedClients"));

    mgmt.Stop("done");
}
catch (Exception ex) { Check($"管理 API（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 4. MqttSign 签名计算（Assembly 元数据路径）
try
{
    var sign = new MqttSign();
    var ok = sign.Calculate("pk0001", "dev0001", "secret0001");
    Check("MqttSign：HMAC 签名计算", ok && !sign.Password.IsNullOrEmpty() && !sign.ClientId.IsNullOrEmpty());
}
catch (Exception ex) { Check($"MqttSign（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 5. JSON 序列化（JsonHelper 成员访问路径）
try
{
    var msg = new NewLife.MQTT.Messaging.ConnAck { ReturnCode = 0 };
    var json = msg.ToJson();
    var back = json.ToJsonEntity<NewLife.MQTT.Messaging.ConnAck>();
    Check("JSON：ToJson/ToJsonEntity 往返", !json.IsNullOrEmpty() && back != null);
}
catch (Exception ex) { Check($"JSON（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 6. 配置绑定（Config<T> 源生成器路径）
try
{
    var value = SmokeConfig.Current.Value;
    Check($"配置：Config<T> 绑定（Value={value}）", value == 42);
}
catch (Exception ex) { Check($"配置（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 7. 看板（HttpListener 路径）
try
{
    using var mgmt = new MqttManagementServer { Port = 0, Exchange = new MqttExchange(), EnableDashboard = true };
    mgmt.Start();
    await Task.Delay(500);

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var html = await http.GetStringAsync($"http://localhost:{mgmt.Dashboard!.Port}/");
    Check("看板：HttpListener 提供 HTML", !html.IsNullOrEmpty());

    mgmt.Stop("done");
}
catch (Exception ex) { Check($"看板（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 8. 控制器静态分发（MapController<T> 源生成器路径）：AOT 下应可用
try
{
    var container = new ObjectContainer();
    container.Register(typeof(AotControllerA), null, new AotControllerA());
    using var server = new HttpServer { Port = 0, ServiceProvider = container.BuildServiceProvider() };
    server.MapController<AotControllerA>("/a");
    server.Start();

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var text = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/a/Echo?msg=generated");
    Check("控制器分发：MapController<T> 源生成静态分发", text.Contains("generated"));
}
catch (Exception ex) { Check($"控制器分发：MapController<T>（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 9. 手工 ControllerHandler + DynamicDependency 显式保根：AOT 下应可用
try
{
    RootControllerB();

    var container = new ObjectContainer();
    container.Register(typeof(AotControllerB), null, new AotControllerB());
    using var server = new HttpServer { Port = 0, ServiceProvider = container.BuildServiceProvider() };
    server.Map("/b/*", new ControllerHandler { ControllerType = typeof(AotControllerB), Path = "/b/" });
    server.Start();

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var text = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/b/Echo?msg=rooted");
    Check("控制器分发：手工注册 + DynamicDependency 保根", text.Contains("rooted"));
}
catch (Exception ex) { Check($"控制器分发：手工注册 + DynamicDependency（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 10. 对照（仅记录现象）：手工注册、无保根 —— 再现 MQTT 管理 API 在 AOT 下的 404
try
{
    var container = new ObjectContainer();
    container.Register(typeof(AotControllerC), null, new AotControllerC());
    using var server = new HttpServer { Port = 0, ServiceProvider = container.BuildServiceProvider() };
    server.Map("/c/*", new ControllerHandler { ControllerType = typeof(AotControllerC), Path = "/c/" });
    server.Start();

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    try
    {
        var text = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/c/Echo?msg=noroot");
        Console.WriteLine($"[INFO] 对照（无保根）：本次运行可用，返回 [{text}]");
    }
    catch (HttpRequestException hex) when (hex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine("[INFO] 对照（无保根）：404（方法元数据被裁剪），与 MQTT 管理 API 现象一致");
    }
}
catch (Exception ex) { Console.WriteLine($"[INFO] 对照（无保根）异常：{ex.GetType().Name} {ex.Message}"); }

// 11. 集群 RPC：HTTP 控制器分发 + NodeInfo/SessionInfo DTO 序列化
try
{
    using var server = new ClusterServer { Name = "aot_cluster", Port = 0 };
    using var exchange = new ClusterExchange { Cluster = server };
    server.ClusterExchange = exchange;
    server.Start();

    using var node = new ClusterNode { EndPoint = $"127.0.0.1:{server.Port}" };
    var echo = await node.Echo("cluster-hello");

    var myNode = new NodeInfo { EndPoint = "127.0.0.2:2883" };
    var joined = await node.Join(myNode);
    var registered = server.Nodes.Count;
    var pong = await node.Ping(myNode);
    var left = await node.Leave(myNode);
    var session = await node.GetSession("none");

    // 订阅/退订：数组参数绑定往返（FIX-CLUS-1 修复后应通过）
    var subAck = await node.Subscribe([new SubscriptionInfo { Topic = "aot/cluster", ClientId = "c1", Endpoint = "127.0.0.1:10001" }]);
    var unsubAck = await node.Unsubscribe([new SubscriptionInfo { Topic = "aot/cluster", ClientId = "c1", Endpoint = "127.0.0.1:10001" }]);

    // 健康检查：字典返回值序列化
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var health = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/Cluster/Health");

    // 发布载荷绑定与保真：PublishInfo/PublishMessage 经 RPC 往返（含 QoS/Payload），服务端以记录型交换机观测
    var recorder = new RecordingExchange();
    server.Exchange = recorder;
    var pubAck = await node.Publish(new PublishInfo { Message = new PublishMessage { Topic = "aot/pub", Payload = (ArrayPacket)"aot-pub"u8.ToArray(), QoS = QualityOfService.AtLeastOnce } });
    var got = recorder.LastPublished;
    var pubOk = pubAck == "OK" && got != null && got.Topic == "aot/pub" && got.QoS == QualityOfService.AtLeastOnce && got.Payload?.ToStr() == "aot-pub";
    Check($"集群发布：PublishInfo/PublishMessage 载荷往返（ack=[{pubAck}], topic=[{got?.Topic}], qos=[{got?.QoS}], payload=[{got?.Payload?.ToStr()}]）", pubOk);

    var ok = echo == "cluster-hello" && joined != null && registered == 1 && pong != null && left == "OK" && session == null
             && subAck == "OK" && unsubAck == "OK" && health.Contains("NodeId");
    Check($"集群 RPC：Echo/Join/Ping/Leave/GetSession/Subscribe/Health（echo=[{echo}], joined={joined != null}, 节点数={registered}, pong={(pong != null ? "非空" : "null")}, left=[{left}], sub=[{subAck}], unsub=[{unsubAck}], health含NodeId={health.Contains("NodeId")}）", ok);

    server.Stop("done");
}
catch (Exception ex) { Check($"集群 RPC（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 13. 大载荷跨轮帧体：载荷超过接收缓冲（默认 8KB）时帧体跨多轮到达，必要时由多段共享切片组成链式包
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    using var sub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_big_sub", Timeout = 15000, Reconnect = false };
    using var pub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_big_pub", Timeout = 15000, Reconnect = false };
    await sub.ConnectAsync();
    await pub.ConnectAsync();

    var topic = "aot/big";
    await sub.SubscribeAsync(topic);
    await Task.Delay(200);

    var allOk = true;
    foreach (var size in new[] { 20_000, 200_000 })
    {
        var payload = new Byte[size];
        for (var i = 0; i < size; i++) payload[i] = (Byte)(i * 31 + size);

        var tcs = new TaskCompletionSource<Byte[]>();
        sub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToArray() ?? []);
        await pub.PublishAsync(topic, new ArrayPacket(payload), QualityOfService.AtMostOnce);

        var got = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task ? tcs.Task.Result : [];
        var same = got.Length == size;
        if (same)
        {
            for (var i = 0; i < size && same; i++) same = got[i] == payload[i];
        }
        if (!same) allOk = false;
        Console.WriteLine($"[INFO] 大载荷 {size}：收到 {got.Length} 字节，逐字节一致={same}");
    }

    Check("大载荷跨轮帧体：20KB/200KB 逐字节完整往返", allOk);
}
catch (Exception ex) { Check($"大载荷跨轮帧体（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 14. 断线重连 + 自动重订阅：验证 Core v12 会话一次性契约下客户端重建会话与订阅恢复
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    using var client = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_reconnect", Timeout = 10000, Reconnect = true };
    var connects = 0;
    var reconnected = new TaskCompletionSource<Boolean>();
    client.Connected += (s, e) =>
    {
        if (Interlocked.Increment(ref connects) >= 2) reconnected.TrySetResult(true);
    };

    await client.ConnectAsync();
    await client.SubscribeAsync("aot/reconnect");
    await Task.Delay(200);

    // 服务端强制断开该客户端，客户端应自动重连并恢复订阅
    var kicked = (server.Exchange as MqttExchange)?.DisconnectClient("aot_reconnect", "AOT 冒烟强制断开") ?? false;
    var back = await Task.WhenAny(reconnected.Task, Task.Delay(10000)) == reconnected.Task;
    await Task.Delay(500);

    using var pub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_reconnect_pub", Timeout = 10000, Reconnect = false };
    await pub.ConnectAsync();
    var got2 = new TaskCompletionSource<String?>();
    client.Received += (s, e) => got2.TrySetResult(e.Arg.Payload?.ToStr());
    await pub.PublishAsync("aot/reconnect", "after-reconnect");
    var resubscribed = await Task.WhenAny(got2.Task, Task.Delay(10000)) == got2.Task && got2.Task.Result == "after-reconnect";

    Check($"断线重连 + 自动重订阅（连接次数={connects}，服务端断开={kicked}，重连完成={back}，重订阅送达={resubscribed}）", back && resubscribed);
}
catch (Exception ex) { Check($"断线重连（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 15. JWT 认证（HS256 签名 + Claims 解析 + 主题级授权）
try
{
    const String secret = "aot_smoke_jwt_secret_0123456789";
    using var server = new MqttServer { Port = 0, Authenticator = new MqttJwtAuthenticator(secret) };
    server.Start();

    var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    var token = MakeJwt(secret, $"{{\"sub\":\"aot_jwt\",\"pub\":\"aot/#\",\"mqtt_sub\":\"aot/#\",\"exp\":{exp}}}");

    using var client = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_jwt", UserName = "aot_jwt", Password = token, Timeout = 5000, Reconnect = false };
    var ack = await client.ConnectAsync();
    var accepted = ack.ReturnCode == ConnectReturnCode.Accepted;

    var tcs = new TaskCompletionSource<String?>();
    client.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());
    await client.SubscribeAsync("aot/jwt");
    await client.PublishAsync("aot/jwt", "jwt-ok");
    var received = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task && tcs.Task.Result == "jwt-ok";

    // 篡改载荷后签名校验失败，连接应被拒绝
    var parts = token.Split('.');
    var badToken = $"{parts[0]}.{Base64Url(Encoding.UTF8.GetBytes("{\"sub\":\"hacker\"}"))}.{parts[2]}";
    var refused = false;
    using (var bad = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_jwt_bad", UserName = "aot_jwt_bad", Password = badToken, Timeout = 5000, Reconnect = false })
    {
        try { await bad.ConnectAsync(); } catch { refused = true; }
    }

    Check($"JWT 认证：有效令牌连接并收发（accepted={accepted}, received={received}），篡改令牌被拒={refused}", accepted && received && refused);

    await client.DisconnectAsync();
}
catch (Exception ex) { Check($"JWT 认证（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 16. OAuth 认证（HS256 令牌 + Claims/Scope 主题级授权）
try
{
    const String secret = "aot_smoke_oauth_secret_9876543210";
    using var server = new MqttServer { Port = 0, Authenticator = new MqttOAuthAuthenticator(secret) };
    server.Start();

    var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    var token = MakeJwt(secret, $"{{\"sub\":\"aot_oauth\",\"pub\":\"aot/#\",\"mqtt_sub\":\"aot/#\",\"scope\":\"mqtt:*\",\"exp\":{exp}}}");

    using var client = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_oauth", UserName = "aot_oauth", Password = token, Timeout = 5000, Reconnect = false };
    var ack = await client.ConnectAsync();
    var accepted = ack.ReturnCode == ConnectReturnCode.Accepted;

    var tcs = new TaskCompletionSource<String?>();
    client.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());
    await client.SubscribeAsync("aot/oauth");
    await client.PublishAsync("aot/oauth", "oauth-ok");
    var received = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task && tcs.Task.Result == "oauth-ok";

    // 错误密钥签发的令牌应被拒绝
    var refused = false;
    var badToken = MakeJwt("wrong_secret", $"{{\"sub\":\"aot_oauth_bad\",\"exp\":{exp}}}");
    using (var bad = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_oauth_bad", UserName = "aot_oauth_bad", Password = badToken, Timeout = 5000, Reconnect = false })
    {
        try { await bad.ConnectAsync(); } catch { refused = true; }
    }

    Check($"OAuth 认证：有效令牌连接并收发（accepted={accepted}, received={received}），错误密钥被拒={refused}", accepted && received && refused);

    await client.DisconnectAsync();
}
catch (Exception ex) { Check($"OAuth 认证（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 17. 保留消息：新订阅即刻下发、清除后不再下发
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    using (var pub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_retain_pub", Timeout = 5000, Reconnect = false })
    {
        await pub.ConnectAsync();
        await pub.PublishAsync(new PublishMessage { Topic = "aot/retain", Payload = (ArrayPacket)"retained-value"u8.ToArray(), QoS = QualityOfService.AtMostOnce, Retain = true });
        await Task.Delay(300);
        await pub.DisconnectAsync();
    }

    using var sub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_retain_sub", Timeout = 5000, Reconnect = false };
    var got = new TaskCompletionSource<String?>();
    sub.Received += (s, e) => got.TrySetResult(e.Arg.Payload?.ToStr());
    await sub.ConnectAsync();
    await sub.SubscribeAsync("aot/retain");
    var gotRetained = await Task.WhenAny(got.Task, Task.Delay(5000)) == got.Task && got.Task.Result == "retained-value";

    // 空负载清除保留消息；随后新订阅者不应再收到
    using (var pub2 = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_retain_pub2", Timeout = 5000, Reconnect = false })
    {
        await pub2.ConnectAsync();
        await pub2.PublishAsync(new PublishMessage { Topic = "aot/retain", Payload = (ArrayPacket)Array.Empty<Byte>(), QoS = QualityOfService.AtMostOnce, Retain = true });
        await Task.Delay(300);
        await pub2.DisconnectAsync();
    }

    using var sub2 = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_retain_sub2", Timeout = 5000, Reconnect = false };
    var got2 = new TaskCompletionSource<String?>();
    sub2.Received += (s, e) => got2.TrySetResult(e.Arg.Payload?.ToStr());
    await sub2.ConnectAsync();
    await sub2.SubscribeAsync("aot/retain");
    var cleared = await Task.WhenAny(got2.Task, Task.Delay(1000)) != got2.Task;

    Check($"保留消息：订阅即得={gotRetained}，清除后不再下发={cleared}", gotRetained && cleared);

    await sub.DisconnectAsync();
    await sub2.DisconnectAsync();
}
catch (Exception ex) { Check($"保留消息（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 18. 持久会话离线消息补发（CleanSession=false + QoS1）
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    const String clientId = "aot_offline_client";
    const String topic = "aot/offline";

    using (var c1 = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = clientId, CleanSession = false, Timeout = 5000, Reconnect = false })
    {
        await c1.ConnectAsync();
        await c1.SubscribeAsync([topic], QualityOfService.AtLeastOnce);
        await c1.DisconnectAsync();
    }
    await Task.Delay(300);

    using (var pub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_offline_pub", Timeout = 5000, Reconnect = false })
    {
        await pub.ConnectAsync();
        await pub.PublishAsync(topic, "offline-msg", QualityOfService.AtLeastOnce);
        await pub.DisconnectAsync();
    }
    await Task.Delay(300);

    using var c2 = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = clientId, CleanSession = false, Timeout = 5000, Reconnect = false };
    var tcs = new TaskCompletionSource<String?>();
    c2.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());
    var ack = await c2.ConnectAsync();
    var present = ack.SessionPresent;
    var gotOffline = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task && tcs.Task.Result == "offline-msg";

    Check($"持久会话离线补发：SessionPresent={present}，补投收到={gotOffline}", present && gotOffline);

    await c2.DisconnectAsync();
}
catch (Exception ex) { Check($"持久会话离线补发（异常：{ex.GetType().Name} {ex.Message}）", false); }

// 19. MQTT-SN 网关：UDP 接入注册/发布 → TCP 订阅者收到
try
{
    using var server = new MqttServer { Port = 0 };
    server.Start();

    using var tcpSub = new MqttClient { Server = $"tcp://127.0.0.1:{server.Port}", ClientId = "aot_sn_tcpsub", Timeout = 5000, Reconnect = false };
    var tcs = new TaskCompletionSource<String?>();
    tcpSub.Received += (s, e) => tcs.TrySetResult(e.Arg.Payload?.ToStr());
    await tcpSub.ConnectAsync();
    await tcpSub.SubscribeAsync("aot/sn");
    await Task.Delay(200);

    using var gateway = new MqttSnGateway { Port = 0, GatewayId = 1, MqttBroker = $"tcp://127.0.0.1:{server.Port}", AdvertiseInterval = 120 };
    gateway.Start();

    using var sn = new MqttSnClient
    {
        ClientId = "aot_sn_client",
        LocalPort = 0,
        Gateway = new IPEndPoint(IPAddress.Loopback, gateway.ActualPort),
        KeepAliveSeconds = 120,
        CleanSession = true,
    };
    var connected = await sn.ConnectAsync();
    var topicId = await sn.RegisterTopicAsync("aot/sn");
    await sn.PublishAsync(topicId, "sn-hello"u8.ToArray(), QualityOfService.AtMostOnce);

    var gotSn = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task && tcs.Task.Result == "sn-hello";
    Check($"MQTT-SN 网关：UDP 注册/发布→TCP 订阅者收到（connected={connected}, topicId={topicId}, received={gotSn}）", connected && topicId > 0 && gotSn);

    await sn.DisconnectAsync();
    await tcpSub.DisconnectAsync();
}
catch (Exception ex) { Check($"MQTT-SN 网关（异常：{ex.GetType().Name} {ex.Message}）", false); }

Console.WriteLine(failures.Count == 0 ? "全部场景通过" : $"失败 {failures.Count} 个场景：{String.Join("；", failures)}");
return failures.Count == 0 ? 0 : 1;

[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods, typeof(AotControllerB))]
static void RootControllerB() { }

// 手动构造 HS256 JWT：header.payload.signature（Base64Url）
static String MakeJwt(String secret, String payloadJson)
{
    var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
    var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
    var data = Encoding.UTF8.GetBytes($"{header}.{payload}");
    var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data));
    return $"{header}.{payload}.{signature}";
}

static String Base64Url(Byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

internal class SmokeConfig : Config<SmokeConfig>
{
    public Int32 Value { get; set; } = 42;
}

/// <summary>源生成器路径：经 MapController&lt;T&gt; 注册</summary>
internal class AotControllerA
{
    public String Echo(String msg) => "A:" + msg;
}

/// <summary>手工注册路径：DynamicDependency 保根构造与公开方法</summary>
internal class AotControllerB
{
    public String Echo(String msg) => "B:" + msg;
}

/// <summary>手工注册路径：无保根（对照组）</summary>
internal class AotControllerC
{
    public String Echo(String msg) => "C:" + msg;
}

/// <summary>记录型集群交换机：仅记录收到的发布消息，用于载荷保真断言</summary>
internal class RecordingExchange : IMqttExchange
{
    public PublishMessage? LastPublished;

    public Boolean Add(Int32 sessionId, IMqttHandler session) => false;
    public Boolean TryGetValue(Int32 sessionId, out IMqttHandler session) { session = null!; return false; }
    public Boolean Remove(Int32 sessionId) => false;
    public void Publish(PublishMessage message) => LastPublished = message;
    public void Subscribe(Int32 sessionId, String topic, QualityOfService qos) { }
    public void Unsubscribe(Int32 sessionId, String topic) { }
    public IList<PublishMessage> GetRetainMessages(String topicFilter) => [];
    public void SavePersistentSession(String clientId, Int32 sessionId, IDictionary<String, QualityOfService>? subscriptions) { }
    public Boolean RestorePersistentSession(String clientId, Int32 sessionId) => false;
    public void ClearPersistentSession(String clientId) { }
    public void EnqueueOfflineMessage(String clientId, PublishMessage message) { }
    public MqttStats Stats { get; } = new();
    public IList<String> GetClientIds() => [];
    public IList<String> GetTopics() => [];
    public Int32 GetSubscriberCount(String topic) => 0;
    public Boolean InjectMessage(String clientId, PublishMessage message) => false;
}
