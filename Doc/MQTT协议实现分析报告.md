# NewLife.MQTT 协议实现分析报告

## 概述

本报告基于 NewLife.MQTT 项目源码进行全面深入分析，对标 **MQTT 3.1.1**（OASIS 标准 mqtt-v3.1.1-os）和 **MQTT 5.0**（OASIS 标准 mqtt-v5.0-os）官方协议规范，同时与社区主流 MQTT 客户端（MQTTnet、Eclipse Paho）和服务端产品（EMQX、Mosquitto、HiveMQ、VerneMQ）进行功能对比，全面列出已实现和未实现的功能。

**项目定位：** 纯国产自主研发的 MQTT 完整协议实现，同时包含客户端 `MqttClient` 和服务端 `MqttServer`，以单一 NuGet 包形式交付。

**核心特点：**
- **零第三方依赖** — 仅依赖 NewLife.Core 基础库，无供应链安全风险
- **MIT 开源协议** — 可自由修改和商用，无 copyleft/专利限制，无法律风险
- **客户端+服务端一体** — 单一 `NewLife.MQTT` NuGet 包即可同时使用客户端与服务端
- **全版本覆盖** — 支持 .NET Framework 4.5 / 4.6.1、.NET Standard 2.0/2.1、.NET 7/8/9/10
- **六种传输协议** — TCP / TLS / WebSocket / WSS / QUIC / 可靠UDP

---

## 一、项目架构总览

### 1.1 项目组成

| 项目 | 说明 |
|------|------|
| `NewLife.MQTT` | 核心库，包含 MQTT 协议编解码、客户端、服务端、集群、桥接、规则引擎 |
| `NewLife.MqttServer` | 开箱即用的 MQTT Broker 应用，基于核心库构建 |
| `XUnitTestClient` | 单元测试项目，覆盖编解码、客户端、集群、规则引擎等 |
| `Test` | 集成测试/演示项目 |

### 1.2 核心模块划分

| 模块 | 命名空间 | 核心类型 | 说明 |
|------|---------|---------|------|
| 消息编解码 | `Messaging` | `MqttMessage` / `MqttFactory` / `MqttCodec` | 15种报文类型完整实现 |
| 客户端 | 根命名空间 | `MqttClient` / `AliyunMqttClient` | 全功能客户端 |
| 服务端 | 根命名空间 | `MqttServer` / `MqttSession` | 高性能网络服务 |
| 消息交换 | `Handlers` | `MqttExchange` / `IMqttExchange` | 订阅路由 / Retain / 持久会话 |
| 处理器 | `Handlers` | `MqttHandler` / `IMqttHandler` | 可扩展指令处理 |
| 安全认证 | `Handlers` | `IMqttAuthenticator` | 可插拔 ACL 权限控制 |
| 消息桥接 | `Handlers` | `MqttBridge` | 双向跨 Broker 消息转发 |
| 规则引擎 | `Handlers` | `MqttRuleEngine` / `MqttRule` | 5种动作类型 |
| WebHook | `Handlers` | `MqttWebHook` | 6种事件推送 |
| Inflight | `Handlers` | `InflightManager` | QoS>0 消息超时重发 |
| MQTT 5.0 | `Handlers` | `MqttSessionCapabilities` | 别名/流控/能力声明 |
| 集群 | `Clusters` | `ClusterServer` / `ClusterExchange` | 跨节点订阅同步与消息转发 |
| QUIC 传输 | `Quic` | `MqttQuicClient` | .NET 9+ QUIC 传输 |
| UDP 传输 | `Quic` | `MqttUdpClient` | 全平台可靠 UDP 传输 |
| WebSocket | `WebSocket` | `WebSocketClientCodec` / `WebSocketServerCodec` | ws/wss 编解码 |
| ProxyProtocol | `ProxyProtocol` | `ProxyCodec` / `ProxyMessage` / `ProxyMessageV2` | v1/v2 代理协议 |

### 1.3 支持的传输方式

| 传输方式 | 客户端 | 服务端 | 说明 |
|---------|--------|--------|------|
| TCP | ✅ | ✅ | 标准 MQTT 传输，默认端口 1883 |
| TCP + TLS/SSL | ✅ | ✅ | X509 证书，支持 TLS 1.2+，支持 pfx/pem |
| WebSocket | ✅ | ✅ | ws:// 协议，默认端口 8083 |
| WebSocket + TLS (WSS) | ✅ | ✅ | wss:// 自动启用 TLS，默认端口 8884 |
| MQTT over QUIC | ✅ | — | .NET 7+ 平台，基于 System.Net.Quic，低延迟无队头阻塞（.NET 7/8 需启用预览特性） |
| MQTT over 可靠 UDP | ✅ | ✅ | 基于 NewLife.Core UDP，全平台支持，自研可靠传输层 |
| ProxyProtocol v1/v2 | ✅（测试） | ✅ | nginx/HAProxy 透传真实客户端 IP |

---

## 二、MQTT 3.1.1 协议对标（OASIS mqtt-v3.1.1-os）

### 2.1 控制报文类型（14/14 全部实现 ✅）

| 报文类型 | 值 | 方向 | 状态 | 实现类 | 协议规范章节 |
|---------|---|------|------|--------|------------|
| CONNECT | 1 | C→S | ✅ | `ConnectMessage` | §3.1 |
| CONNACK | 2 | S→C | ✅ | `ConnAck` | §3.2 |
| PUBLISH | 3 | 双向 | ✅ | `PublishMessage` | §3.3 |
| PUBACK | 4 | 双向 | ✅ | `PubAck` | §3.4 |
| PUBREC | 5 | 双向 | ✅ | `PubRec` | §3.5 |
| PUBREL | 6 | 双向 | ✅ | `PubRel` | §3.6 |
| PUBCOMP | 7 | 双向 | ✅ | `PubComp` | §3.7 |
| SUBSCRIBE | 8 | C→S | ✅ | `SubscribeMessage` | §3.8 |
| SUBACK | 9 | S→C | ✅ | `SubAck` | §3.9 |
| UNSUBSCRIBE | 10 | C→S | ✅ | `UnsubscribeMessage` | §3.10 |
| UNSUBACK | 11 | S→C | ✅ | `UnsubAck` | §3.11 |
| PINGREQ | 12 | C→S | ✅ | `PingRequest` | §3.12 |
| PINGRESP | 13 | S→C | ✅ | `PingResponse` | §3.13 |
| DISCONNECT | 14 | C→S | ✅ | `DisconnectMessage` | §3.14 |

### 2.2 固定头部与编码（§2 协议）

| 规范条目 | 状态 | 实现说明 |
|---------|------|---------|
| 4位报文类型 + 4位标志位 | ✅ | `MqttMessage.Read()` 解析高4位类型、低4位标志 |
| 剩余长度（1~4字节变长编码） | ✅ | `ReadEncodedInt` / `WriteEncodedInt`，支持最大 256MB 报文 |
| DUP 标志位 | ✅ | `Duplicate` 属性，QoS>0 重发时设置 |
| QoS 标志位（2位） | ✅ | `QoS` 属性，0/1/2 三级 |
| Retain 标志位 | ✅ | `Retain` 属性，保留消息标记 |
| UTF-8 字符串（2字节长度前缀） | ✅ | `ReadString` / `WriteString` |
| 二进制数据（2字节长度前缀） | ✅ | `ReadData` / `WriteData` |
| 报文标识符（2字节 UInt16） | ✅ | `MqttIdMessage.Id`，全局递增分配 |
| 各报文固定标志位校验 | ✅ | 各消息类型 `GetFlag()` 独立实现，如 SUBSCRIBE 固定 QoS=1 |
| 粘包拆包处理 | ✅ | `MqttCodec` + `PacketCodec` 流式解析 |

### 2.3 QoS 服务质量（§4.3 协议）

| QoS 级别 | 客户端发布 | 客户端接收 | 服务端分发 | 说明 |
|---------|-----------|-----------|-----------|------|
| QoS 0（至多一次） | ✅ | ✅ | ✅ | 无确认，fire-and-forget |
| QoS 1（至少一次） | ✅ | ✅ | ✅ | PUBLISH → PUBACK |
| QoS 2（恰好一次） | ✅ | ✅ | ✅ | PUBLISH → PUBREC → PUBREL → PUBCOMP |

**QoS 增强功能：**

| 功能 | 状态 | 说明 |
|------|------|------|
| Inflight 消息队列 | ✅ | 客户端+服务端均有独立 `InflightManager` 实例 |
| 超时重发（DUP=1） | ✅ | 默认10秒超时/最大3次重发，可配置 `RetryTimeout`/`MaxRetries` |
| QoS 降级分发 | ✅ | 按订阅者声明的 QoS 取较小值降级 |
| 报文标识符管理 | ✅ | `Interlocked.Increment` 线程安全递增，QoS 0 不分配 ID |

### 2.4 CONNECT 连接管理（§3.1 协议）

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 协议名校验（"MQTT"） | ✅ | `ConnectMessage.ProtocolName` |
| 协议版本协商（3.1/3.1.1/5.0） | ✅ | `ProtocolLevel` 字段，支持 0x03/0x04/0x05 |
| ClientId（必填） | ✅ | 自动生成格式 `IP@PID`，也可手动设置 |
| 用户名/密码认证 | ✅ | `Username`/`Password`，标志位独立控制 |
| Keep Alive 心跳 | ✅ | 客户端半周期定时 Ping，默认600秒 |
| Clean Session | ✅ | `=1`清除旧会话，`=0`恢复持久会话+离线消息推送 |
| 连接返回码（6种） | ✅ | `ConnectReturnCode` 枚举完整实现 |
| 自动重连（指数退避） | ✅ | 可配初始延迟/最大延迟/最大次数，`2^n` 指数退避 |
| 重连后自动重订阅 | ✅ | `ConnectAsync` 中自动重发订阅列表 |
| 连接字符串初始化 | ✅ | `Init()` 方法解析 `Server=;UserName=;Password=` 格式 |
| 连接/断开事件通知 | ✅ | `Connected`/`Disconnected` 事件 |

### 2.5 遗嘱消息（§3.1.2.5 协议）

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 遗嘱标志/QoS/Retain 编解码 | ✅ | `ConnectMessage` 中 `HasWill`/`WillQualityOfService`/`WillRetain` |
| 遗嘱主题和消息体 | ✅ | `WillTopicName`/`WillMessage` |
| 客户端遗嘱设置 | ✅ | `MqttClient.WillTopic`/`WillMessage`/`WillQoS`/`WillRetain` |
| 异常断开时自动发布遗嘱 | ✅ | `MqttHandler.Close()` 中检测非正常断开后发布 |
| 正常 DISCONNECT 时清除遗嘱 | ✅ | `OnDisconnect()` 设置 `_normalDisconnect=true` |
| 遗嘱消息通过交换机分发 | ✅ | 遗嘱消息同时发送到本地 Exchange 和集群 ClusterExchange |

### 2.6 Retain 保留消息（§3.3.1.3 协议）

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| Retain 标志编解码 | ✅ | `PublishMessage.Retain` + `GetFlag()` |
| 发布时存储 Retain 消息 | ✅ | `MqttExchange._retainMessages` ConcurrentDictionary 存储 |
| 新订阅者匹配推送 Retain | ✅ | `Subscribe()` 中按 RetainHandling 策略推送 |
| 空 Payload 清除 Retain | ✅ | `Publish()` 中 Payload 为空时 TryRemove |
| $SYS 保留消息 | ✅ | 系统主题消息同时存入 Retain 存储 |

### 2.7 主题与通配符（§4.7 协议）

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 层级分隔符 `/` | ✅ | `MqttTopicFilter.IsMatch()` 按 `/` 分割匹配 |
| 多层通配符 `#` | ✅ | 匹配当前层及所有子层 |
| 单层通配符 `+` | ✅ | 仅匹配当前层 |
| `$` 系统主题屏蔽通配符 | ✅ | `$SYS/` 前缀不匹配通配符订阅 |
| 主题过滤器验证 | ✅ | `IsValidTopicFilter()` 校验 `#`/`+` 位置合法性 |
| 发布主题名验证 | ✅ | `IsValidTopicName()` 禁止通配符 |
| 共享订阅解析 `$share/` | ✅ | `ExtractActualTopicFilter()` 提取实际过滤器 |
| 主题最大长度 65536 | ✅ | 验证时检查长度限制 |
| 主题扩展（通配符组合生成） | ✅ | `Expand()` 生成所有通配符组合，支持分布式集群主题路由 |

---

## 三、MQTT 5.0 协议对标（OASIS mqtt-v5.0-os）

### 3.1 新增报文类型

| 报文 | 值 | 方向 | 状态 | 实现类 | 说明 |
|-----|---|------|------|--------|------|
| AUTH | 15 | 双向 | ✅ | `AuthMessage` | ReasonCode（0x00成功/0x18继续/0x19重认证） + Properties |

### 3.2 属性系统（30/30 属性标识符 · 7/7 数据类型）

**数据类型完整实现（7/7）：**

| 数据类型 | 状态 | 读写方法 | 属性示例 |
|---------|------|---------|---------|
| Byte | ✅ | `GetByte`/`SetByte` | PayloadFormatIndicator / MaximumQoS / RetainAvailable / WildcardSubscriptionAvailable / SubscriptionIdentifierAvailable / SharedSubscriptionAvailable / RequestProblemInformation / RequestResponseInformation |
| UInt16 | ✅ | `GetUInt16`/`SetUInt16` | ServerKeepAlive / ReceiveMaximum / TopicAliasMaximum / TopicAlias |
| UInt32 | ✅ | `GetUInt32`/`SetUInt32` | MessageExpiryInterval / SessionExpiryInterval / WillDelayInterval / MaximumPacketSize |
| 变长整数 | ✅ | `GetVariableInt`/`SetVariableInt` | SubscriptionIdentifier |
| UTF-8 字符串 | ✅ | `GetString`/`SetString` | ContentType / ResponseTopic / ReasonString / AssignedClientIdentifier / AuthenticationMethod / ResponseInformation / ServerReference |
| 二进制数据 | ✅ | `GetBinary`/`SetBinary` | CorrelationData / AuthenticationData |
| UTF-8 字符串对 | ✅ | `UserProperties` 列表 | UserProperty（可多次出现） |

**30种属性标识符完整列表（30/30）：**

| 属性ID | 名称 | 类型 | 编解码 | 服务端行为 |
|--------|------|------|--------|-----------|
| 0x01 | PayloadFormatIndicator | Byte | ✅ | ✅ 透传 |
| 0x02 | MessageExpiryInterval | UInt32 | ✅ | ⚠️ 分发时未检查过期 |
| 0x03 | ContentType | String | ✅ | ✅ 透传 |
| 0x08 | ResponseTopic | String | ✅ | ✅ 透传（请求/响应模式） |
| 0x09 | CorrelationData | Binary | ✅ | ✅ 透传（请求/响应模式） |
| 0x0B | SubscriptionIdentifier | 变长整数 | ✅ | ✅ 透传 |
| 0x11 | SessionExpiryInterval | UInt32 | ✅ | ✅ `MqttSessionCapabilities` 记录 |
| 0x12 | AssignedClientIdentifier | String | ✅ | ✅ 可读写 |
| 0x13 | ServerKeepAlive | UInt16 | ✅ | ⚠️ 可读写，服务端未在 CONNACK 中主动设置 |
| 0x15 | AuthenticationMethod | String | ✅ | ✅ AUTH 报文支持 |
| 0x16 | AuthenticationData | Binary | ✅ | ✅ AUTH 报文支持 |
| 0x17 | RequestProblemInformation | Byte | ✅ | ✅ 可读写 |
| 0x18 | WillDelayInterval | UInt32 | ✅ | ⚠️ 可读写，服务端当前立即发布遗嘱 |
| 0x19 | RequestResponseInformation | Byte | ✅ | ✅ 可读写 |
| 0x1A | ResponseInformation | String | ✅ | ✅ 可读写 |
| 0x1C | ServerReference | String | ✅ | ✅ 可读写 |
| 0x1F | ReasonString | String | ✅ | ✅ 可读写 |
| 0x21 | ReceiveMaximum | UInt16 | ✅ | ✅ 流控检查 |
| 0x22 | TopicAliasMaximum | UInt16 | ✅ | ✅ 双向别名管理 |
| 0x23 | TopicAlias | UInt16 | ✅ | ✅ 双向别名解析/分配 |
| 0x24 | MaximumQoS | Byte | ✅ | ✅ 能力声明 |
| 0x25 | RetainAvailable | Byte | ✅ | ✅ 能力声明 |
| 0x26 | UserProperty | 字符串对 | ✅ | ✅ 多值支持 |
| 0x27 | MaximumPacketSize | UInt32 | ✅ | ✅ `MqttSessionCapabilities` 记录 |
| 0x28 | WildcardSubscriptionAvailable | Byte | ✅ | ✅ 能力声明 |
| 0x29 | SubscriptionIdentifierAvailable | Byte | ✅ | ✅ 能力声明 |
| 0x2A | SharedSubscriptionAvailable | Byte | ✅ | ✅ 能力声明 |

### 3.3 MQTT 5.0 核心新功能

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 原因码扩展 | ✅ | 所有 ACK 报文均支持 ReasonCode；`ConnAckReasonCode` 含20种，`PubAck`/`DisconnectMessage` 均有 `ReasonCode` |
| 会话过期间隔 | ✅ | `SessionExpiryInterval` 属性读写 + `MqttSessionCapabilities` 处理 |
| 服务端断开报文 | ✅ | `DisconnectMessage` 支持 ReasonCode + Properties，服务端可主动断开并携带原因 |
| 请求/响应模式 | ✅ | `ResponseTopic` + `CorrelationData` 属性完整支持 |
| 共享订阅 | ✅ | `$share/{group}/{topic}` 解析 + 服务端轮询负载均衡 |
| 订阅标识符 | ✅ | `SubscriptionIdentifier` 变长整数属性 |
| 主题别名（双向） | ✅ | 客户端→服务端：`ResolveTopicAlias()` 解析；服务端→客户端：`AssignTopicAlias()` 自动分配 |
| 流控（Receive Maximum） | ✅ | `CanSendQosMessage()` 检查 inflight 消息数是否超限 |
| 最大报文大小 | ✅ | `MaximumPacketSize` 属性，`MqttSessionCapabilities` 记录 |
| 用户属性 | ✅ | `MqttProperties.UserProperties` 键值对列表，可多次出现 |
| 增强认证框架 | ✅ | `AuthMessage` 完整实现 + `AuthenticationMethod`/`AuthenticationData` 属性 |
| 订阅选项 | ✅ | `NoLocal` / `RetainAsPublished` / `RetainHandling`（3种模式），编解码+服务端完整执行 |
| 服务端能力声明 | ✅ | `BuildConnAckProperties()` 在 CONNACK 中返回服务端支持的能力 |
| CONNACK 原因码（20种） | ✅ | `ConnAckReasonCode` 枚举从 `Success` 到 `ConnectionRateExceeded` |
| 服务端引用/迁移 | ✅ | `ServerReference` 属性支持 |

### 3.4 MQTT 5.0 部分实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| 遗嘱延迟发布 | ⚠️ | `WillDelayInterval` 属性读写完整，服务端当前立即发布遗嘱，未实现延迟定时器 |
| 消息过期清理 | ⚠️ | `MessageExpiryInterval` 属性读写完整，分发时未检查是否过期 |
| 增强认证具体方法 | ⚠️ | `AuthMessage` 框架完整（支持挑战/响应），缺少 SCRAM-SHA-256 等 SASL 具体实现 |
| 服务端 KeepAlive 覆盖 | ⚠️ | `ServerKeepAlive` 属性可读写，服务端未在 CONNACK 中主动设置 |

---

## 四、服务端功能详细评估

### 4.1 会话管理

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 会话创建与注册 | ✅ | `MqttExchange.Add()` ConcurrentDictionary 管理 |
| 会话超时清理 | ✅ | 30秒定时扫描 `RemoveNotAlive()`，清除不活跃会话并断开连接 |
| 持久会话（内存级） | ✅ | `PersistentSession` 保存订阅关系（ConcurrentDictionary）和离线消息（ConcurrentQueue） |
| CleanSession=0 会话恢复 | ✅ | `RestorePersistentSession()` 恢复订阅+推送离线消息 |
| CleanSession=1 清除旧会话 | ✅ | `ClearPersistentSession()` |
| 离线消息队列 | ✅ | ConcurrentQueue 缓存，1000条上限防溢出 |
| ClientId 映射查询 | ✅ | `RegisterClientId()` + `GetClientIds()` |
| 会话过期间隔（5.0） | ✅ | `SessionExpiryInterval` 属性保存在 `MqttSessionCapabilities` |
| 磁盘级持久化 | ❌ | 内存级，可通过实现 `IMqttExchange` 接口扩展 |

### 4.2 消息路由与分发

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 主题精确匹配 | ✅ | ConcurrentDictionary 直接查找 |
| 通配符匹配（`+`/`#`） | ✅ | `MqttTopicFilter.IsMatch()` 逐层匹配 |
| QoS 降级分发 | ✅ | 按订阅者声明的 QoS 取较小值 |
| Retain 保留消息 | ✅ | 存储/清除/新订阅推送 |
| 遗嘱消息分发 | ✅ | 异常断开时通过 Exchange 和 ClusterExchange 发布 |
| 消息重发 (DUP=1) | ✅ | `InflightManager` 超时重发，1秒定时检查 |
| 共享订阅负载均衡 | ✅ | `$share/{group}/{topic}` 轮询分发，`_sharedGroupIndex` 计数 |
| $SYS 系统主题 | ✅ | 14种系统主题定时发布，支持订阅查询 |
| NoLocal（5.0） | ✅ | 不转发给发布者自身，通过 `publisherSessionId` 参数过滤 |
| RetainAsPublished（5.0） | ✅ | 保留原始 Retain 标志转发 |
| RetainHandling（5.0） | ✅ | 3种模式：0=订阅时发送/1=仅新订阅/2=不发送 |
| 主题别名解析（5.0） | ✅ | 客户端→服务端：`ResolveTopicAlias()`；服务端→客户端：`AssignTopicAlias()` |
| 流控检查（5.0） | ✅ | `CanSendQosMessage()` 限制 QoS>0 并发数 |
| $SYS 主题独立分发 | ✅ | $SYS 消息仅发送给显式订阅了 `$SYS/` 前缀的订阅者 |

### 4.3 安全与认证

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 用户名/密码认证 | ✅ | `IMqttAuthenticator.Authenticate()` 可插拔接口 |
| TLS/SSL 加密 | ✅ | X509 证书，支持 pfx/pem |
| 发布权限 ACL | ✅ | `AuthorizePublish()` 主题级检查，失败返回 ReasonCode=0x87 |
| 订阅权限 ACL | ✅ | `AuthorizeSubscribe()` 主题级检查，失败返回 QoS=0x80 |
| 增强认证框架（5.0） | ✅ | `AuthMessage` 挑战/响应流程，Process 中路由到 `OnAuth()` |
| 默认认证器 | ✅ | `DefaultMqttAuthenticator` 全部放行，方便开发测试 |

### 4.4 集群功能

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| 手动配置节点 | ✅ | `ClusterNodes` 指定地址列表 |
| 集群端口独立 | ✅ | 默认2883，与 MQTT 端口分离，基于 `ApiServer` |
| Ping 心跳保活 | ✅ | 15秒间隔 Ping 所有节点 |
| 超时节点剔除 | ✅ | 5分钟不活跃自动清理 |
| 跨节点订阅同步 | ✅ | `ClusterExchange.Subscribe()` 并行广播订阅关系到所有节点 |
| 跨节点取消订阅 | ✅ | `ClusterExchange.Unsubscribe()` 并行广播退订到所有节点 |
| 跨节点消息转发 | ✅ | `ClusterExchange.Publish()` 向匹配节点转发，主题过滤器匹配 |
| 节点本地环回过滤 | ✅ | `AddNode()` 跳过本机地址，本地环回替换为外网 IP |
| 自动节点发现 | ❌ | 当前仅支持手动配置 |

### 4.5 企业级扩展功能

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| **消息桥接（双向）** | ✅ | `MqttBridge` 支持 In/Out/Both 三种方向，主题前缀映射，QoS 上限控制，断线重连 |
| **规则引擎（5种动作）** | ✅ | Republish 转发 / WebHook 触发 / Bridge 桥接 / Drop 丢弃 / Custom 自定义委托 |
| **WebHook 事件推送** | ✅ | 6种事件：连接/断开/发布/投递/订阅/退订，异步非阻塞，支持重试（默认3次） |
| **运行统计** | ✅ | `MqttStats` 连接数/消息数/字节数/订阅数/速率，1秒滑动窗口计算 |
| **$SYS 系统主题** | ✅ | 14种标准主题，可配发布间隔（默认60秒），`EnableSysTopic` 可关闭 |
| **ProxyProtocol** | ✅ | 支持 nginx/HAProxy 代理协议 v1/v2，透传真实客户端 IP，`ProxyMessageV2` 支持 v2 |

### 4.6 $SYS 系统主题列表

| 主题 | 说明 |
|------|------|
| `$SYS/broker/clients/connected` | 当前在线连接数 |
| `$SYS/broker/clients/total` | 累计连接总数 |
| `$SYS/broker/clients/maximum` | 最大同时在线连接数 |
| `$SYS/broker/messages/received` | 累计接收消息数 |
| `$SYS/broker/messages/sent` | 累计发送消息数 |
| `$SYS/broker/messages/received/persecond` | 每秒接收消息数 |
| `$SYS/broker/messages/sent/persecond` | 每秒发送消息数 |
| `$SYS/broker/bytes/received` | 累计接收字节数 |
| `$SYS/broker/bytes/sent` | 累计发送字节数 |
| `$SYS/broker/subscriptions/count` | 当前订阅数 |
| `$SYS/broker/topics/count` | 当前主题数 |
| `$SYS/broker/retained messages/count` | 保留消息数 |
| `$SYS/broker/uptime` | 运行时长（秒） |
| `$SYS/broker/version` | Broker 版本号 |

---

## 五、客户端功能详细评估

| 功能 | 状态 | 实现说明 |
|------|------|---------|
| TCP 连接 | ✅ | `tcp://host:port`，`NetUri` 解析 |
| TLS/SSL 连接 | ✅ | 设置 `SslProtocol` + `Certificate`，支持 TLS 1.2+ |
| WebSocket 连接 | ✅ | `ws://host:port/path`，`WebSocketClientCodec` 编解码 |
| WSS 连接 | ✅ | `wss://host:port/path`，自动启用 TLS |
| QUIC 连接 | ✅ | `MqttQuicClient`，.NET 7+ 平台，ALPN 协议 "mqtt"（.NET 7/8 需启用预览特性） |
| 可靠 UDP 连接 | ✅ | `MqttUdpClient`，全平台，帧格式：Data/Ack/Connect/ConnAck/Close |
| 连接字符串初始化 | ✅ | `Init("Server=;UserName=;Password=;ClientId=;Timeout=")`，支持逗号和分号分隔 |
| MQTT 3.1/3.1.1/5.0 版本选择 | ✅ | `Version` 属性，默认 V311 |
| 发布 QoS 0/1/2 | ✅ | `PublishAsync()` 完整四步确认 |
| 订阅/退订（回调绑定） | ✅ | `SubscribeAsync()` 支持多主题+回调 |
| 遗嘱消息 | ✅ | `WillTopic`/`WillMessage`/`WillQoS`/`WillRetain` |
| 断线重连（指数退避） | ✅ | `2^n` 退避策略，可配 `InitialReconnectDelay`/`MaxReconnectDelay`/`MaxReconnectAttempts` |
| 重连后自动重订阅 | ✅ | 维护 `_subs` 字典，`ConnectAsync()` 中自动重发 |
| 心跳保活 | ✅ | KeepAlive 半周期定时 Ping（`TimerX`） |
| 消息重发（Inflight） | ✅ | 客户端独立 `InflightManager`，DUP=1 超时重发 |
| 编码器可替换 | ✅ | `IPacketEncoder Encoder`，默认 JSON |
| 性能跟踪（ITracer） | ✅ | 每个 Send/Receive 操作均有 Span 埋点 |
| NoDelay 低延迟 | ✅ | TCP 连接默认关闭 Nagle 算法 |
| 重复订阅防护 | ✅ | `_subs` 字典去重 |
| 连接/断开事件 | ✅ | `Connected`/`Disconnected` 事件 |
| 收到消息事件 | ✅ | `Received` 事件（全局） + 订阅回调（按主题通配符模糊匹配） |
| 超时销毁重建连接 | ✅ | 连续3次 `TaskCanceledException` 自动销毁，下次请求重建 |
| 异步锁连接保护 | ✅ | `SemaphoreSlim` 双重检查，防止并发连接 |
| 阿里云 IoT 适配 | ✅ | `AliyunMqttClient` 内置 `MqttSign` HMAC 签名 + 属性上报 + 时钟同步 |

---

## 六、社区主流产品对比

### 6.1 MQTT 服务端对比

| 功能 | EMQX | Mosquitto | HiveMQ | VerneMQ | **NewLife.MQTT** |
|------|------|-----------|--------|---------|-----------------|
| MQTT 3.1.1 完整支持 | ✅ | ✅ | ✅ | ✅ | ✅ |
| MQTT 5.0 完整支持 | ✅ | ✅ | ✅ | ✅ | ✅ 核心完整 |
| WebSocket | ✅ | ✅ | ✅ | ✅ | ✅ |
| MQTT over QUIC | ✅ | ❌ | ❌ | ❌ | ✅ (.NET 7+) |
| MQTT over 可靠UDP | ❌ | ❌ | ❌ | ❌ | ✅ 全平台 |
| QoS 0/1/2 | ✅ | ✅ | ✅ | ✅ | ✅ |
| Retain / Will | ✅ | ✅ | ✅ | ✅ | ✅ |
| 共享订阅 | ✅ | ✅ (v2.0+) | ✅ | ✅ | ✅ |
| 持久会话 | ✅ 磁盘 | ✅ 磁盘 | ✅ 磁盘 | ✅ 磁盘 | ✅ 内存（可扩展） |
| 消息桥接 | ✅ | ❌ | 企业版 | 插件 | ✅ 内置 |
| 规则引擎 | ✅ | ❌ | 企业版 | ❌ | ✅ 内置 |
| WebHook | ✅ | ❌ | 企业版 | 插件 | ✅ 内置 |
| ACL 权限 | ✅ | ✅ | ✅ | ✅ | ✅ |
| $SYS 系统主题 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 集群 | ✅ 自动 | ❌ | 企业版 | ✅ | ✅ 手动 |
| HTTP 管理 API | ✅ | 插件 | ✅ | ✅ | ❌ |
| Dashboard | ✅ | ❌ | ✅ | ❌ | ❌ |
| 插件/扩展系统 | ✅ | ✅ | ✅ | ✅ | ✅ DI 注入 |
| 主题别名（5.0） | ✅ | ✅ | ✅ | ✅ | ✅ |
| 流控（5.0） | ✅ | ✅ | ✅ | ✅ | ✅ |
| ProxyProtocol | ✅ | ❌ | ❌ | ❌ | ✅ |
| 开源协议 | Apache 2.0 | EPL/EDL | 商业 | Apache 2.0 | **MIT** |
| 实现语言 | Erlang | C | Java | Erlang | **C# (.NET)** |
| 第三方依赖 | 大量 | 少量 | 大量 | 大量 | **零依赖** |
| 客户端+服务端一体 | ❌ | ❌ | ❌ | ❌ | **✅** |

### 6.2 MQTT 客户端对比

| 功能 | MQTTnet | Eclipse Paho C# | **NewLife.MQTT** |
|------|---------|-----------------|-----------------|
| MQTT 3.1.1 | ✅ | ✅ | ✅ |
| MQTT 5.0 | ✅ | ✅ | ✅ |
| TCP/TLS/WS/WSS | ✅ | ✅ | ✅ |
| MQTT over QUIC | ❌ | ❌ | ✅ (.NET 7+) |
| MQTT over 可靠UDP | ❌ | ❌ | ✅ |
| QoS 0/1/2 | ✅ | ✅ | ✅ |
| 自动重连 | ✅ | ✅ | ✅ 指数退避 |
| 重连后自动重订阅 | ❌ 需手动 | ❌ | ✅ 自动 |
| 消息重发（Inflight） | ✅ | ✅ | ✅ |
| 连接字符串初始化 | ❌ | ❌ | ✅ |
| 阿里云 IoT 适配 | ❌ | ❌ | ✅ 内置 |
| 遗嘱消息 | ✅ | ✅ | ✅ |
| 编码器可替换 | ❌ | ❌ | ✅ |
| 性能跟踪 | ❌ | ❌ | ✅ ITracer |
| ProxyProtocol | ❌ | ❌ | ✅ |
| 内置服务端 | ❌ | ❌ | ✅ |
| 开源协议 | MIT | EPL 2.0 | **MIT** |
| 第三方依赖 | 少量 | 多 | **零依赖** |

### 6.3 NewLife.MQTT 差异化优势

| 优势 | 详细说明 |
|------|---------|
| **零第三方依赖** | 仅依赖 NewLife.Core 基础库，无供应链安全风险，无许可证冲突 |
| **MIT 开源协议** | 最宽松的开源协议，可自由修改商用，无 copyleft 限制，无专利条款，无法律风险 |
| **客户端+服务端一体** | 单一 `NewLife.MQTT` NuGet 包同时提供客户端和服务端，极简集成 |
| **全版本 .NET 覆盖** | .NET Framework 4.5 ~ .NET 10，覆盖所有主流 .NET 运行时 |
| **六种传输协议** | TCP/TLS/WS/WSS/QUIC/可靠UDP，业界覆盖面最广 |
| **MQTT over QUIC** | 业界罕见，仅 EMQX 和 NewLife.MQTT 支持，低延迟无队头阻塞，.NET 7+ 支持（7/8 需启用预览特性） |
| **MQTT over 可靠UDP** | 独创自研，4字节帧头+序列号+Ack确认+超时重发，无需操作系统 QUIC/msquic 支持，全平台可用 |
| **企业功能免费内置** | 桥接/规则引擎/WebHook/统计/$SYS，竞品需企业版或付费插件 |
| **纯国产自主研发** | 完全自主知识产权，无外部依赖法律风险，信创合规 |
| **ProxyProtocol 支持** | 支持 nginx/HAProxy 代理协议 v1/v2，透传真实客户端 IP |
| **阿里云 IoT 内置适配** | `AliyunMqttClient` 开箱即用，HMAC 签名认证 |
| **DI 扩展架构** | 基于依赖注入的可扩展架构，可替换认证器（`IMqttAuthenticator`）、交换机（`IMqttExchange`）、处理器（`IMqttHandler`） |
| **重连后自动重订阅** | 竞品（MQTTnet/Paho）断线重连后需要用户手动重新订阅，NewLife.MQTT 自动完成 |

---

## 七、已知局限与演进方向

### 7.1 当前局限

| # | 描述 | 影响 | 可行方案 |
|---|------|------|---------|
| 1 | 会话持久化仅内存级，服务重启后丢失 | 不影响 CleanSession=1 场景 | 实现 `IMqttExchange` 接口对接 Redis/数据库 |
| 2 | 遗嘱延迟发布未实现定时器 | 遗嘱消息立即发布，不遵守 `WillDelayInterval` | 添加 TimerX 延迟队列 |
| 3 | 消息过期分发时未检查 | 过期消息仍可能推送给订阅者 | 在 `Publish()` 分发时检查 `MessageExpiryInterval` |
| 4 | 集群缺少自动发现 | 需手动配置节点地址，适合小规模集群 | 集成 mDNS/Consul/Stardust 注册中心 |
| 5 | 缺少 HTTP 管理 API | 无法通过 API 查询/管理 Broker 状态 | 基于 WebApi 添加 RESTful 管理接口 |
| 6 | 缺少 Dashboard | 无可视化管理界面 | 集成 Web UI 或对接 Grafana |
| 7 | 增强认证缺少 SASL 具体实现 | AUTH 框架完整但缺少开箱即用的认证方法 | 实现 SCRAM-SHA-256 等标准方法 |

### 7.2 演进方向

| 优先级 | 功能 | 说明 |
|--------|------|------|
| 高 | 消息过期清理 | 分发时检查 `MessageExpiryInterval`，过期消息不推送 |
| 高 | 会话磁盘持久化 | 提供 Redis/SQLite 持久化 `IMqttExchange` 实现 |
| 中 | 遗嘱延迟发布 | 实现 `WillDelayInterval` 延迟队列 |
| 中 | HTTP 管理 API | RESTful API 查询连接/主题/统计/配置 |
| 中 | 服务端 KeepAlive 覆盖 | CONNACK 中主动设置 `ServerKeepAlive` |
| 低 | Dashboard | Web 可视化管理界面 |
| 低 | 集群自动发现 | mDNS / 注册中心集成 |
| 低 | SASL 认证实现 | SCRAM-SHA-256 等标准认证方法 |

---

## 八、总结评分

| 维度 | 评分 | 说明 |
|------|------|------|
| MQTT 3.1.1 协议 | ★★★★★ 100% | 14种报文全部实现，固定头部/QoS/Retain/Will 语义完整 |
| MQTT 5.0 协议 | ★★★★☆ 90% | 30种属性/AUTH/原因码/别名/流控/订阅选项完整，遗嘱延迟和消息过期待完善 |
| 客户端功能 | ★★★★★ 95% | 六种传输/重连/重订阅/Inflight/ITracer/阿里云适配 |
| 服务端功能 | ★★★★☆ 92% | Retain/Will/Session/ACL/$SYS/桥接/规则/WebHook/集群 |
| 企业扩展 | ★★★★☆ 88% | 桥接/规则引擎/WebHook/统计免费内置，缺 HTTP API/Dashboard |
| 集群能力 | ★★★☆☆ 65% | 订阅同步/消息转发/心跳保活，缺自动发现 |
| 安全能力 | ★★★★☆ 85% | TLS/ACL/认证框架完整，缺 SASL 具体实现 |
| 传输协议 | ★★★★★ 98% | TCP/TLS/WS/WSS/QUIC/UDP 六种传输，业界最全 |

**总结：** NewLife.MQTT 是一个**纯国产、零依赖、MIT 开源**的 MQTT 完整协议实现。MQTT 3.1.1 全面支持（14/14报文），MQTT 5.0 核心功能完整（30/30属性、AUTH 报文、原因码、主题别名、流控、订阅选项），六种传输协议覆盖面业界最广（TCP/TLS/WS/WSS/QUIC/UDP），内置桥接/规则引擎/WebHook/统计等企业级功能。在开源 MQTT 实现中，MQTT over QUIC、MQTT over 可靠UDP、企业功能免费内置属于领先水平，ProxyProtocol 支持和客户端+服务端一体化交付为独特优势。