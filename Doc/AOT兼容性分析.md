# NewLife.MQTT AOT 兼容性评估

> 基线：NewLife.Core 12.0.2026.1009-beta0916（v12 已 AOT 就绪）｜评估方式：**NativeAOT 真实编译 + 运行实测**（win-x64，.NET 10）｜冒烟工程：临时工程（不入库，复现方法见 §5）

---

## 1. 现状摘要

- **主链路 AOT 编译/运行双通**：10 场景冒烟中 9 通过（TCP 回环、WebSocket 桥接、HMAC 签名、JSON、配置绑定、看板 HttpListener、控制器源生成分发、动态依赖保根），**0 条来自 NewLife.MQTT / NewLife.Core 的 ILC 告警**。
- **唯一阻断点**：管理 API 与集群 RPC 的控制器分发。`MqttManagementController` / `ClusterController` 经手工 `new ControllerHandler { ControllerType = typeof(...) }` 注册，AOT 下控制器公开方法元数据被裁剪，请求返回 404（`Cannot find operation [Stats] within controller [...]`）。
- **修复路径两条，均已实测验证**：
  1. `HttpServer.MapController<T>()` 源生成静态分发（推荐：零反射，分发由 Core 源生成器生成 `HttpDispatchTable`；需把令牌校验/响应信封改 `HttpServer.Use` 中间件）；
  2. `[DynamicDependency(PublicConstructors | PublicMethods, typeof(X))]` 显式保根（最小改动：每个控制器 1 处注解）。
- **边界**：`NewLife.MQTT.Storage`（XCode/Redis）与 `NewLife.MqttServer` 应用（Stardust）不在本轮范围（未验证，取决于各自依赖链的 AOT 就绪度）。
- **结论**：AOT 支持属于**小改造**（1 个阻断点 ×2 处注册 + 1 个 CI 作业）；是否纳入正式支持由排期决策。

## 2. 实测数据

### 2.1 运行期：10 场景冒烟（JIT/AOT 双跑）

| # | 场景 | JIT | AOT | 说明 |
|---|------|:---:|:---:|------|
| 1 | TCP 回环：连接/订阅/发布/接收 | ✅ | ✅ | 主收发链路（消息泵编解码） |
| 2 | WebSocket 桥接：连接/订阅/发布/接收 | ✅ | ✅ | HttpServer + MqttWebSocketHandler |
| 3 | 管理 API：`GET /Api/Stats` | ✅ | ❌ 404 `Cannot find operation [Stats]` | **唯一阻断点**：反射分发未保根 |
| 4 | MqttSign：HMAC 签名计算 | ✅ | ✅ | `Assembly.GetEntryAssembly` 为空时回退 `GetExecutingAssembly` |
| 5 | JSON：`ToJson`/`ToJsonEntity` 往返 | ✅ | ✅ | 走 Core JsonHelper（源生成成员访问） |
| 6 | 配置：`Config<T>` 绑定 | ✅ | ✅ | 走 Core ConfigModelGenerator |
| 7 | 看板：HttpListener 提供 HTML | ✅ | ✅ | `System.Net.HttpListener` 可用 |
| 8 | 控制器分发：`MapController<T>` 源生成静态分发 | ✅ | ✅ | **修复路径 1 验证** |
| 9 | 控制器分发：手工注册 + `[DynamicDependency]` 保根 | ✅ | ✅ | **修复路径 2 验证** |
| 10 | 对照：手工注册、无保根 | ✅ 可用 | ❌ 404 | 因果闭合：无保根即被裁剪 |

### 2.2 编译期：ILC 告警

- `dotnet publish -c Release -r win-x64`：**0 条来自 NewLife.MQTT / NewLife.Core 的告警**；仅冒烟工程自身诊断行 `Assembly.Location` 触发 IL3000（评估证据，正式工程不会出现）。
- 产物：单文件 exe 约 11.0 MB（含 MQTT 全功能）。
- AOT 判真：产物运行 `IsDynamicCodeSupported=False`、`Assembly.Location` 为空。
- 严格姿态：`JsonSerializerIsReflectionEnabledByDefault=false`，配置类与 JSON 场景仍全通过。

## 3. 支持所需工作（清单）

| # | 工作项 | 位置 | 做法 | 验收 |
|---|--------|------|------|------|
| 1 | 管理 API 控制器保根 | `Handlers/MqttManagementServer.cs`（`Start`） | 首选：令牌校验/响应信封改 `HttpServer.Use` 中间件 + `MapController<MqttManagementController>(ApiPath)`；次选：`[DynamicDependency]` 注解 | 冒烟场景 3 转 ✅ |
| 2 | 集群 RPC 控制器保根 | `Clusters/ClusterServer.cs`（`Start`） | 同 #1（`ClusterController`，密钥校验改中间件或加注解） | 新增冒烟场景 |
| 3 | AOT 冒烟工程入库 + CI | `AotSmoke/`、`.github/workflows/aot.yml` | 参考 NewLife.Core 的 AotSmoke：`PublishAot` 写在工程属性内（勿走 CLI 全局属性，避免波及 net45 等多目标依赖），win-x64 发布 + 运行按退出码判定 | CI 防回归 |
| 4 | 多目标共存口径 | 无改动 | `PublishAot` 只作用于应用工程，库的 net45~net10 多目标不受影响 | — |

> 说明：`MapController<T>` 的静态分发由 Core 源生成器 `HttpControllerDispatchGenerator` 生成；生成器随 Core 包分发、对下游零配置（冒烟场景 6/8 已在 AOT 下证实生成器在消费方生效）。

## 4. 边界与不在范围

| 项 | 现状 | 理由 |
|---|---|---|
| `NewLife.MQTT.Storage`（XCode/Redis） | 未验证 | 取决于 XCode/Redis 自身 AOT 就绪度 |
| `NewLife.MqttServer` 应用（Stardust + DI） | 未验证 | Stardust 运行时基于 Core 11 编译，AOT 就绪度未知 |
| `Test` / `Benchmark` / `XUnitTest` | 不参与 | 非发布产物 |
| net45~netstandard2.x 目标框架 | 与 AOT 无关 | AOT 只作用于应用工程 |

## 5. 复现方法

```powershell
# 1) 临时冒烟工程（不入库）：net10.0，ProjectReference 到 NewLife.MQTT，工程属性：
#    <PublishAot>true</PublishAot>
#    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
# 2) JIT 基线
dotnet run -c Release
# 3) NativeAOT 真编译 + 运行（期望 0 条库侧 ILC 告警；场景 3 当前失败为已知阻断）
dotnet publish -c Release -r win-x64 -o out
.\out\AotSmoke.exe
```

冒烟场景清单见 §2.1（TCP/WS 收发、管理 API、签名、JSON、配置、看板、控制器分发 ×3）。

## 6. 收口记录（只增不删）

| 项 | 位置 | 处置与理由 |
|---|---|---|
| 动态能力（脚本/插件/表达式编译） | — | 设计性不支持，与 NewLife.Core 口径一致 |
| `MqttSign` 取入口程序集信息 | `MqttSign.cs` | AOT 下 `GetEntryAssembly` 为空时自动回退 `GetExecutingAssembly`，实测签名可用，不改造 |
| Dashboard HttpListener | `Handlers/MqttDashboard.cs` | 实测 AOT 可用，不改造 |
| HttpClient/HttpListener 等 BCL 路径 | `Handlers/MqttDashboard.cs` | 实测通过，不改造 |

---

（完）
