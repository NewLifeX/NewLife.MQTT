# 新生命MQTT协议 - 版本变更历史

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
