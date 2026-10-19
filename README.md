# NewLife.MQTT — A Complete, Fully Self-Developed MQTT Protocol Implementation

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.mqtt?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.MQTT?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.MQTT?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.MQTT?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.MQTT?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.MQTT?logo=github)](https://github.com/NewLifeX/NewLife.MQTT/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

**NewLife.MQTT** is a **fully self-developed, zero third-party dependency, MIT-licensed** complete MQTT protocol implementation. A single NuGet package provides both a fully featured `MqttClient` and a high-performance `MqttServer` (broker), covering every mainstream runtime from .NET Framework 4.5 to .NET 10.

## 📖 Documentation Navigation

| Document | Description |
|------|------|
| [Requirements](Doc/需求文档.md) | Project vision, core goals, functional requirements of the 7 major modules, deferred items and non-goals |
| [Feature List](Doc/功能清单.md) | Implementation/test/comment three-dimensional tracking status for 87 feature points |
| [Architecture Design](Doc/架构设计.md) | Overall five-layer architecture, data models, interface design, key decisions, performance design |
| [SRV-Server Architecture](Doc/SRV-服务端架构.md) | Detailed broker design: message routing, security authentication, HTTP management API |
| [CLUS-Cluster Architecture](Doc/CLUS-集群架构.md) | Four-stage cluster evolution roadmap, Raft consensus, topic sharding, multi-tier topology |
| [Competitive Analysis](Doc/竞品分析.md) | MQTT ecosystem landscape comparison (EMQX/MQTTnet/Mosquitto, etc.), SWOT |
| [CLUS-Cluster Competitive Analysis](Doc/CLUS-集群竞品分析.md) | Dedicated benchmarking of cluster capabilities (EMQX/HiveMQ/VerneMQ) |
| [Integration Test Guide](Doc/集成测试说明.md) | Test architecture, how to run, test categories, cross-compatibility tests (NewLife ↔ MQTTnet, mandatory in CI) |
| [AOT Compatibility Analysis](Doc/AOT兼容性分析.md) | NativeAOT measurements: 19 main-path scenarios pass both JIT and AOT runs, mainstream RID matrix (win/linux × x64/arm64 + Loongson LA64, see aot.yml/test.yml in CI), implemented rooting strategy, reproduction steps (AotSmoke project checked in) |
| [Release Notes](Doc/更新日志.md) | Version release records and commit details |

---

---

## Core Features

| Feature | Description |
|------|------|
| **Complete protocol** | MQTT 3.1 / 3.1.1 / 5.0, 14 basic packet types + AUTH packet, 30 MQTT 5.0 properties |
| **Zero dependencies** | Depends only on NewLife.Core; no third-party packages anywhere in the NuGet dependency chain, no supply-chain security risk |
| **Client + server in one** | A single `NewLife.MQTT` NuGet package, no separate client/server builds |
| **Seven transports** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / reliable UDP (unique, all platforms) / ProxyProtocol v1/v2 |
| **High performance** | Built on NewLife.Net, millions of connections per node (verified at 4 million); broker measured at **~1.25 million msg/s** inbound (multiple connections), 106,000 msg/s end-to-end on a single connection; topic routing stays constant at 120 ns per entry with tens of thousands of subscription filters ([performance report](Doc/Benchmark/MQTT性能测试.md)) |
| **Free enterprise features** | Cluster (Raft consensus / topic sharding) / bridging / rule engine / WebHook / $SYS / ACL, all built in free of charge |
| **Automatic resubscription** | All topics are resubscribed automatically after reconnecting; no user code required |
| **SOCKS5 proxy** | Built-in SOCKS5 proxy support (username/password authentication); both TCP and WS connections can be forwarded through a proxy |
| **Full version coverage** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 ~ net10 |
| **NativeAOT support** | Main paths verified under NativeAOT (management API / cluster RPC / TCP / WS / authentication / MQTT-SN, etc., 19 scenarios run under both JIT and AOT, 0 ILC warnings on the library side; CI covers win-x64 / linux-x64 / linux-arm64 / Loongson LA64, where LA64 AOT smoke is observational and non-blocking) |
| **Compliance-ready** | Completely self-developed, MIT-licensed, no copyleft / patent clauses, no overseas supply-chain risk |

---

## Differentiating Advantages

### MQTT over QUIC (rare in the industry)
Currently **only EMQX and NewLife.MQTT support** this transport:
- Low latency, no head-of-line blocking, ideal for weak networks and mobile scenarios
- .NET 7/8 require preview features; .NET 9+ is stable
- Default port 14567, ALPN identifier `"mqtt"`
- Full client/server path (connect/subscribe/publish/receive) verified end to end (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT over Reliable UDP (industry first)
**A world first, no operating-system msquic support required, available on all platforms**:
- In-house 4-byte frame header + sequence number + ACK confirmation + timeout retransmission
- Covers embedded platforms such as ARM Linux 4.x that do not support msquic

### Automatic Resubscription After Reconnect
MQTTnet / Eclipse Paho require users to resubscribe manually after reconnecting; NewLife.MQTT does it automatically:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// No extra code is needed after reconnecting; subscriptions are restored automatically
```

### Enterprise Features Built In and Free
- **Message bridging**: In / Out / Both directions, topic prefix mapping, QoS cap control
- **Rule engine**: 5 actions (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 event types pushed asynchronously with automatic retry on failure; events are integrated into the MqttHandler + Exchange pipeline
- **Cluster**: Raft consensus + consistent-hash topic sharding + multi-tier topology routing, cross-node subscription synchronization and message forwarding, dedicated RPC port (2883)
- **MQTT-SN v1.2**: Access gateway for constrained devices (sensors), interoperable with MQTT topics
- **Persistent storage**: Optional `NewLife.MQTT.Storage` extension package with Redis / database implementations for offline messages and session persistence
- **$SYS system topics**: 14 standard topics with real-time runtime statistics
- **ProxyProtocol v1/v2**: Preserve the real client IP through nginx/HAProxy

---

## Quick Start

### Installation

```bash
dotnet add package NewLife.MQTT
```

### Starting the Server

```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MqttHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),
    Log = XTrace.Log,
};
server.Start();
```

### Connecting a Client

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// Subscribe (automatically resubscribed after reconnect)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Publish
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Connection String Initialization

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Client Usage in Detail

### Specifying the Protocol Version

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (default) / V500
};
```

### QoS Publishing

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### QoS Semantics

The effective consumption QoS is the **smaller** of the publish QoS and the subscription QoS:

| QoS | Name | Description |
|-----|------|------|
| 0 | At most once | No acknowledgement after sending; messages may be lost, suitable for sensor heartbeats |
| 1 | At least once | The broker replies with PUBACK; duplicates are possible, suitable for important data |
| 2 | Exactly once | Four-step handshake; no loss, no duplicates, suitable for billing/orders |

### Wildcard Subscriptions

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + matches a single level
await client.SubscribeAsync("/device/#", handler);        // # matches multiple levels
```

### Shared Subscriptions (Load Balancing)

Format `$share/{group}/{topic}`; multiple consumers in the same group receive messages in a round-robin fashion:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Received: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> WebSocket server: since v12 it is hosted by a separate `HttpServer` (`EnableWebSocket = true`, `WebSocketPort` defaults to 0 for system assignment, `WebSocketPath` defaults to `/mqtt`) and no longer shares the MQTT TCP port; clients still connect via `ws://host:port/mqtt`. Setting `WebSocketCertificate` enables TLS on that port, i.e. `wss://host:port/mqtt`.

---

## Server Usage in Detail

### Custom Handler

Inherit from `MqttHandler` to intercept all kinds of messages, ideal for IoT platforms that persist data directly:

```csharp
public class MyHandler : MqttHandler
{
    private readonly ILog _log;

    public MyHandler(ILog log) => _log = log;

    protected override ConnAck OnConnect(ConnectMessage message)
    {
        _log.Info("Connected[{0}] user={1} clientId={2}",
            Session.Remote.EndPoint, message.Username, message.ClientId);
        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.Info("Disconnected[{0}]", Session.Remote);
        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.Info("[{0} QoS={1}] {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());
        return base.OnPublish(message);
    }
}

services.AddTransient<IMqttHandler, MyHandler>();
```

### Custom Authentication (ACL)

```csharp
public class MyAuthenticator : IMqttAuthenticator
{
    public Boolean Authenticate(String clientId, String username, String password)
        => username == "admin" && password == "secret";

    public Boolean AuthorizePublish(String clientId, String topic)
        => !topic.StartsWith("$SYS/");

    public Boolean AuthorizeSubscribe(String clientId, String topic) => true;
}

services.AddSingleton<IMqttAuthenticator, MyAuthenticator>();
```

---

## Enterprise Features

### Message Bridging

```csharp
var bridge = new MqttBridge
{
    Name = "to-cloud",
    Server = "tcp://cloud-broker.example.com:1883",
    Direction = BridgeDirection.Both,
    TopicPrefix = "edge/factory1/",
    MaxQoS = QualityOfService.AtLeastOnce,
};
bridge.Start();
```

### Rule Engine

```csharp
var engine = new MqttRuleEngine();
engine.AddRule(new MqttRule
{
    TopicFilter = "/sensor/+/temperature",
    Action = RuleAction.Republish,
    TargetTopic = "/analytics/temperature",
});
engine.AddRule(new MqttRule
{
    TopicFilter = "/alarm/#",
    Action = RuleAction.WebHook,
    WebHookUrl = "https://api.example.com/alarm",
});
```

### Cluster Deployment

Three-node cluster configuration (each node lists the other node addresses):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Cluster operations: Join on startup → Ping keep-alive every 15 seconds → Lease on shutdown; nodes that time out (5 minutes) are removed automatically.

> Since v12, cluster RPC uses HTTP (`ClusterPort` is an HTTP port; node addresses may be written as `host:port` or `http://host:port`), and the shared secret `ClusterSecret` is verified as a request token on every call; NewLife.Remoting is no longer required.

### Alibaba Cloud IoT Adapter

```csharp
var client = new AliyunMqttClient
{
    ProductKey = "a1xxxxxxxx",
    DeviceName = "myDevice",
    DeviceSecret = "xxxxxxxx",
};
await client.ConnectAsync();
await client.PublishPropertyAsync(new { temperature = 25.6 });
```

---

## Try It Quickly

Open the source code and set `Test` as the startup project to run both Server and Client at the same time:

![Demo](Doc/Demo.png)

---

## Core Highlights

| Highlight | Description |
|------|------|
| 🚀 **Complete protocol** | Full MQTT 3.1.1 support (14 packet types) + complete MQTT 5.0 core features (30 properties) |
| 🔒 **Zero dependencies** | Depends only on the NewLife.Core foundation library; no supply-chain security risk, no license conflicts |
| ⚖️ **MIT open source** | The most permissive open-source license; free to modify and use commercially, no copyleft restrictions, no patent clauses, no legal risk |
| 🎯 **Client + server in one** | A single `NewLife.MQTT` NuGet package provides both client and server; minimal integration |
| 🌐 **Seven transports** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / reliable UDP / ProxyProtocol v1/v2 — the broadest coverage in the industry |
| 🏢 **Free enterprise features** | Cluster / bridging / rule engine / WebHook / statistics / $SYS; competitors require enterprise editions or paid plugins |
| 🇨🇳 **Fully self-developed** | Complete independent intellectual property, no external-dependency legal risk, compliance-ready |
| 🔧 **Full version coverage** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Differentiating Advantages

### 1. MQTT over QUIC (rare in the industry)
- **Only EMQX and NewLife.MQTT support it**
- Low latency, no head-of-line blocking
- Available on .NET 7+ (preview features required on .NET 7/8, stable on .NET 9+)

### 2. MQTT over Reliable UDP (original, self-developed)
- **Unique in the industry**
- 4-byte frame header + sequence number + Ack confirmation + timeout retransmission
- No operating-system QUIC/msquic support required; works on all platforms

### 3. Enterprise Features Built In and Free
- **Message bridging (bidirectional):** Supports In/Out/Both directions, topic prefix mapping, QoS cap control
- **Rule engine (5 actions):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook event push:** 6 event types, asynchronous and non-blocking, with retry support
- **Cluster support:** Manual node configuration, cross-node subscription synchronization and message forwarding
- **ProxyProtocol:** Supports nginx/HAProxy proxy protocol v1/v2 to preserve the real client IP
- **$SYS system topics:** 14 standard topics with real-time runtime statistics

### 4. Automatic Resubscription After Reconnect
Competitors (MQTTnet / Eclipse Paho) require users to **resubscribe manually** after reconnecting; NewLife.MQTT **does it automatically**.

### 5. Built-in Alibaba Cloud IoT Adapter
`AliyunMqttClient` works out of the box: HMAC signature authentication + property reporting + clock synchronization.

## MQTT Protocol
MQTT is the most popular IoT communication protocol, including client, server and web management platform.

It provides a publish/subscribe model that is simpler, lighter and easier to use, targeting constrained environments (low bandwidth, high network latency, unstable network communication). It can be summarized simply as built for the Internet of Things. The official summary of its characteristics is as follows:
1. Uses the publish/subscribe messaging pattern, providing one-to-many message publishing and decoupling applications.
2. Message transport that is agnostic to payload content.
3. Uses TCP/IP for network connectivity.
4. Three quality-of-service levels for message publishing:
"At most once": message publishing relies entirely on the underlying TCP/IP network; message loss or duplication can occur. This level is suitable for cases such as environmental sensor data, where losing one reading does not matter because another will be sent soon.
"At least once": ensures messages arrive, but duplicates may occur.
"Exactly once": ensures a message arrives exactly once. This level is suitable for cases such as billing systems, where duplicate or lost messages would lead to incorrect results.
5. Small transport with very low overhead (the fixed-length header is 2 bytes) and minimal protocol exchange to reduce network traffic.
6. Uses the Last Will and Testament feature to notify interested parties when a client disconnects abnormally.

## MQTT Publish and Subscribe
When publishing, the message QoS is specified, and the message stored by the broker includes the QoS;
when subscribing, the QoS of a consumed message is the smaller of the publish and subscription QoS values!

Detailed scenarios:
- Subscription QoS=0: regardless of what is published, consumed messages always have QoS 0;
- Subscription QoS=1: when a message is published with QoS=0, consumption yields QoS=0; when published with QoS=1 or 2, consumption yields QoS=1;
- Subscription QoS=2: the consumed message QoS is exactly the published QoS;
- Publish QoS=0: the broker does not reply at all; in theory a mid-way loss goes unnoticed, but because TCP is used, the client can detect network failures;
- Publish QoS=1: the broker replies with `PubAck`, indicating the message has been received;
- Publish QoS=2: the broker replies with `PubRec`, the client sends `PubRel` again, the broker replies with `PubComp`, and only then is the message considered published;
- Subscription QoS=2: the broker pushes a QoS=2 message, the client first replies `PubRec`, the broker sends `PubRel` again, the client replies `PubComp`, and only then is the message considered consumed;
- When publishing a QoS=2 message, the double-confirmation flow does not need the consumer to be online; it is completed solely between the publisher and the broker.

## Quick Taste
Open the source solution, set Test as the startup project, and start it.
By default, TestServer and TestClient start one after the other.
![Demo](Doc/Demo.png)

## Server
Reference `NewLife.MQTT` from NuGet and use the following code to start the server:
```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MqttHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),

    Log = XTrace.Log,
    SessionLog = XTrace.Log,
};
server.Start();
```
By specifying port 1883, the default handler `MqttHandler`, and the default exchange `MqttExchange`, the server starts.

## Client
Reference `NewLife.MQTT` from NuGet and use the following code to connect to the server:
```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    //UserName = "admin",
    //Password = "admin",
    ClientId = Guid.NewGuid() + "",
};

await client.ConnectAsync().ConfigureAwait(false);

// Subscribe to the "/test" topic
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Publish a message to the "/test" topic every 2 seconds
while (true)
{
    try
    {
        var msg = "Learning has no fixed order; the one who gets there first is the teacher" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```
There are several elements for a client to connect to the server: `server address`, `username`, `password` and `client identifier`; then connect via `ConnectAsync`.
A client can act as a consumer, subscribing to a specified topic via `SubscribeAsync`.
A client can also act as a producer, publishing messages to a specified topic via `PublishAsync`.

## Shared Subscriptions (EMQX)
Supports the EMQX shared subscription feature: multiple clients can subscribe to the same shared topic, and the MQTT broker distributes message payloads to subscribers in a load-balanced manner.
The shared subscription format is: `$share/{group}/{topic}`, where `{group}` is the shared group name and `{topic}` is the actual topic filter.

```csharp
// Subscribe to a shared topic; multiple clients in the same shared group receive messages in a load-balanced way
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Received shared subscription message: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Publish to the actual topic (without the $share prefix)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Advantages of shared subscriptions:
- **Load balancing**: multiple subscribers in the same shared group receive messages evenly
- **Horizontal scaling**: increase processing capacity by adding subscribers
- **High availability**: if one subscriber fails, the other subscribers can continue processing messages

### Specifying the MQTT Protocol Version
Starting from version 2.3.2025.0212, the client supports specifying the MQTT protocol version. Set the `Version` property to choose MQTT 3.1, 3.1.1 or 5.0:
```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // Specify MQTT 5.0
};
```
Supported versions:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (default)
- `MqttVersion.V500` - MQTT 5.0

**Note**: If the `Version` property is not set explicitly, the client uses MQTT 3.1.1 by default to maintain backward compatibility.

## Custom Server
When you need to handle client connections and message interaction logic on the server side, a custom server is required. For example, an IoT platform can store device-reported MQTT data directly upon receipt, without consuming it again.
An example custom handler is shown below:
```csharp
private class MyHandler : MqttHandler
{
    private readonly ILog _log;

    public MyHandler(ILog log) => _log = log;

    protected override ConnAck OnConnect(ConnectMessage message)
    {
        _log.Info("Client[{0}] connected user={1} pass={2} clientId={3}", Session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.Info("Client[{0}] disconnected", Session.Remote);

        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.Info("Client[{0}] published [{1}:qos={2}]: {3}", Session.Remote, message.Topic, (Int32)message.QoS, message.Payload.ToStr());

        return base.OnPublish(message);
    }
}
```
Simply change the server's handler registration code slightly to use the custom handler:
```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MyHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),

    Log = XTrace.Log,
    SessionLog = XTrace.Log,
};
server.Start();
```

## Cluster
Cluster management: Join, Ping, Lease.
Join joins the cluster and tells the others that this node is one of the cluster nodes; called N-1 times at startup.
Ping all nodes every 15 seconds to update the active time.
Lease leaves the cluster; called N-1 times.
Each node periodically removes timed-out nodes based on the last active time.

## NewLife Project Matrix
By default, each project supports net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5; older versions (2024.0801) supported net4.0/net2.0.

|                               Project                               | Year  | Description                                                                                  |
| :-----------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------------- |
|                          Foundation components                      |       | Backing other middleware and product projects                                                |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | Core library: logging, configuration, caching, networking, serialization, APM performance tracing |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | Big-data middleware, hundreds of billions of rows per table, MySql/SQLite/SqlServer/Oracle/PostgreSql/DaMeng, automatic sharding, read/write separation |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | Networking library, tens of millions of TPS per node (22.66 million tps), millions of connections per node (4 million TCP long connections) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | Protocol communication library providing a CS application communication framework, supports Http/RPC communication frameworks, high throughput, low overhead and easy access for IoT devices |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | Cube rapid development platform integrating user permissions, SSO login, OAuth server, etc.; validated by projects with 10 billion rows per table |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | Service management component that installs applications as operating-system daemons: Windows services, Linux Systemd |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero-generation scaffolding, project templates NewLife.Templates based on the NewLife component ecosystem: Web, WebApi, Service |
|                              Middleware                             |       | Integration with well-known middleware platforms                                            |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis client with microsecond latency, millions of throughput, rich message queues; validated by projects with tens of billions of data points |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | Fully managed RocketMQ client supporting Apache RocketMQ and Alibaba Cloud Message Queue; validated by billion-scale projects |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | IoT messaging protocol, MqttClient/MqttServer, client supports Alibaba Cloud IoT |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT standard library defining various communication protocol standards for the IoT domain |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII, implemented on the IoT standard library, supporting the ZeroIoT platform and IoTEdge gateway |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | Siemens PLC protocol, implemented on the IoT standard library, supporting IoT platforms and IoTEdge |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | Map component library wrapping Baidu Maps, Amap, Tencent Maps and Tianditu |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | Audio codec library: PCM/ADPCMA/G711A/G722U/WAV/AAC |
|                            Product platforms                         |       | Product-platform level, ready to compile and deploy, customizable |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | Stardust distributed service platform: node management, APM monitoring center, configuration center, registry, release center |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | AntJob distributed big-data computing platform (real-time/offline), ant-moving sharding concept, validated by trillion-scale projects |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | Enterprise ERP: product management, customer management, sales management, supplier management |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | CrazyCoder developer toolbox: networking, serial ports, encryption/decryption, regular expressions, Modbus, MQTT |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | Simple file storage supporting centralized file storage in distributed systems |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | Product-grade reverse proxy: NAT proxy, Http proxy |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http stress-testing tool |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git source code management system |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | Embedded operating system, completely independent, supporting the ARM Cortex-M chip architecture |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | Embedded industrial computer, IoT edge gateway, high-performance .NET8 host, used in industry, agriculture, transportation and healthcare |
|                         FIoT IoT Platform                          | 2020  | End-to-end IoT solution for construction, environmental protection and agriculture, integrating software, hardware and big-data analysis; validated by a 100,000-point single-node project |
|                        UWB High-Precision Indoor Positioning     | 2020  | Centimeter-level (10~20 cm) high-precision indoor positioning, integrated hardware and software, interoperable with other systems, validated by large exhibition-hall projects |

## NewLife Development Team
![XCode](https://newlifex.com/logo.png)

The NewLife team was founded in 2002 and is an IoT industry solution provider in the new era, dedicated to providing software and hardware application consulting, system architecture planning and development services.
More than 80 open-source projects led by the team have been widely used across industries, with cumulative NuGet downloads exceeding 4 million.
The team's big-data middleware NewLife.XCode, AntJob distributed computing platform, Stardust distributed platform, NewLife.Redis cache queue component and FIoT IoT platform have all been successfully applied in power, education, internet, telecommunications, transportation, logistics, industrial control, healthcare, museums and other industries, providing customers with large numbers of advanced, reliable, secure, high-quality and easily extensible products and system integration services.

We will continue to improve our services to become a long-term trusted partner of our customers, and through continuous innovation and development, become an excellent IoT service provider in China.

`The NewLife team started in 2002; some open-source projects have a history of more than 20 years, and the source repositories retain all modification records since 2010`
Website: https://newlifex.com
Open source: https://github.com/newlifex
QQ groups: 1600800/1600838
WeChat official account:
![智能大石头](https://newlifex.com/stone.jpg)
