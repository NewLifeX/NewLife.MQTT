# NewLife.MQTT AOT 兼容性评估

> 基线：NewLife.Core 12.0.2026.1013-beta0732（2026-10-15 升级复验）｜评估方式：**NativeAOT 真实编译 + 运行实测**（win-x64，.NET 10）｜冒烟工程：`AotSmoke/`（已入库，复现方法见 §5）

---

## 1. 现状摘要

- **主链路 AOT 编译/运行双通**：14 场景冒烟全部通过（TCP 回环、WebSocket 桥接、HMAC 签名、JSON、配置绑定、看板 HttpListener、控制器源生成分发、动态依赖保根、管理 API、集群 RPC、集群发布、大载荷跨轮帧体、断线重连+自动重订阅），**0 条来自 NewLife.MQTT / NewLife.Core 的 ILC 告警**。
- **已实施修复（2026-10-09）**：
  1. 控制器元数据保根：`MqttManagementServer.Start` / `ClusterServer.Start` 标注 `[DynamicDependency(PublicConstructors | PublicMethods, typeof(控制器))]`（低版本 TFM 由 Core 公共垫片提供该特性，无需条件编译）；
  2. 匿名对象载荷改字典承载：AOT 下匿名类型属性被裁剪，经运行时序列化会得到空对象 `{}`——`MqttManagementController.Stats`、`ClusterController.Health`、`ClusterNode` 六处 RPC 载荷、`AliyunMqttClient` 两处载荷均改为 `Dictionary<String, Object?>`（键名与线格式不变，JIT 行为不变）；
  3. 对照实验确认因果：无保根的手工注册在 AOT 下 404（方法元数据被裁剪），保根后恢复；
  4. 集群 RPC 载荷 DTO 保根与订阅数组归一化：`ClusterServer.Start` 对控制器签名涉及的载荷 DTO（NodeInfo/SubscriptionInfo/PublishInfo/PublishMessage/MqttProperties/SessionInfo/Raft 系列/SnapshotSubscription）统一标注 `[DynamicDependency(PublicConstructors | PublicProperties)]`（AOT 下运行时参数绑定需反射创建/读写，修复前 `Publish` 等路径报 `No parameterless constructor defined`）；`Subscribe`/`Unsubscribe` 控制器的数组参数经 `ToSubscriptionInfos` 归一化（FIX-CLUS-1：JSON 数组经参数绑定为对象列表后无法直接强转，JIT/AOT 同错）。
- **边界**：`NewLife.MQTT.Storage`（XCode/Redis）与 `NewLife.MqttServer` 应用（Stardust）不在本轮范围（未验证，取决于各自依赖链的 AOT 就绪度）。
- **FIX-CLUS-1 已修复（2026-10-09）**：订阅/退订 RPC 数组参数绑定失败为 v12 迁移引入的既有缺陷，已随本次改造修复并补 RPC 级回归测试（`ClusterRpcSubscribeTests`）。
- **深挖中发现并修复的两个运行时缺陷（2026-10-10）**：冒烟场景 13/14 与端到端大载荷探针驱动发现——①**链式帧体解码静默截断**：v12 消息泵零拷贝切帧时，跨多次管道追加的帧体为多段共享切片组成的链包（`Next != null`），Core `SpanReader` 对链包退化为 8192 字节流式缓冲，`Available` 只反映已缓冲分块，`ReadPacket(Available)` 会截断负载（载荷超过单轮读取上限 8KB 且跨轮追加时即触发，20KB 载荷确定性复现）；修复为 `MqttMessage.EnsureContiguous`（链包→`new ArrayPacket(body.ToArray())`），在 `ReadBody` 与 `MqttFactory.ReadMessage` 入口归一化。②**管理 API 断开客户端不真正断连**：`MqttExchange.DisconnectClient` 原仅调 `handler.Close(...)`（关闭通知语义），交换机登记已清但 TCP 连接仍在，客户端收不到消息又不触发重连；修复为经 `(handler as MqttHandler)?.Session` 走标准会话关闭（含遗嘱与注销）。两处均补确定性回归测试（`FrameBodyChainTests` / `ExchangeDisconnectTests`），修复前红灯、修复后全绿。

## 2. 实测数据

### 2.1 运行期：14 场景冒烟（JIT/AOT 双跑）

| # | 场景 | JIT | AOT | 说明 |
|---|------|:---:|:---:|------|
| 1 | TCP 回环：连接/订阅/发布/接收 | ✅ | ✅ | 主收发链路（消息泵编解码） |
| 2 | WebSocket 桥接：连接/订阅/发布/接收 | ✅ | ✅ | HttpServer + MqttWebSocketHandler |
| 3 | 管理 API：`GET /Api/Stats` | ✅ | ✅ | 保根后可达；字典返回值键完整（修复前 404） |
| 4 | MqttSign：HMAC 签名计算 | ✅ | ✅ | `Assembly.GetEntryAssembly` 为空时回退 `GetExecutingAssembly` |
| 5 | JSON：`ToJson`/`ToJsonEntity` 往返 | ✅ | ✅ | 走 Core JsonHelper（源生成成员访问） |
| 6 | 配置：`Config<T>` 绑定 | ✅ | ✅ | 走 Core ConfigModelGenerator |
| 7 | 看板：HttpListener 提供 HTML | ✅ | ✅ | `System.Net.HttpListener` 可用 |
| 8 | 控制器分发：`MapController<T>` 源生成静态分发 | ✅ | ✅ | 源生成器路径可用 |
| 9 | 控制器分发：手工注册 + `[DynamicDependency]` 保根 | ✅ | ✅ | 本轮采用的最小保根路径 |
| 10 | 对照：手工注册、无保根 | ✅ 可用 | ❌ 404 | 因果闭合：无保根即被裁剪 |
| 11 | 集群 RPC：Echo/Join/Ping/Leave/GetSession/Subscribe/Unsubscribe/Health | ✅ | ✅ | 保根 + 数组归一化 + 字典载荷/返回值（NodeInfo/SessionInfo 往返） |
| 12 | 集群发布：PublishInfo/PublishMessage 载荷保真 | ✅ | ✅ | topic/qos/payload 完整往返（记录型交换机观测） |
| 13 | 大载荷跨轮帧体：20KB/200KB 逐字节完整往返 | ✅ | ✅ | 消息泵零拷贝切帧产生链式帧体（`Next != null`），验证 `EnsureContiguous` 归一化路径 |
| 14 | 断线重连 + 自动重订阅 | ✅ | ✅ | 经 `MqttExchange.DisconnectClient` 强制断开，客户端重连并恢复订阅（连接次数=2，重订阅送达） |

> 订阅/退订 RPC 的数组参数绑定曾为既有缺陷（FIX-CLUS-1），2026-10-09 已随本页方案修复并解除门禁。
> 场景 13/14 为 2026-10-10 新增（随上表两个运行时缺陷的修复一并加入冒烟网），其中场景 13 用于覆盖"大帧体跨多轮接收→链式包"这一 v12 特有路径。

### 2.2 编译期：ILC 告警

- `dotnet publish -c Release -r win-x64`：**0 条来自 NewLife.MQTT / NewLife.Core 的告警**；仅冒烟工程自身诊断行 `Assembly.Location` 触发 IL3000（评估证据，正式工程不会出现）。
- 升级复验：`12.0.2026.1013-beta0732` 发布运行同样 0 条库侧 ILC 告警、14 场景全通过（2026-10-15）；此前双基线（发布包 `12.0.2026.1009-beta0916` 与下一版 `12.0.2026.1010` 本地 HEAD，尚未发包）结论一致。
- 产物：单文件 exe 约 11.6 MB（含 MQTT 全功能）。
- AOT 判真：产物运行 `IsDynamicCodeSupported=False`、`Assembly.Location` 为空。
- 严格姿态：`JsonSerializerIsReflectionEnabledByDefault=false`，配置类与 JSON 场景仍全通过。

## 3. 支持所需工作（清单）

| # | 工作项 | 状态 | 说明 |
|---|--------|------|------|
| 1 | 管理 API 控制器保根 | ✅ 已完成 | `MqttManagementServer.Start` 标注 `[DynamicDependency]`；冒烟场景 3 转 ✅ |
| 2 | 集群 RPC 控制器保根 | ✅ 已完成 | `ClusterServer.Start` 标注 `[DynamicDependency]`；冒烟场景 11 通过 |
| 3 | 匿名对象载荷改字典承载 | ✅ 已完成 | 管理 Stats / 集群 Health / ClusterNode 六处 / Aliyun 两处（AOT 下匿名类型属性被裁剪 → 空对象） |
| 4 | 集群 RPC 载荷 DTO 保根 + 订阅数组归一化 | ✅ 已完成 | `ClusterServer.Start` 保根 14 类载荷 DTO；`Subscribe`/`Unsubscribe` 数组参数归一化（FIX-CLUS-1）；冒烟场景 11/12 通过 |
| 5 | AOT 冒烟工程入库 + CI | ✅ 已完成 | `AotSmoke/` 工程入库（`PublishAot` 写在工程属性内、严格姿态关反射序列化）；CI 作业 `aot-smoke`（windows-latest，win-x64 发布后运行按退出码判定，见 `.github/workflows/test.yml`）；复现方法见 §5 |

> 说明：`MapController<T>` 的静态分发由 Core 源生成器 `HttpControllerDispatchGenerator` 生成；生成器随 Core 包分发、对下游零配置（冒烟场景 6/8 已在 AOT 下证实生成器在消费方生效）。手工保根路径下控制器仍走反射分发，AOT 下功能等价、性能与 JIT 一致。

## 4. 边界与不在范围

| 项 | 现状 | 理由 |
|---|---|---|
| `NewLife.MQTT.Storage`（XCode/Redis） | 未验证 | 取决于 XCode/Redis 自身 AOT 就绪度 |
| `NewLife.MqttServer` 应用（Stardust + DI） | 未验证 | Stardust 运行时基于 Core 11 编译，AOT 就绪度未知 |
| `Test` / `Benchmark` / `XUnitTest` | 不参与 | 非发布产物 |
| net45~netstandard2.x 目标框架 | 与 AOT 无关 | AOT 只作用于应用工程 |

## 5. 复现方法

```powershell
# 冒烟工程：仓库内 AotSmoke/（net10.0，ProjectReference 到 NewLife.MQTT，工程属性：
#    <PublishAot>true</PublishAot>
#    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>）
# 1) JIT 基线
dotnet run -c Release --project AotSmoke
# 2) NativeAOT 真编译 + 运行（期望 0 条库侧 ILC 告警；正常输出为"全部场景通过"）
dotnet publish -c Release -r win-x64 -o BinTest/AotSmoke AotSmoke/AotSmoke.csproj
.\BinTest\AotSmoke\AotSmoke.exe
```

冒烟场景清单见 §2.1（TCP/WS 收发、管理 API、签名、JSON、配置、看板、控制器分发 ×3、集群 RPC/发布、大载荷跨轮帧体、断线重连+自动重订阅）。

## 6. 收口记录（只增不删）

| 项 | 位置 | 处置与理由 |
|---|---|---|
| 动态能力（脚本/插件/表达式编译） | — | 设计性不支持，与 NewLife.Core 口径一致 |
| `MqttSign` 取入口程序集信息 | `MqttSign.cs` | AOT 下 `GetEntryAssembly` 为空时自动回退 `GetExecutingAssembly`，实测签名可用，不改造 |
| Dashboard HttpListener | `Handlers/MqttDashboard.cs` | 实测 AOT 可用，不改造 |
| HttpClient/HttpListener 等 BCL 路径 | `Handlers/MqttDashboard.cs` | 实测通过，不改造 |

---

（完）
