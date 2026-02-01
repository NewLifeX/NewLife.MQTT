# 新生命MQTT协议 - 版本变更历史

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
