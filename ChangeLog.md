# 新生命MQTT协议 - 版本变更历史

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
