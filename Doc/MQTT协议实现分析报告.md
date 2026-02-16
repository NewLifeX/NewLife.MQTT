# NewLife.MQTT 协议实现分析报告

## 概述

本报告对 NewLife.MQTT 项目进行全面分析，对标 MQTT 3.1.1（OASIS标准）和 MQTT 5.0 官方协议规范，列出各功能点的实现情况、社区主流扩展功能支持情况，以及尚未实现的功能清单。

---

## 一、项目架构总览

| 模块 | 说明 |
|------|------|
| `Messaging/` | 协议报文定义与编解码（15种报文类型，含 MQTT 5.0 AUTH） |
| `Messaging/MqttProperties` | MQTT 5.0 属性系统（支持全部数据类型） |
| `Messaging/MqttPropertyId` | MQTT 5.0 属性标识符枚举（30种） |
| `MqttClient` | 客户端实现（TCP/SSL/WebSocket/WSS/重连/心跳/遗嘱） |
| `MqttServer` / `MqttSession` | 服务端实现（基于 NetServer） |
| `Handlers/MqttHandler` | 消息处理器基类（遗嘱发布/正常断开清除） |
| `Handlers/MqttExchange` | 消息交换机（Retain存储/会话持久化/离线消息/共享订阅负载均衡） |
| `Handlers/InflightManager` | Inflight消息管理器（超时重发/DUP标志） |
| `Handlers/MqttSessionCapabilities` | MQTT 5.0 会话能力（主题别名/流控/最大报文） |
| `Handlers/IMqttAuthenticator` | ACL 权限控制接口（认证/发布权限/订阅权限） |
| `Handlers/MqttStats` | 运行统计（连接数/消息速率/订阅数/字节数/速率计算） |
| `Handlers/MqttBridge` | 消息桥接器（双向桥接/主题前缀映射/QoS 上限/断线重连） |
| `Handlers/MqttWebHook` | WebHook 事件推送（连接/断开/发布/订阅事件，HTTP POST 推送，重试机制） |
| `Handlers/MqttRuleEngine` | 规则引擎（主题匹配/Republish/WebHook/Bridge/Drop/自定义动作） |
| `Handlers/PersistentSession` | 持久化会话（订阅关系/离线消息队列） |
| `Quic/MqttQuicTransport` | MQTT over QUIC 传输（客户端 + 监听器，NET9_0_OR_GREATER） |
| `Quic/MqttUdpTransport` | 基于 NewLife.Core UDP 的可靠传输层（客户端 + 监听器，全平台） |
| `WebSocket/WebSocketClientCodec` | WebSocket 客户端编解码器（ws/wss） |
| `Clusters/` | 集群支持（`ClusterServer`/`ClusterExchange`/`ClusterNode`） |
| `ProxyProtocol/` | HAProxy ProxyProtocol v1/v2 支持 |
| `MqttCodec` | 编解码器（粘包处理） |
| `MqttTopicFilter` | 主题过滤匹配 |
| `MqttFactory` | 消息工厂（支持15种报文类型） |
| `AliyunMqttClient` | 阿里云物联网平台适配 |

**支持的传输方式：**
- ✅ TCP
- ✅ TCP + TLS/SSL
- ✅ WebSocket（服务端通过 `WebSocketServerCodec` + `HttpCodec` 管道实现）
- ✅ WebSocket 客户端（`WebSocketClientCodec` — 支持 ws:// 连接）
- ✅ WebSocket + TLS（WSS）客户端（wss:// — 自动启用 TLS）
- ✅ UDP（通过 `NetUri` 的 `CreateRemote()` 支持）
- ✅ ProxyProtocol v1/v2（nginx/HAProxy 透传）

- ✅ MQTT over QUIC（`MqttQuicClient` / `MqttQuicListener` — .NET 9+ 平台，需要 Windows 11 或 msquic）
- ✅ MQTT over 可靠 UDP（`MqttUdpClient` / `MqttUdpListener` — 基于 NewLife.Core UDP，全平台支持，不依赖 OS）

---

## 二、MQTT 3.1.1 协议功能点对标

### 2.1 报文类型（14种 + AUTH）

| 报文类型 | 值 | 实现 | 说明 |
|---------|---|------|------|
| CONNECT | 1 | ? 完整 | `ConnectMessage` — 支持用户名/密码/遗嘱/CleanSession/KeepAlive/5.0属性 |
| CONNACK | 2 | ? 完整 | `ConnAck` — 支持 SessionPresent + ReturnCode + 5.0 ReasonCode + Properties |
| PUBLISH | 3 | ? 完整 | `PublishMessage` — 支持 Topic/Payload/QoS/Retain/DUP 标志 + Properties |
| PUBACK | 4 | ? 完整 | `PubAck` — QoS 1 确认 + 5.0 ReasonCode + Properties |
| PUBREC | 5 | ? 完整 | `PubRec` — QoS 2 第一步 + 5.0 ReasonCode + Properties |
| PUBREL | 6 | ? 完整 | `PubRel` — QoS 2 第二步 + 5.0 ReasonCode + Properties |
| PUBCOMP | 7 | ? 完整 | `PubComp` — QoS 2 第三步 + 5.0 ReasonCode + Properties |
| SUBSCRIBE | 8 | ? 完整 | `SubscribeMessage` — 支持多主题订阅 + Properties |
| SUBACK | 9 | ? 完整 | `SubAck` — 支持 GrantedQoS 列表 + Properties |
| UNSUBSCRIBE | 10 | ? 完整 | `UnsubscribeMessage` — 支持多主题退订 + Properties |
| UNSUBACK | 11 | ? 完整 | `UnsubAck` + 5.0 ReasonCodes + Properties |
| PINGREQ | 12 | ? 完整 | `PingRequest` |
| PINGRESP | 13 | ? 完整 | `PingResponse` |
| DISCONNECT | 14 | ? 完整 | `DisconnectMessage` + 5.0 ReasonCode + Properties |
| AUTH | 15 | ? 完整 | `AuthMessage` — MQTT 5.0 增强认证报文 + ReasonCode + Properties |

**结论：15种报文类型（含 MQTT 5.0 AUTH）已全部实现，协议编解码完整。所有报文均支持 MQTT 5.0 属性扩展。**

### 2.2 QoS 服务质量

| QoS 等级 | 客户端 | 服务端 | 说明 |
|----------|--------|--------|------|
| QoS 0（至多一次） | ? | ? | 发送即忘 |
| QoS 1（至少一次） | ? | ? | PUBLISH → PUBACK 流程完整，支持超时重发 |
| QoS 2（恰好一次） | ? | ? | 四步握手完整实现（PUBLISH→PUBREC→PUBREL→PUBCOMP），Id 正确传递 |

**QoS 增强：**
- ? `InflightManager` 维护待确认消息队列，支持超时重发（DUP=1）
- ? 可配置重发超时时间（`RetryTimeout`，默认10秒）和最大重发次数（`MaxRetries`，默认3次）
- ?? 服务端未维护 Packet Identifier 的使用状态表（内存级去重）
- ?? 进程重启后 Inflight 消息丢失（无磁盘持久化）

### 2.3 连接管理

| 功能 | 状态 | 说明 |
|------|------|------|
| 协议名/版本协商 | ? | 支持 3.1/3.1.1/5.0 协议版本枚举 |
| ClientId | ? | 支持自动生成（IP@PID） |
| 用户名/密码认证 | ? | Connect 报文支持，服务端通过 `IMqttAuthenticator` 可插拔验证 |
| Keep Alive 心跳 | ? | 客户端定时 Ping，服务端会话超时清理 |
| Clean Session | ? | CleanSession=0 时服务端恢复订阅关系和推送离线消息（`PersistentSession`） |
| 连接重试/指数退避 | ? | `ReconnectLoopAsync` 完整实现 |
| 最大重连次数 | ? | `MaxReconnectAttempts` |
| 连接/断开事件 | ? | `Connected`/`Disconnected` 事件 |

### 2.4 遗嘱消息（Will/LWT）

| 功能 | 状态 | 说明 |
|------|------|------|
| 遗嘱标志/QoS/Retain | ? | `ConnectMessage` 中完整定义 |
| 遗嘱主题/消息 | ? | `WillTopicName`/`WillMessage` 字段 |
| 客户端遗嘱设置 | ? | `MqttClient.WillTopic`/`WillMessage`/`WillQoS`/`WillRetain` 便捷属性 |
| 遗嘱发送（服务端） | ? | `MqttHandler.Close()` 异常断开时自动发布遗嘱消息到 Exchange 和 Cluster |
| 遗嘱删除（正常断开） | ? | `OnDisconnect` 收到 DISCONNECT 时设置 `_normalDisconnect=true` 并清除遗嘱 |

### 2.5 Retain 消息保留

| 功能 | 状态 | 说明 |
|------|------|------|
| Retain 标志编解码 | ? | `PublishMessage.GetFlag()` 正确保留 Retain 标志位 |
| 服务端存储 Retain 消息 | ? | `MqttExchange.Publish` 自动检测并存储 Retain 消息到 `_retainMessages` |
| 新订阅者收到 Retain 消息 | ? | `MqttExchange.Subscribe` 订阅时自动匹配并推送已保留的消息 |
| 空消息清除 Retain | ? | 空 Payload 的 Retain 消息自动清除对应主题的保留消息 |

### 2.6 主题与过滤

| 功能 | 状态 | 说明 |
|------|------|------|
| 主题层级分隔符 `/` | ? | |
| 多层通配符 `#` | ? | `MqttTopicFilter.IsMatch` |
| 单层通配符 `+` | ? | `MqttTopicFilter.IsMatch` |
| `$` 开头系统主题屏蔽通配符 | ? | 有 `$` 前缀检查 |
| 主题验证 | ? | `IsValidTopicFilter`/`IsValidTopicName` |
| 共享订阅主题解析 | ? | `ExtractActualTopicFilter` 支持 `$share/{group}/` |
| 共享订阅负载均衡 | ? | `MqttExchange.Publish` 中 `$share/` 消息轮询分发给组内活跃订阅者 |

### 2.7 协议编解码

| 功能 | 状态 | 说明 |
|------|------|------|
| 固定头部编解码 | ? | 4位类型 + DUP/QoS/Retain 标志 |
| 可变长度编码（1-4字节） | ? | `ReadEncodedInt`/`WriteEncodedInt` |
| 粘包拆包处理 | ? | `MqttCodec` + `PacketCodec` |
| 请求-响应匹配 | ? | `IsMatch` 方法，支持 Id 匹配和类型配对 |
| UTF-8 字符串编码 | ? | `ReadString`/`WriteString` |
| DUP 标志支持 | ? | `PublishMessage.GetFlag()` 正确保留 DUP 标志，用于消息重发 |

---

## 三、MQTT 5.0 协议功能点对标

### 3.1 新增报文类型

| 报文类型 | 值 | 状态 | 说明 |
|---------|---|------|------|
| AUTH | 15 | ? **已实现** | `AuthMessage` — 支持 ReasonCode（0x00成功/0x18继续认证/0x19重新认证）+ Properties |

### 3.2 属性系统（Properties）

| 功能 | 状态 | 说明 |
|------|------|------|
| 属性基础设施 | ? **完整** | `MqttProperties` 类 — 完整的读写编解码器 |
| 属性标识符定义 | ? **完整** | `MqttPropertyId` 枚举 — 30种属性 ID |
| Byte 类型属性 | ? | PayloadFormatIndicator/RequestProblemInformation/MaximumQoS/RetainAvailable 等 |
| UInt16 类型属性 | ? | ServerKeepAlive/ReceiveMaximum/TopicAliasMaximum/TopicAlias |
| UInt32 类型属性 | ? | MessageExpiryInterval/SessionExpiryInterval/WillDelayInterval/MaximumPacketSize |
| 变长整数类型属性 | ? | SubscriptionIdentifier |
| UTF-8 字符串属性 | ? | ContentType/ResponseTopic/AssignedClientIdentifier/AuthenticationMethod/ReasonString 等 |
| 二进制数据属性 | ? | CorrelationData/AuthenticationData |
| 用户属性（字符串对） | ? | UserProperty — 支持多次出现 |
| CONNECT 属性 | ? | 含遗嘱属性 `WillProperties`，ProtocolLevel≥5 时读写 |
| CONNACK 属性 | ? | 读写完整 |
| PUBLISH 属性 | ? | `Properties` 字段定义 |
| PUBACK/PUBREC/PUBREL/PUBCOMP 属性 | ? | 均有 `ReasonCode` + `Properties` |
| SUBSCRIBE/SUBACK 属性 | ? | 均有 `Properties` 字段 |
| UNSUBSCRIBE/UNSUBACK 属性 | ? | UNSUBACK 有 `ReasonCodes` 列表 + `Properties` |
| DISCONNECT 属性 | ? | `ReasonCode` + `Properties` |
| AUTH 属性 | ? | `ReasonCode` + `Properties` |

### 3.3 MQTT 5.0 核心新功能

| 功能 | 状态 | 说明 |
|------|------|------|
| 会话过期间隔（Session Expiry Interval） | ? | `MqttSessionCapabilities.SessionExpiryInterval` 从 CONNECT 属性提取 |
| 消息过期间隔（Message Expiry Interval） | ? | 属性系统支持读写 `MessageExpiryInterval` |
| 原因码（Reason Code）扩展 | ? | 所有 ACK 报文均有 `ReasonCode` 字段；`ConnAckReasonCode` 枚举定义完整 |
| 原因字符串（Reason String） | ? | 属性系统支持 `ReasonString` |
| 服务端断开（Server Disconnect） | ? | `DisconnectMessage` 支持 `ReasonCode` + `Properties` |
| 请求/响应模式（Request-Response） | ? | 属性系统支持 `ResponseTopic` + `CorrelationData` |
| 共享订阅（Shared Subscription） | ? | 主题解析 + 服务端轮询负载均衡分发 |
| 订阅标识符（Subscription Identifier） | ? | 属性系统支持 `SubscriptionIdentifier`（变长整数） |
| 主题别名（Topic Alias） | ? | `MqttSessionCapabilities` — 客户端→服务端别名映射 + 服务端→客户端别名分配 |
| 流控（Receive Maximum） | ? | `MqttSessionCapabilities.ReceiveMaximum` + `CanSendQosMessage()` 流控检查 |
| 最大报文大小（Maximum Packet Size） | ? | `MqttSessionCapabilities.MaximumPacketSize` 从 CONNECT 属性提取 |
| 服务端保持连接（Server Keep Alive） | ? | 属性系统支持 `ServerKeepAlive` |
| 分配客户端标识符（Assigned Client Identifier） | ? | 属性系统支持 `AssignedClientIdentifier` |
| 用户属性（User Properties） | ? | `MqttProperties.UserProperties` — 键值对列表，可多次出现 |
| 遗嘱延迟间隔（Will Delay Interval） | ? | 属性系统支持 `WillDelayInterval` |
| 载荷格式标识（Payload Format Indicator） | ? | 属性系统支持 `PayloadFormatIndicator` |
| 内容类型（Content Type） | ? | 属性系统支持 `ContentType` |
| 订阅选项（Retain Handling / No Local / Retain As Published） | ?? 部分 | 属性系统支持，但服务端分发逻辑未完整处理订阅选项标志 |
| 增强认证（AUTH 报文） | ? | `AuthMessage` 支持 ReasonCode + AuthenticationMethod/AuthenticationData 属性 |

---

## 四、服务端功能完整性评估

### 4.1 会话管理

| 功能 | 状态 | 说明 |
|------|------|------|
| 会话创建与注册 | ? | `MqttExchange.Add` |
| 会话超时清理 | ? | `RemoveNotAlive` 定时清理 |
| 会话状态持久化（内存级） | ? | `PersistentSession` — 保存订阅关系和离线消息队列 |
| CleanSession=0 会话恢复 | ? | `MqttExchange.RestorePersistentSession` — 恢复订阅并推送离线消息 |
| 离线消息队列 | ? | `MqttExchange.EnqueueOfflineMessage` — 限制1000条，FIFO 淘汰 |
| 磁盘级会话持久化 | ? **未实现** | 进程重启后所有会话丢失（当前仅内存存储） |

### 4.2 消息路由与分发

| 功能 | 状态 | 说明 |
|------|------|------|
| 基于主题的消息路由 | ? | `MqttExchange.Publish` 遍历匹配 |
| 通配符主题匹配 | ? | `MqttTopicFilter.IsMatch` |
| 消息 QoS 降级分发 | ? | 按订阅者声明的 QoS 分发 |
| Retain 消息存储与推送 | ? | 发布时自动存储，订阅时自动推送，空消息自动清除 |
| 遗嘱消息发布 | ? | `MqttHandler.Close()` 异常断开时发布，正常断开时清除 |
| 消息重发机制 | ? | `InflightManager` — 超时重发 + DUP=1 标志 + 最大重试次数 |
| 共享订阅负载均衡 | ? | `$share/` 消息轮询分发给组内活跃订阅者 |
| 消息磁盘持久化 | ? **未实现** | 用于进程重启后 QoS 1/2 消息可靠性保证 |

### 4.3 安全与认证

| 功能 | 状态 | 说明 |
|------|------|------|
| 用户名/密码认证 | ? | 通过 `IMqttAuthenticator.Authenticate` 可插拔实现 |
| TLS/SSL | ? | 支持 X509 证书 |
| ACL（主题级权限控制） | ? | `IMqttAuthenticator.AuthorizePublish`/`AuthorizeSubscribe` 接口 |
| 默认认证器 | ? | `DefaultMqttAuthenticator` — 允许所有操作（开发/测试用） |
| 增强认证（MQTT 5.0 AUTH） | ? | `AuthMessage` 报文已实现，服务端可通过扩展处理 AUTH 流程 |
| 客户端证书认证 | ?? | `Certificate` 属性存在，但无双向认证逻辑 |

### 4.4 MQTT 5.0 会话能力

| 功能 | 状态 | 说明 |
|------|------|------|
| 主题别名解析 | ? | `MqttSessionCapabilities.ResolveTopicAlias` — 客户端→服务端方向 |
| 主题别名分配 | ? | `MqttSessionCapabilities.AssignTopicAlias` — 服务端→客户端方向 |
| 流控（Receive Maximum） | ? | `CanSendQosMessage()` 检查 inflight 消息数 |
| 最大报文大小 | ? | `MaximumPacketSize` 从 CONNECT 属性提取 |
| CONNACK 属性构建 | ? | `BuildConnAckProperties()` 返回服务端能力 |

### 4.5 集群

| 功能 | 状态 | 说明 |
|------|------|------|
| 集群节点发现 | ? | 手动配置 `ClusterNodes` |
| 集群心跳与超时清理 | ? | `DoPing` 定时器 |
| 跨节点订阅同步 | ? | `ClusterExchange.Subscribe` 广播 |
| 跨节点消息转发 | ? | `ClusterExchange.Publish` |
| 集群节点自动发现 | ? | 仅支持手动配置 |
| 集群一致性（脑裂处理） | ? | |

---

## 五、客户端功能完整性评估

| 功能 | 状态 | 说明 |
|------|------|------|
| TCP 连接 | ? | |
| TLS/SSL 连接 | ? | 支持 SslProtocol + 证书 |
| WebSocket 连接 | ? | `WebSocketClientCodec` — 支持 ws:// 连接 |
| WebSocket + TLS 连接 | ? | 支持 wss:// — 自动启用 TLS |
| 发布 QoS 0/1/2 | ? | 含 QoS 2 四步握手（Id 正确传递） |
| 订阅/退订 | ? | 支持多主题、回调绑定 |
| 去重订阅 | ? | `_subs` 字典防重复 |
| 断线重连 | ? | 指数退避 + 最大次数限制 |
| 重连后自动重订阅 | ? | `ConnectAsync` 中重新发送订阅 |
| 心跳 | ? | `TimerX` 定时 Ping |
| 连接字符串初始化 | ? | `Init(String config)` |
| 阿里云适配 | ? | `AliyunMqttClient` + `MqttSign` |
| 遗嘱消息设置 | ? | `WillTopic`/`WillMessage`/`WillQoS`/`WillRetain` 便捷属性 |
| 离线消息接收 | ? | 服务端已支持 CleanSession=0 离线消息推送 |
| MQTT 5.0 协议版本 | ? | `Version` 属性支持 `MqttVersion.V500` |
| 消息重发（DUP） | ?? | 客户端侧未集成 `InflightManager`（服务端已有） |

---

## 六、社区主流扩展功能对比

对比 EMQX、Mosquitto、HiveMQ、MQTTnet 等主流实现：

### 6.1 核心扩展功能

| 功能 | EMQX | Mosquitto | MQTTnet | **NewLife.MQTT** | 状态 |
|------|------|-----------|---------|-----------------|------|
| Retain 消息 | ? | ? | ? | ? | ? 已实现 |
| 遗嘱消息发布 | ? | ? | ? | ? | ? 已实现 |
| 会话持久化（内存） | ? | ? | ? | ? | ? 已实现 |
| 离线消息队列 | ? | ? | ? | ? | ? 已实现 |
| QoS 消息重发 | ? | ? | ? | ? | ? 已实现 |
| MQTT 5.0 属性系统 | ? | ? | ? | ? | ? 已实现 |
| AUTH 增强认证 | ? | ? | ? | ? | ? 已实现 |
| ACL 权限控制 | ? | ? | 插件 | ? | ? 已实现 |
| 共享订阅负载均衡 | ? | ? | 部分 | ? | ? 已实现 |
| $SYS 系统主题 | ? | ? | ? | ? | ? **已实现** |

### 6.2 运维与可观测

| 功能 | EMQX | Mosquitto | **NewLife.MQTT** | 状态 |
|------|------|-----------|-----------------|------|
| 连接数/消息速率统计 | ? | ? | ? | ? **已实现** — `MqttStats` 连接/消息/订阅/字节计数 + 速率计算 |
| 客户端列表查询 | ? | ? | ? | ? **已实现** — `MqttExchange.GetClientIds()` |
| 主题/订阅查询 | ? | ? | ? | ? **已实现** — `MqttExchange.GetTopics()` / `GetSubscriberCount()` |
| $SYS 系统主题 | ? | ? | ? | ? **已实现** — `MqttExchange` 定时发布统计到 `$SYS/` 主题树 |
| HTTP 管理 API | ? | 插件 | ? | ?? **低** |
| Dashboard | ? | 第三方 | ? | ?? **低** |
| Tracer/Metrics 埋点 | ? | ? | ? `ITracer` | — |

### 6.3 高级协议特性

| 功能 | EMQX | Mosquitto | **NewLife.MQTT** | 状态 |
|------|------|-----------|-----------------|------|
| MQTT over QUIC | ? | ? | ? | ? **已实现** — `MqttQuicClient` / `MqttQuicListener`（NET9+） |
| 桥接（Bridge） | ? | ? | ? | ? **已实现** — `MqttBridge` 双向桥接/主题映射/QoS 上限 |
| 规则引擎 | ? | ? | ? | ? **已实现** — `MqttRuleEngine` 主题匹配 + 多种动作 |
| WebHook | ? | ? | ? | ? **已实现** — `MqttWebHook` HTTP POST 推送 + 重试机制 |
| 消息持久化到数据库 | ? | 插件 | ? | ?? **中** |

---

## 七、已知 Bug 与代码问题

### 已修复

| # | 位置 | 问题 | 状态 |
|---|------|------|------|
| 1 | `ConnectMessage.cs` | MQTT 5.0 属性解析 bug：不同属性 ID 值长度不同，但旧代码按固定 5 字节解析 | ? **已修复** — 重构为 `MqttProperties` 类，按 ID 分类型解析 |
| 2 | `MqttClient.cs` | QoS 2 流程中 `new PubRel()` 未设置 `Id` | ? **已修复** — `new PubRel { Id = rec.Id }` |
| 3 | `PublishMessage.cs` | `GetFlag()` 强制将 `Retain = false` | ? **已修复** — 不再强制清零，由调用方控制 |
| 4 | `PublishMessage.cs` | `GetFlag()` 强制将 `Duplicate = false` | ? **已修复** — 不再强制清零，支持消息重发 |
| 5 | `MqttHandler.cs` | `OnDisconnect` 返回 `new DisconnectMessage()` 但 DISCONNECT 不应有响应 | ✅ **已修复** — 返回 `null`，并设置 `_normalDisconnect=true` 清除遗嘱 |
| 6 | `MqttClient.cs` | 客户端侧未集成 `InflightManager`，不支持消息超时重发 | ✅ **已修复** — 客户端 `ConnectAsync` 后初始化 `InflightManager`，`PublishAsync` 中自动加入/移除 Inflight 队列 |
| 7 | `MqttHandler.cs` | 服务端未集成 MQTT 5.0 会话能力（主题别名/流控） | ✅ **已修复** — `OnPublish` 调用 `ResolveTopicAlias`，`PublishAsync` 调用 `CanSendQosMessage` + `AssignTopicAlias` |
| 8 | `MqttExchange.cs` | MQTT 5.0 订阅选项未在分发逻辑中处理 | ✅ **已修复** — `Subscribe` 接受 NoLocal/RetainAsPublished/RetainHandling 参数，`Publish` 中完整处理 |

### 当前已知问题

| # | 位置 | 问题 | 严重度 |
|---|------|------|--------|
| 1 | `MqttExchange` / `PersistentSession` | 会话持久化仅内存级，进程重启后丢失（可通过 `MqttBridge` 或自定义 `IMqttExchange` 实现磁盘持久化） | ⚠️ 低 |

---

## 八、功能实现优先级建议

### 已完成 ?

1. ~~**Retain 消息**~~ ? — `MqttExchange` 中存储/推送/清除保留消息
2. ~~**遗嘱消息发布**~~ ? — 服务端异常断开发布，正常 DISCONNECT 时清除
3. ~~**QoS 2 Bug 修复**~~ ? — PubRel Id 正确传递、Retain/DUP 标志不再强制清零
4. ~~**消息重发机制**~~ ? — `InflightManager` 超时重发 + DUP=1
5. ~~**会话持久化与离线消息**~~ ? — `PersistentSession` + `EnqueueOfflineMessage` + `RestorePersistentSession`
6. ~~**ACL 权限控制框架**~~ ? — `IMqttAuthenticator` 接口 + `DefaultMqttAuthenticator`
7. ~~**MQTT 5.0 属性系统**~~ ? — `MqttProperties` + `MqttPropertyId` 完整实现
8. ~~**MQTT 5.0 原因码**~~ ? — 所有 ACK 报文 + `ConnAckReasonCode` 枚举
9. ~~**共享订阅负载均衡**~~ ? — `$share/` 轮询分发
10. ~~**MQTT 5.0 AUTH 报文**~~ ? — `AuthMessage`
11. ~~**WebSocket 客户端**~~ ? — `WebSocketClientCodec` 支持 ws:// 和 wss://
12. ~~**MQTT 5.0 会话能力**~~ ? — `MqttSessionCapabilities` 主题别名/流控/最大报文

13. ~~**运行统计**~~ ? — `MqttStats` 连接/消息/订阅/字节计数 + 速率计算 + `$SYS` 系统主题发布
14. ~~**消息桥接**~~ ? — `MqttBridge` 双向桥接远端 Broker，支持主题前缀映射和 QoS 上限
15. ~~**WebHook 事件推送**~~ ? — `MqttWebHook` 将连接/发布/订阅等事件通过 HTTP POST 推送到外部系统
16. ~~**规则引擎**~~ ? — `MqttRuleEngine` 主题匹配 + Republish/WebHook/Bridge/Drop/Custom 动作
17. ~~**MQTT over QUIC**~~ ? — `MqttQuicClient` / `MqttQuicListener`（.NET 9+ 平台）

### 下一步优先级 ??（生产加固）

1. **会话磁盘持久化** — 将 `PersistentSession` 持久化到磁盘/数据库，支持进程重启后恢复
2. **客户端 InflightManager 集成** — 客户端侧超时重发
3. **服务端 5.0 能力集成** — 在 `MqttHandler` 中调用 `MqttSessionCapabilities` 进行主题别名解析和流控
4. **消息持久化到数据库** — 用于 QoS 1/2 消息的磁盘级可靠性保证

### 第三优先级 ??（锦上添花）

5. **HTTP 管理 API** — RESTful 接口查询客户端/主题/订阅
6. **Dashboard** — 可视化 Broker 运行状态

---

## 九、测试覆盖

### 9.1 报文编解码测试（`MessageCodecTests`，40个测试）

| 测试类别 | 数量 | 说明 |
|---------|------|------|
| CONNECT/CONNACK 往返 | 5 | 基本连接、遗嘱消息、V5.0协议、拒绝码 |
| PUBLISH 往返 | 7 | QoS0/1/2、Retain标志、DUP标志、空Payload |
| PUBACK/PUBREC/PUBREL/PUBCOMP | 4 | Id 正确传递 |
| SUBSCRIBE/SUBACK 往返 | 3 | 单主题、多主题、GrantedQoS |
| UNSUBSCRIBE/UNSUBACK | 2 | 多主题退订 |
| PING/DISCONNECT | 3 | 基本往返 |
| AUTH (MQTT 5.0) | 2 | 空消息、带 ReasonCode |
| MQTT 5.0 属性系统 | 9 | Byte/UInt16/UInt32/String/Binary/VarInt/UserProperty/多属性/空属性 |
| 标志位正确性 | 4 | QoS/Retain/DUP 标志位编解码 |
| MqttFactory | 1 | 15种报文类型创建 |

### 9.2 集成测试（`MqttIntegrationTests`，19个测试）

| 测试类别 | 数量 | 说明 |
|---------|------|------|
| QoS 全路径 | 3 | QoS0 发布接收、QoS1 PubAck、QoS2 PubComp |
| 发布/订阅 | 2 | 多主题订阅、通配符接收 |
| 连接管理 | 2 | 连接/断开事件、心跳 |
| Retain 消息 | 2 | 存储并推送、空消息清除 |
| 遗嘱消息 | 2 | 异常断开发布、正常断开不发布 |
| MqttExchange | 3 | Retain操作、持久会话、离线消息 |
| ACL 权限控制 | 1 | DefaultMqttAuthenticator |
| InflightManager | 1 | 添加/确认 |
| SessionCapabilities | 3 | CONNECT属性、流控、主题别名 |

### 9.3 运行统计测试（`MqttStatsTests`，5个测试）

| 测试类别 | 数量 | 说明 |
|---------|------|------|
| 线程安全 | 2 | 连接计数器并发递增、消息计数器并发递增 |
| 速率计算 | 1 | 消息速率滑动窗口计算 |
| 运行时长 | 1 | Uptime 递增验证 |
| 最大连接数 | 1 | MaxConnectedClients 高水位跟踪 |

### 9.4 规则引擎测试（`MqttRuleEngineTests`，6个测试）

| 测试类别 | 数量 | 说明 |
|---------|------|------|
| 规则匹配 | 2 | Republish 动作触发、不匹配规则不触发 |
| 消息过滤 | 2 | Drop 规则阻止投递、禁用规则跳过 |
| 统计与排序 | 2 | 命中计数递增、多规则按顺序执行 |

---

## 十、总结

### 完成度评估

| 维度 | 评分 | 说明 |
|------|------|------|
| MQTT 3.1.1 报文编解码 | ????? 100% | 14种报文全部实现，编解码正确，Retain/DUP 标志位完整 |
| MQTT 3.1.1 语义完整性 | ????? 95% | Retain、遗嘱、会话持久化、消息重发、共享订阅均已实现 |
| MQTT 5.0 支持 | ???? 80% | 属性系统完整、AUTH 报文、原因码、主题别名、流控均已实现 |
| 客户端功能 | ????? 95% | TCP/SSL/WS/WSS/QUIC/重连/QoS/遗嘱完整 |
| 服务端功能 | ????? 92% | Retain/遗嘱/会话持久化/ACL/消息重发/共享订阅/统计/$SYS/规则引擎/桥接/WebHook |
| 扩展功能 | ???? 85% | 规则引擎/桥接/WebHook/QUIC/统计 均已实现，缺 HTTP API 和 Dashboard |
| 集群能力 | ??? 65% | 订阅同步和消息转发已实现，缺自动发现和一致性 |
| 云平台适配 | ???? 75% | 阿里云已适配，可扩展其它平台 |
| 测试覆盖 | ????? 90% | 70个单元/集成测试，覆盖编解码、QoS路径、核心功能、统计、规则引擎 |

### 核心结论

项目已从**基础通信框架**提升为**生产可用的 MQTT Broker 和客户端**实现：

1. **MQTT 3.1.1 完整支持**：14种报文编解码、Retain 消息、遗嘱消息、会话持久化（内存级）、消息重发机制、共享订阅负载均衡均已实现。可以标称为"完整支持 MQTT 3.1.1"。

2. **MQTT 5.0 大幅增强**：属性系统完整重构，支持全部数据类型（Byte/UInt16/UInt32/VarInt/String/Binary/UserProperty）。AUTH 报文、原因码扩展、主题别名、流控等核心新功能已实现。所有15种报文类型均支持 5.0 属性扩展。

3. **多传输协议**：TCP/SSL/WebSocket/WSS/QUIC 五种传输方式全覆盖。MQTT over QUIC 基于 .NET 9+ System.Net.Quic API，提供低延迟、无队头阻塞的传输层。

4. **企业级扩展功能**：
   - **运行统计**（`MqttStats`）：连接数/消息速率/订阅数/字节数实时统计，`$SYS` 系统主题自动发布。
   - **消息桥接**（`MqttBridge`）：双向桥接远端 Broker，支持主题前缀映射和 QoS 上限控制。
   - **规则引擎**（`MqttRuleEngine`）：基于主题过滤的消息路由，支持 Republish/WebHook/Bridge/Drop/Custom 五种动作。
   - **WebHook 推送**（`MqttWebHook`）：将 MQTT 事件（连接/断开/发布/订阅）通过 HTTP POST 推送到外部系统，支持重试机制。

5. **服务端安全加固**：ACL 权限控制框架（`IMqttAuthenticator`）提供可插拔的认证和主题级权限检查。

6. **剩余优化方向**：会话磁盘持久化（进程重启恢复）、客户端侧消息重发集成、HTTP 管理 API。
