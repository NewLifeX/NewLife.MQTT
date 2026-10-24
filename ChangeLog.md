# 新生命MQTT协议 - 版本变更历史

## v4.0.2026.1024 (2026-10-24)

### 破坏性变更（v4.0 升级须知）
- **依赖升级到 NewLife.Core 12.0.2026.1024**：全面适配 v12 网络栈（消息泵、消息/帧契约、`OwnerPacket` 引用计数所有权模型）。基于 Core v11 编译的调用方需随大版本重新编译；自定义协议处理器与直接操作 `NewLife.Data` 数据包的代码需按 Core 12 升级说明调整
- **WebSocket 独立监听**：改由独立 `HttpServer` + `MqttWebSocketHandler` 桥接，用 `EnableWebSocket` 开启；`WebSocketPort` 默认 0（系统分配）、`WebSocketPath` 默认 `/mqtt`，不再与 MQTT TCP 端口共用；`WebSocketCertificate` 开启 WSS
- **管理 API 迁移到 HttpServer**：地址保持 `/Api/{Action}`，但改由 HttpServer 控制器承载
- **集群 RPC 迁移到 HTTP**：`ClusterPort` 为 HTTP 端口，节点地址可写 `host:port` 或 `http://host:port`，`ClusterSecret` 作为每次请求的令牌校验；移除 `NewLife.Remoting` 依赖
- **框架目标说明**：继续提供 net45 / net461 / netstandard2.0 / netstandard2.1 / net6.0~net10.0；NewLife.Core 12 已不再提供 net7.0 资产，net7.0 目标回退使用 Core net6.0 资产

### 多语言本地化（31 种语言）
- **运行时消息全面本地化**：主库 220+ 条运行时消息以及存储组件、服务端示例词表全部改走 NewLife.Core 12 编译期词表，覆盖 31 种语言，缺失译文自动回退英文
- **程序集元数据与配置项翻译**：包标题/描述、服务端配置显示名与看板文案支持多语言，`Localization.Culture` 与 `Localization.LogCulture` 可分别控制界面与日志语言
- **11 种语言 README**：README 默认语言切换为英文，补齐简体中文、繁體中文、西班牙语、德语、日语、法语、葡萄牙语、俄语、韩语、阿拉伯语全量译文
- **本地化质量门禁**：新增词表键集合/译文完整性/源码消息/README 结构校验脚本并接入 CI

### 事件总线
- **MqttEventBus**：新增基于 MQTT 主题的强类型事件总线，对齐 NewLife.Core `IEventBusFactory` 契约，可在 MQTT / Redis / 进程内通道间切换
- **广播与共享订阅**：默认广播订阅，开启 `UseSharedSubscription` 后走 `$share` 共享订阅；信封携带发布方标识排除自身回环

### 性能优化
- **主题路由前缀索引**：以首层主题段前缀索引 + 通配符桶定位候选过滤器，订阅列表写时复制快照，发布读路径无锁零分配
- **零分配主题匹配**：`MqttTopicFilter.IsMatch` 改为 Span 逐段匹配，热路径前缀判断改用 Ordinal，字面量匹配由 637ns 降至 8.8ns
- **收发缓冲默认下调**：服务端会话缓冲与客户端接收缓冲默认 2048 字节，降低小消息高吞吐场景的大对象分配
- **实测吞吐**：Broker 收包约 125 万 msg/s，单连接端到端 QoS0 约 10.6 万 msg/s

### NativeAOT 与多平台
- **主链路 AOT 兼容**：TCP/WS 收发、管理 API、集群 RPC、JWT/OAuth 认证、保留消息、持久会话离线补发、MQTT-SN 网关等 19 个场景 JIT/AOT 双跑通过，库侧 0 条 ILC 告警
- **CI 平台矩阵**：单元测试覆盖 win-x64 / linux-x64 / linux-arm64 / 龙芯 LA64（QEMU 仿真）；AOT 冒烟覆盖 win-x64 / linux-x64 / linux-arm64；RISC-V 64、s390x 指令集与 Android / iOS 移动端冒烟纳入观察期
- **交叉兼容门禁**：NewLife ↔ MQTTnet 双向 32 个交叉用例（连接、QoS 0/1/2、通配符、共享订阅、遗嘱等）纳入每平台测试门禁
- **移动端冒烟**：新增 Android / iOS 冒烟工程，验证主库在移动运行时加载核心 API

### Bug 修复
- **[fix]** 修复 v12 消息泵链式帧体解码静默截断：帧体跨多次管道追加时按 `Available` 取负载会截断，20KB 载荷确定性复现，已在大载荷入口归一化为连续包
- **[fix]** 修复 QUIC 服务端会话未接入协议会话：连接后交换机、集群与认证注入失效，客户端 CONNACK 无法配对
- **[fix]** 修复管理 API 断开客户端未真正断连：只清理交换机登记，TCP 连接仍然在线
- **[fix]** 修复集群成员变更未同步分片路由环，分片转发可能指向已离线节点
- **[fix]** 修复集群订阅/退订 RPC 数组参数绑定失败，以及 AOT 下集群载荷反序列化失败
- **[fix]** 修复 Storage 离线消息查询重载误用，DbExchange 读取离线消息会抛出 SQL 语法错误

### 工程与测试
- **解决方案升级 slnx 格式**，单元测试并行化后全量耗时由约 94s 降至约 12s
- **新增 LoadBench 双进程压测工具**，支持 server / pub / sub / mix / pair / rawserver / rawclient 七种场景
- 新增 LA64 仿真流水线与 RISC-V / s390x 指令集冒烟工程，补齐多 RID 测试矩阵

---

## v3.2.2026.0802 (2026-08-02)

### 集群能力升级（v4.x）
- **Raft 共识引擎**：完整 Raft 实现（选举、投票、日志复制、快照），支撑集群强一致状态同步
- **主题分片与路由**：一致哈希主题分片 + 分片感知路由表，大规模主题路由水平扩展
- **集群路由加速**：布隆过滤器预检 + 批量合并转发，降低跨节点转发开销
- **多层拓扑集群**：分层集群 HierarchicalCluster、路由降级、连接聚合、订阅增量同步、分片快照
- **热点主题检测**：HotTopicDetector 自动识别热点主题，辅助负载均衡与限流
- **集群运维增强**：集群密钥 ClusterSecret 热更新、健康检查、降级自动升级、离线消息迁移、环路防护、集群指标
- **订阅同步优化**：广播订阅/退订改为顺序发送避免并发问题，订阅关系支持 clientId 与会话标识

### 消息持久化（新项目）
- **NewLife.MQTT.Storage**：新增独立存储库，Redis + Database 双实现，支持离线消息与会话持久化

### 协议扩展
- **MQTT-SN v1.2**：新增 MQTT-SN 协议支持（网关、编解码、主题注册），适配传感器等受限设备接入
- **WebHook 六种事件管线**：WebHook 事件集成至 MqttHandler + Exchange 管线，修复退订与投递事件缺失

### 安全与传输
- **OAuth 认证**：新增 OAuth 认证器，支持 JWT 令牌校验
- **SOCKS5 代理**：内置 SOCKS5 代理支持，TCP/WS 连接均可代理转发
- **安全增强**：限速、黑白名单、JWT 认证

### Bug 修复
- **[fix]** 修复 6 种 MQTT 5.0 消息类型的属性序列化缺陷
- **[fix]** 修复 SubAck / UnsubAck 空属性时缓冲区估算不足、必须写入长度字段的序列化问题
- **[fix]** Client_Error 空方法补充日志记录

### 测试与质量
- 新增 RaftCluster 单元测试（状态转换、投票逻辑、日志复制）
- 新增集群路由 5 类单元测试（23 个用例）
- 新增 OAuth 认证 + SOCKS5 代理单元测试（16 个用例）
- 新增热点主题检测单元测试（7 个用例）
- 修复测试稳定性问题，升级基础组件依赖

---

## v3.1.2026.0601 (2026-06-01)

### MQTT 5.0 高级特性（Phase-2）
- **服务端消息注入（InjectMessage）**：服务端可主动向指定客户端推送消息，支持 MQTT 5.0 属性透传
- **服务端分配 ClientId**：MQTT 5.0 CONNACK 携带 AssignedClientIdentifier，完成 Phase-2 服务端特性支持
- **ServerReference**：MQTT 5.0 服务端重定向属性支持（F045~F049 全覆盖）
- **InflightManager 放弃回调**：修正 Inflight 流控，支持放弃消息的回调通知

### MQTT 3.1 服务端兼容接入（F044）
- **MQIsdp 协议名识别**：服务端识别 MQTT 3.1 协议名 `MQIsdp`，允许旧版 3.1 设备无缝接入
- **ClientId 限制放开**：MQTT 3.1 设备 ClientId 不再强制 23 字节上限

### 协议兼容增强
- **V3/V3.1.1 自动清除 5.0 属性**：PublishAsync 根据协议版本自动清除 5.0 Properties，提升互操作兼容性
- **消息属性透传**：MqttExchange 支持消息属性透传，线内消息 Payload 深拷贝
- **PubAck 增强**：ReasonCode 与 Properties 完整读写支持，ToString 输出增强

### 跨框架互操作测试
- **MQTTnet 交叉集成 E2E 测试**：新增 NewLife MqttClient → MQTTnet 服务端、MQTTnet 客户端 → NewLife MqttServer 双向端到端测试，覆盖连接、QoS 0/1/2、通配符、重连、遗嘱等核心场景
- **NewLife 自测 E2E**：覆盖 QoS 1/2 端到端、多订阅者、手动重连、MQTT 5.0、CleanSession=false 等场景

### 编解码优化
- **MqttCodec 队列匹配**：新增 `Read` 重载，支持 IPacket 解包时 Reply 消息队列匹配；补充 PingRequest/PingResponse 队列匹配测试

### 测试工程重构
- **项目重命名**：XUnitTestClient → XUnitTest，更新解决方案与 CI 路径
- **目录模块化**：按功能领域整理测试文件（Protocols/Clients/Sessions/Routing/Transports/Auth/Stats/Handlers/Integration/Clusters）

---

## v3.0.2026.0501 (2026-05-01)

### 测试与质量
- **综合集成测试**：新增 `ComprehensiveMqttIntegrationTests`，覆盖连接、发布/订阅、QoS 0/1/2、遗嘱消息、保留消息、通配符订阅、并发等典型场景，共约 770 行
- **测试问题修复**：修正 `MqttIntegrationTests` 和 `MultiProtocolIntegrationTests` 中的已知测试缺陷，提升测试稳定性
- **集成测试文档**：新增 `Doc/集成测试说明.md`，详细记录各测试类用途、运行方式与注意事项

---

## v2.3.2026.0201 (2026-02-01)

### 依赖更新
* 更新依赖包到最新版本
  - 2026-01-24: 更新 NewLife.Core 和 NewLife.Remoting
  - 2026-01-14: 更新 NewLife.Core 和 NewLife.Remoting
  - 2026-01-12: 更新 NewLife.Core 和 NewLife.Remoting
  - 2026-01-03: 更新 NewLife.Core 和 NewLife.Remoting

---

## v2.3.2026.0102 (2026-01-02)

### 新功能
* **增加重连机制** (感谢 @猿人易 贡献)
  - 支持配置化的重连机制，包括最大重连次数、初始/最大重连间隔等属性
  - 实现指数退避策略，避免频繁重连对服务器造成压力
  - 引入异步锁 `_connectLock` 确保连接过程线程安全
  - 新增异步重连循环 `ReconnectLoopAsync`
  - 优化断开连接逻辑，改为异步重连循环
  - 改进 `Dispose` 方法，确保资源正确释放

### 框架支持
* 新增 .NET 10 目标框架支持

---

## v2.2.2025.1001 (2025-10-01)

### 优化改进
* 较大 Int64 转字符串，避免精度丢失（针对 Web 和嵌入式设备）
* 优化 MQTT 服务名称显示

---

## v2.1.2005.0601 (2025-06-01)

### 新功能
* **支持 MQTT over WebSocket**
  - 支持每个 WebSocket 包携带一个完整的 MQTT 帧
  - 在 webjs 上的 WebSocket 测试通过
  - 注：暂不支持一个 MQTT 帧拆散到多个 WebSocket 帧传输（服务端尚未支持粘包组包）
* MqttServer 注入 Http 编码器和 WebSocket 编码器
* 添加 WebSocket 服务器支持及示例项目
* 添加 WebSocket 配置属性到 MqttSetting 类

---

**说明**：
- 正式版本号格式：`{主版本}.{子版本}.{年}.{月日}`
- 本项目遵循 NewLife 团队开发规范
- 详细代码变更请参考 Git 提交历史
