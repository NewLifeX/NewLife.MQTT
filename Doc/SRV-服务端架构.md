# SRV 服务端架构

> 版本：v5.0 | 日期：2026-07-20
> 需求对应：[需求文档](需求文档.md) §3.4 | 功能清单：[功能清单](功能清单.md) SRV | 返回：[架构设计](架构设计.md)

## 1. 模块职责

SRV 服务端实现高性能 MQTT Broker，面向平台运维工程师提供全版本兼容、会话管理、消息路由、安全认证和可扩展架构。

| 职责 | 核心类型 | 说明 |
|------|---------|------|
| 网络服务 | `MqttServer`（继承 `NetServer`） | TCP/UDP/IPv6 多协议监听，默认端口 1883 |
| 消息交换 | `MqttExchange` : `IMqttExchange` | 全局单例，订阅路由、Retain、持久会话、$SYS |
| 连接处理 | `MqttHandler` : `IMqttHandler` | 每连接一实例，处理 MQTT 报文管线 |
| 安全认证 | `IMqttAuthenticator` → `DefaultMqttAuthenticator` | 可插拔认证（用户名密码 + OAuth 令牌）+ ACL |
| 会话能力 | `MqttSessionCapabilities` | MQTT 5.0 会话能力协商与运行时管理 |
| 持久会话 | `PersistentSession` | 内存级订阅关系 + 离线消息队列（1000 条上限） |
| Inflight 管理 | `InflightManager` | QoS>0 消息超时重发（10s/3 次） |
| 运行统计 | `MqttStats` | 1 秒滑动窗口实时速率 |
| 持久化 | `IStorageProvider` → `DbExchange` / `RedisExchange` | 消息和会话跨重启恢复 |
| HTTP 管理 | `MqttManagementServer` + `MqttDashboard` | RESTful API + Web 看板 |

---

## 2. 核心组件设计

### 2.1 MqttExchange — 消息交换机

`MqttExchange` 是服务端的核心中枢（Singleton），管理所有订阅关系和消息路由。

**数据结构**：

| 字典 | 键 | 值 | 说明 |
|------|------|------|------|
| `_sessions` | `sessionId` | `IMqttHandler` | 所有活跃会话 |
| `_topics` | `topicFilter` | `SubscriptionList`（写时复制快照） | 主题→订阅者映射 |
| `_topicIndex` | 首层主题段 | `FilterBucket`（过滤器快照） | 前缀索引，Publish 按前缀定位候选过滤器 |
| `_wildcardFilters` | — | `FilterBucket` | `+`/`#` 开头的过滤器集合（不与 `$` 主题匹配） |
| `_retainMessages` | `topic` | `PublishMessage` | Retain 保留消息 |
| `_clientIdMap` | `sessionId` | `clientId` | ClientId 映射 |
| `_sharedGroupIndex` | `$share/{group}/{topic}` | `Int32` 轮询索引 | 共享订阅负载均衡 |

**Publish 路由流程**（`MqttExchange.Publish()`）：

```
1. Retain 处理：带 Retain → 空Payload清除 / 非空存储
2. $SYS 独立分发（不匹配通配符）
3. 前缀索引路由：按发布主题首层段取候选过滤器 + 通配符过滤器（$ 主题跳过）
   ├── 精确匹配：过滤器与主题逐段比对（无通配符过滤器走字符串恒等快速通道）
   ├── 通配符匹配：MqttTopicFilter.IsMatch() Span 零分配逐层匹配
   ├── 共享订阅：$share 解析 → 轮询分发
   └── NoLocal 过滤（5.0）：跳过发布者自身
4. QoS 降级：Min(publishQoS, subscribeQoS)
5. 主题别名分配（5.0）：AssignTopicAlias()
6. 流控检查（5.0）：CanSendQosMessage()
7. RetainAsPublished（5.0）：控制原始 Retain 标志
8. 订阅标识符注入（5.0）：SubscriptionIdentifier 原样回传
```

### 2.2 MqttHandler — 连接处理器

`MqttHandler` 每连接一实例（Transient），处理 MQTT 报文管线。可重写虚方法实现自定义行为。

**报文处理管线**：

```
CONNECT   → OnConnect()    → CompleteConnect()
PUBLISH   → OnPublish()    → Exchange.Publish()
SUBSCRIBE → OnSubscribe()   → Exchange.Subscribe()
UNSUBSCRIBE → OnUnsubscribe() → Exchange.Unsubscribe()
PINGREQ   → OnPing()       → SendAsync(PingResponse)
DISCONNECT → OnDisconnect() → Close()
AUTH      → OnAuth()       → 挑战/响应
```

**OnConnect 关键逻辑**：

1. 协议版本识别：`MQIsdp`+`0x03`→V310 / `MQTT`+`0x04`→V311 / `MQTT`+`0x05`→V500
2. `IMqttAuthenticator.Authenticate()` → 失败返回 CONNACK ReasonCode
3. 会话处理：CleanSession=1 清除旧会话 / =0 恢复持久会话
4. 空 ClientId → `Rand.NextString(16)` 自动生成 + CONNACK `AssignedClientIdentifier`
5. 遗嘱消息注册
6. `MqttSessionCapabilities` 初始化（ReceiveMaximum/MaxPacketSize/TopicAlias/SessionExpiry）
7. `BuildConnAckProperties()` 服务端能力声明

### 2.3 MqttServer — 网络服务

`MqttServer` 继承 `NewLife.Net.NetServer`，管理网络端口监听与 MQTT 帧编解码。

**编解码器链**：

```
TCP 数据 → MqttCodec（v12 消息泵 IMessageCodec，含 ProxyProtocol 头剥离）→ MqttMessage
WebSocket → 独立 HttpServer + MqttWebSocketHandler 桥接 → 同一 MqttSession 处理链
```

- `MqttCodec`：v12 消息泵上的 MQTT 帧定界（固定头 + 变长剩余长度）与整帧构建；服务端可选识别并剥离前置代理写入的 ProxyProtocol v1/v2 头
- `MqttWebSocketHandler`：WebSocket 握手后的二进制帧桥接到 MQTT 会话，见下节端口说明
- 旧版 `ProxyCodec`/`WebSocketServerCodec`/`HttpCodec` 管线已随 NewLife.Core v12 移除（v12 不再提供管线编解码器）

**端口说明（v12 起）**：

| 端口 | 配置项 | 用途 |
|------|--------|------|
| 1883（默认） | `Port` | MQTT TCP/TLS 监听 |
| 独立 | `EnableWebSocket` / `WebSocketPort` / `WebSocketPath`（默认 `/mqtt`） | MQTT over WebSocket；`WebSocketPort=0` 时由系统分配；配置 `WebSocketCertificate` 后启用 WSS |
| 18883（可选） | `ManagementPort` | HTTP 管理 API（`/Api/{Action}`） |
| 2883（可选） | `ClusterPort` | 集群 RPC（HTTP） |

**会话生命周期**：

| 阶段 | 操作 |
|------|------|
| 连接建立 | `MqttHandler` 创建，注册到 `NetServer` 会话 |
| CONNECT 成功 | `Exchange.Add(handler)` 注册到全局交换机 |
| 活跃中 | 心跳保活 + 消息收发 |
| 异常断开 | 遗嘱发布 + `Exchange.Remove(sessionId)` |
| 正常 DISCONNECT | 遗嘱清除 + 会话关闭 |
| 超时清理 | 30s 定时扫描 `RemoveNotAlive()` |

**接收缓冲调优**：`MqttServer.SessionBufferSize`（默认 2048 字节，`MqttClient.ReceiveBufferSize` 同语义）在会话启动前设置底层 Socket 会话的 `BufferSize`。v12 消息泵下每个接收轮次都会更换接收缓冲，缓冲大小即每条消息的接收侧分配量级；默认 8KB 在小消息高吞吐场景会产生大量大对象（每消息 8KB），2048 可降低约 75%。大载荷为主的场景可调大以减少读取次数。

---

## 3. 安全认证架构

### 3.1 认证流程

```
CONNECT 报文
  → IMqttAuthenticator.Authenticate(clientId, username, password)
      ├── 成功 → 继续连接
      └── 失败 → CONNACK ReasonCode（0x04 用户名密码错误 / 0x05 未授权）
  → OAuth 令牌认证（可选，外部颁发令牌）
      ├── 令牌验证通过 → 继续连接
      └── 失败 → CONNACK ReasonCode 0x05
  → MQTT 5.0 增强认证（可选）
      ├── AuthMessage 挑战（ReasonCode 0x18 继续）
      ├── 客户端响应
      └── AuthMessage 确认（ReasonCode 0x00 成功 / 0x19 重认证）
```

### 3.2 ACL 权限模型

```
PUBLISH  → AuthorizePublish(clientId, topic)  → 失败 ReasonCode 0x87（未授权）
SUBSCRIBE → AuthorizeSubscribe(clientId, topic) → 失败 QoS 0x80（失败）
```

### 3.3 SASL 认证（SRV-3 MQTT 5.0）

`ISaslMechanism` 可插拔接口，内置 `ScramSha256Mechanism` 标准实现。`MqttHandler.SaslCredentialStore` 属性注入凭据存储。

---

## 4. MQTT 5.0 服务端特性

### 4.1 会话能力协商

`MqttSessionCapabilities` 在 CONNECT 时提取客户端能力，在 CONNACK 时声明服务端能力：

| 客户端能力 | 属性 | 服务端行为 |
|-----------|------|-----------|
| ReceiveMaximum | 0x21 | `CanSendQosMessage()` inflight 数量检查 |
| MaximumPacketSize | 0x27 | 发送前报文大小校验 |
| TopicAliasMaximum | 0x22 | 服务端→客户端 `AssignTopicAlias()` 上限 |
| SessionExpiryInterval | 0x11 | 离线会话保活时长 |

| 服务端声明 | 属性 | 说明 |
|-----------|------|------|
| MaximumQoS | 0x24 | 支持最高 QoS 2 |
| RetainAvailable | 0x25 | Retain 功能可用 |
| WildcardSubscriptionAvailable | 0x28 | 通配符订阅可用 |
| SharedSubscriptionAvailable | 0x2A | 共享订阅可用 |
| SubscriptionIdentifierAvailable | 0x29 | 订阅标识符可用 |

### 4.2 订阅选项处理

| 选项 | 属性 | 服务端行为 |
|------|------|-----------|
| NoLocal | Subscription 选项 bit 2 | Publish 路由跳过发布者自身 |
| RetainAsPublished | Subscription 选项 bit 3 | 保留原始 Retain 标志转发 |
| RetainHandling | Subscription 选项 bit 4-5 | 0=订阅时发送 / 1=仅新订阅 / 2=不发送 |

### 4.3 遗嘱延迟发布

`WillDelayInterval`（0x18）属性：`IMqttHandler.Close()` 中 `Task.Delay(willDelayInterval)` 延迟后再发布遗嘱消息。

### 4.4 消息过期清理

`MqttExchange.IsMessageExpired()` 在 `Publish()` 和订阅推送时检查 `MessageExpiryInterval`（0x02），过期消息跳过分发。

### 4.5 服务端 KeepAlive 覆盖

`IMqttHandler.CompleteConnect()` 中，若配置了 `ServerKeepAlive`（0x13），则写入 CONNACK Properties 覆盖客户端 KeepAlive。

---

## 5. HTTP 管理 API（SRV-8）

`MqttManagementServer` 提供 RESTful JSON API，默认监听 `ManagementPort` 端口。

**端点**：

| 端点 | 方法 | 说明 |
|------|:----:|------|
| `/api/stats` | GET | 运行统计（连接数/消息数/字节数/速率） |
| `/api/clients` | GET | 在线客户端列表（ClientId/IP/连接时间/订阅数） |
| `/api/topics` | GET | 当前所有主题 |
| `/api/retained` | GET | 保留消息列表 |

**Dashboard**：`MqttDashboard`（基于 NewLife.Core `HttpServer`，参考 NewLife.Agent `WebPanel`）在 `ManagementPort+1` 提供 HTML，仅查看本实例实时状态数据并支持基本控制操作，自动代理 `/api/*` 到管理 API。`MqttServer.EnableDashboard=true` 启用。

---

## 6. DI 扩展点

| 接口 | 生命周期 | 替换场景 |
|------|---------|---------|
| `IMqttHandler` | Transient | 自定义连接处理逻辑（日志、审计、限流） |
| `IMqttExchange` | Singleton | Redis/SQLite 持久化交换机 |
| `IMqttAuthenticator` | Singleton | 对接用户数据库、LDAP、OAuth |
| `ISaslMechanism` | Transient | 自定义 SASL 认证方法 |
| `IStorageProvider` | Singleton | 持久化后端切换 |

**自定义 Handler 示例**：

```csharp
public class MyHandler : MqttHandler
{
    protected override ConnAck OnConnect(ConnectMessage message)
    {
        XTrace.WriteLine($"客户端[{message.ClientId}]连接");
        return base.OnConnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        XTrace.WriteLine($"收到[{message.Topic}]: {message.Payload.ToStr()}");
        return base.OnPublish(message);
    }
}

// 注册
services.AddTransient<IMqttHandler, MyHandler>();
```

---

（完）
