# NewLife.MQTT AOT 兼容性评估

> 基线：NewLife.Core 12.0.2026.1013-beta0732（2026-10-15 升级复验）｜评估方式：**NativeAOT 真实编译 + 运行实测**（必选覆盖 win-x64 / linux-x64 / linux-arm64，.NET 10；linux-la64 全量单测门禁、AOT 冒烟与 win-arm64 / osx 双架构 / linux-musl 为观察期）｜冒烟工程：`AotSmoke/`（已入库，复现方法见 §5）

---

## 1. 现状摘要

- **RID 覆盖（2026-10-16 扩展）**：AOT 冒烟与单元测试均覆盖 **win-x64 / linux-x64 / linux-arm64 / linux-la64**；前三者 AOT + 单测阻断门禁，linux-la64 全量单测为阻断门禁、AOT 冒烟为观察期（continue-on-error，失败不阻断）；win-arm64 / osx-x64 / osx-arm64 / linux-musl 观察期（continue-on-error，稳定后转阻断）；边缘 RID（win-x86 / linux-arm32）见 `edge.yml`（仅手动触发）；矩阵与约定见 §6。
- **主链路 AOT 编译/运行双通**：19 场景冒烟全部通过（TCP 回环、WebSocket 桥接、HMAC 签名、JSON、配置绑定、看板 HttpListener、控制器源生成分发、动态依赖保根、管理 API、集群 RPC、集群发布、大载荷跨轮帧体、断线重连+自动重订阅、JWT/OAuth 认证、保留消息、持久会话离线补发、MQTT-SN 网关），**0 条来自 NewLife.MQTT / NewLife.Core 的 ILC 告警**。
- **已实施修复（2026-10-09）**：
  1. 控制器元数据保根：`MqttManagementServer.Start` / `ClusterServer.Start` 标注 `[DynamicDependency(PublicConstructors | PublicMethods, typeof(控制器))]`（低版本 TFM 由 Core 公共垫片提供该特性，无需条件编译）；
  2. 匿名对象载荷改字典承载：AOT 下匿名类型属性被裁剪，经运行时序列化会得到空对象 `{}`——`MqttManagementController.Stats`、`ClusterController.Health`、`ClusterNode` 六处 RPC 载荷、`AliyunMqttClient` 两处载荷均改为 `Dictionary<String, Object?>`（键名与线格式不变，JIT 行为不变）；
  3. 对照实验确认因果：无保根的手工注册在 AOT 下 404（方法元数据被裁剪），保根后恢复；
  4. 集群 RPC 载荷 DTO 保根与订阅数组归一化：`ClusterServer.Start` 对控制器签名涉及的载荷 DTO（NodeInfo/SubscriptionInfo/PublishInfo/PublishMessage/MqttProperties/SessionInfo/Raft 系列/SnapshotSubscription）统一标注 `[DynamicDependency(PublicConstructors | PublicProperties)]`（AOT 下运行时参数绑定需反射创建/读写，修复前 `Publish` 等路径报 `No parameterless constructor defined`）；`Subscribe`/`Unsubscribe` 控制器的数组参数经 `ToSubscriptionInfos` 归一化（FIX-CLUS-1：JSON 数组经参数绑定为对象列表后无法直接强转，JIT/AOT 同错）。
- **边界（2026-10-15 更新）**：`NewLife.MQTT.Storage`（XCode 数据库路径与 Redis 路径）与 `NewLife.MqttServer` 应用（Stardust）仍不在支持范围；其中 Storage 已完成就绪度实测——XCode 包（12.2.2026.901 / 912-beta）在 AOT 下产生 7 条 ILC 告警且运行期数据库命令创建失败，`RedisExchange` 仅构造级可用，详见 §4。
- **FIX-CLUS-1 已修复（2026-10-09）**：订阅/退订 RPC 数组参数绑定失败为 v12 迁移引入的既有缺陷，已随本次改造修复并补 RPC 级回归测试（`ClusterRpcSubscribeTests`）。
- **深挖中发现并修复的两个运行时缺陷（2026-10-10）**：冒烟场景 13/14 与端到端大载荷探针驱动发现——①**链式帧体解码静默截断**：v12 消息泵零拷贝切帧时，跨多次管道追加的帧体为多段共享切片组成的链包（`Next != null`），Core `SpanReader` 对链包退化为 8192 字节流式缓冲，`Available` 只反映已缓冲分块，`ReadPacket(Available)` 会截断负载（载荷超过单轮读取上限 8KB 且跨轮追加时即触发，20KB 载荷确定性复现）；修复为 `MqttMessage.EnsureContiguous`（链包→`new ArrayPacket(body.ToArray())`），在 `ReadBody` 与 `MqttFactory.ReadMessage` 入口归一化。②**管理 API 断开客户端不真正断连**：`MqttExchange.DisconnectClient` 原仅调 `handler.Close(...)`（关闭通知语义），交换机登记已清但 TCP 连接仍在，客户端收不到消息又不触发重连；修复为经 `(handler as MqttHandler)?.Session` 走标准会话关闭（含遗嘱与注销）。两处均补确定性回归测试（`FrameBodyChainTests` / `ExchangeDisconnectTests`），修复前红灯、修复后全绿。

## 2. 实测数据

### 2.1 运行期：19 场景冒烟（JIT/AOT 双跑）

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
| 15 | JWT 认证：HS256 令牌连接并收发、篡改令牌被拒 | ✅ | ✅ | `MqttJwtAuthenticator`：Core JsonParser 解析 Claims + 主题级授权（pub/mqtt_sub） |
| 16 | OAuth 认证：HS256 令牌连接并收发、错误密钥被拒 | ✅ | ✅ | `MqttOAuthAuthenticator`（HS256 共享密钥路径，未启用 JwksEndpoint/RS256） |
| 17 | 保留消息：订阅即得、清除后不再下发 | ✅ | ✅ | 保留消息存取与订阅时推送（RetainHandling=0） |
| 18 | 持久会话离线补发：SessionPresent + QoS1 补投 | ✅ | ✅ | CleanSession=false 会话保存/恢复 + 离线队列补发（内存交换机） |
| 19 | MQTT-SN 网关：UDP 注册/发布 → TCP 订阅者收到 | ✅ | ✅ | `MqttSnGateway` + `MqttSnClient`（UDP 编解码 + 网关桥接至 broker） |

> 订阅/退订 RPC 的数组参数绑定曾为既有缺陷（FIX-CLUS-1），2026-10-09 已随本页方案修复并解除门禁。
> 场景 13/14 为 2026-10-10 新增（随上表两个运行时缺陷的修复一并加入冒烟网），其中场景 13 用于覆盖"大帧体跨多轮接收→链式包"这一 v12 特有路径。
> 场景 15~19 为 2026-10-15 新增（JWT/OAuth 认证、保留消息、持久会话离线补发、MQTT-SN 网关；JWT/OAuth 令牌为手工 HS256 构造）。

### 2.2 编译期：ILC 告警

- `dotnet publish -c Release -r win-x64`：**0 条来自 NewLife.MQTT / NewLife.Core 的告警**；仅冒烟工程自身诊断行 `Assembly.Location` 触发 IL3000（评估证据，正式工程不会出现）。
- **分析器常驻（2026-10-16）**：库工程 net8.0+ 启用 `IsAotCompatible`（AOT/裁剪分析器随日常构建运行），本地实测 net8.0/net9.0/net10.0 构建 **0 条 IL 告警**；CI `aot-analyzer` 作业对三个 TFM 的构建日志做 IL 告警门禁（既有 CS/CA 告警基线不在本任务范围，故未采用全局 `-warnaserror`）。
- 升级复验：`12.0.2026.1013-beta0732` 发布运行同样 0 条库侧 ILC 告警、19 场景全通过（2026-10-15，扩展场景后复跑）；此前双基线（发布包 `12.0.2026.1009-beta0916` 与下一版 `12.0.2026.1010` 本地 HEAD，尚未发包）结论一致。
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
| 5 | AOT 冒烟工程入库 + CI 多 RID 矩阵 | ✅ 已完成 | `AotSmoke/` 工程入库（`PublishAot` 写在工程属性内、严格姿态关反射序列化）；CI 拆分为独立 `.github/workflows/aot.yml`：分析器门禁 + 必选 RID（win-x64/linux-x64/linux-arm64）阻断 + 观察期 RID（linux-la64 冒烟/win-arm64/osx 双架构/musl）；边缘 RID 见 `.github/workflows/edge.yml`（仅手动触发）；矩阵见 §6，复现方法见 §5 |
| 6 | 编译期分析器门禁（IsAotCompatible） | ✅ 已完成 | 库工程 net8.0/9.0/10.0 常驻启用 `IsAotCompatible`（IsTrimmable 元数据随包分发，与 Core 口径一致）；CI `aot-analyzer` 作业对三个 TFM 校验 0 条 IL 告警 |

> 说明：`MapController<T>` 的静态分发由 Core 源生成器 `HttpControllerDispatchGenerator` 生成；生成器随 Core 包分发、对下游零配置（冒烟场景 6/8 已在 AOT 下证实生成器在消费方生效）。手工保根路径下控制器仍走反射分发，AOT 下功能等价、性能与 JIT 一致。

## 4. 边界与不在范围

| 项 | 现状 | 理由 |
|---|---|---|
| `NewLife.MQTT.Storage`（XCode/Redis） | 未就绪（2026-10-15 实测） | 数据库路径：XCode 包（12.2.2026.901 stable / 912-beta，已复刻 XCode AotSmoke 裁剪根配方）在 win-x64 NativeAOT 下产生 7 条 ILC 告警（均出自 XCode.dll：IL2104/IL3053/IL3000/IL3002）且运行期 `DbSession.OnCreateCommand` 抛 `InvalidOperationException`；Redis 路径仅 `RedisExchange` 构造级可用（真实读写未验证）。随本轮修复 `MqttOfflineMessageEntity.FindAllByClientId` 的单参 `FindAll(String)`（整条 SQL 语义）重载误用后，DbExchange 在 JIT 下会话与离线消息往返已通过 |
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

各 RID 使用原生 runner 发布运行（NativeAOT 官方不支持跨 OS 编译，**不能**在 Windows 上交叉发布 Linux 产物）：

```bash
# linux-x64（ubuntu-latest 原生）
sudo apt-get install -y clang zlib1g-dev
dotnet publish AotSmoke/AotSmoke.csproj -c Release -r linux-x64 -o AotSmoke/bin/aot-out
./AotSmoke/bin/aot-out/AotSmoke
# linux-arm64（ubuntu-24.04-arm 原生）、osx-arm64（macos-latest）、osx-x64（macos-15-intel）、
# win-arm64（windows-11-arm）同理，仅 -r 与产物名不同
# linux-musl-x64/arm64：alpine:3.20 容器内 dotnet-install.sh + clang/build-base/zlib-dev/icu-libs
```

LA64（linux-loongarch64）无官方支持，走 QEMU 用户态仿真 + 龙芯 SDK（CI 封装为复合动作 `.github/actions/setup-la64`）。
两个硬约束：**龙芯运行时是解释器模式**（`IsDynamicCodeSupported=False`），仿真下 MSBuild/csc/vstest 均不可用（实测崩溃；
官方 vstest 也不认 loongarch64），因此 LA64 一律「**x64 编译 IL → QEMU 下用 LA64 运行时执行**」；
且需要一个 loong64 sysroot 供 ELF 解释器使用：

```bash
# 1) QEMU binfmt
sudo apt-get install -y qemu-user-static binfmt-support && sudo update-binfmts --enable qemu-loongarch64

# 2) loong64 sysroot：取 Debian sid 的 loong64 原生包（glibc 2.43）
#    Ubuntu 自带的 libc6-loong64-cross 2.39 太旧，libcoreclr 会因无法识别的重定位（0x0e）加载失败
#    包清单：libc6 / libgcc-s1 / libstdc++6 / libssl3t64 / zlib1g / libzstd1 / liblzma5 / libcrypt1 / libicu76
#    解包后用软链补齐 merged-usr 的 /lib、/lib64（Debian 里由 base-files 提供，sysroot 需自建）
ln -sfn usr/lib ~/la64root/lib && ln -sfn usr/lib64 ~/la64root/lib64
export QEMU_LD_PREFIX=~/la64root                        # 作为 ELF 解释器前缀
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1          # 宿主无 loong64 原生 ICU

# 3) 龙芯 .NET SDK
curl -fL -o ~/la64-sdk.tar.gz https://github.com/loongson/dotnet/releases/download/v10.0.112-loongarch64/dotnet-sdk-10.0.112-linux-loongarch64.tar.gz
mkdir -p ~/la64 && tar -xzf ~/la64-sdk.tar.gz -C ~/la64
export PATH="$HOME/la64:$PATH" DOTNET_ROOT="$HOME/la64"
dotnet --info                                           # 期望 RID: linux-loongarch64（QEMU 通路第一道验证）

# 4) 冒烟：x64 编译 IL，LA64 执行
dotnet build -c Release AotSmoke/AotSmoke.csproj
dotnet AotSmoke/bin/Release/net10.0/AotSmoke.dll        # 同一套 19 场景冒烟

# 5) 全量单元测试：同样 x64 编译、LA64 执行（vstest 不支持 loongarch64，改用 xunit 独立控制台运行器）
dotnet build -c Release XUnitTest/XUnitTest.csproj
curl -fsSL -o /tmp/xrc.nupkg https://www.nuget.org/api/v2/package/xunit.runner.console/2.9.3
unzip -q -o /tmp/xrc.nupkg 'tools/net6.0/*' -d /tmp/xrc
DOTNET_ROLL_FORWARD=LatestMajor dotnet /tmp/xrc/tools/net6.0/xunit.console.dll "$PWD/Bin/UnitTest/XUnitTest.dll" -nocolor
```

冒烟场景清单见 §2.1（TCP/WS 收发、管理 API、签名、JSON、配置、看板、控制器分发 ×3、集群 RPC/发布、大载荷跨轮帧体、断线重连+自动重订阅）。

## 6. 多 RID 支持矩阵与 CI

> 目标：**必选覆盖 win-x64 / linux-x64 / linux-arm64（AOT 冒烟 + 单测阻断门禁）、linux-la64（全量单测阻断门禁，AOT 冒烟为观察期）**；扩展 win-arm64、osx 双架构、linux-musl 双架构（观察期）；边缘 RID（win-x86、linux-arm32）尽力而为。
> 前提：**NativeAOT 官方不支持跨 OS 编译**（Windows→Linux 需 WSL/VM），每个 RID 均使用原生 runner 发布运行。

### 6.1 覆盖分层

| 层级 | 目标 RID | 单测 | AOT 冒烟 | runner / 载体 | 状态 |
|------|----------|:----:|:--------:|---------------|------|
| T1 必选 | win-x64 | ✔ | ✔ 阻断 | windows-latest | 门禁 |
| T1 必选 | linux-x64 | ✔ | ✔ 阻断 | ubuntu-latest | 门禁 |
| T1 必选 | linux-arm64 | ✔ | ✔ 阻断 | ubuntu-24.04-arm | 门禁 |
| T1 必选 | linux-la64（龙芯） | ✔（全量） | ✔（JIT 冒烟） | ubuntu-latest（x64 编译 IL）+ QEMU + 龙芯 SDK v10.0.112 | 单测门禁 / AOT 观察期 |
| T1 扩展 | win-arm64 | — | ✔ | windows-11-arm（自带 ARM64 MSVC 工具链） | 观察期 |
| T1 扩展 | linux-musl-x64 / arm64 | — | ✔ | ubuntu(-arm) + alpine:3.20 容器 | 观察期 |
| T1 扩展 | osx-arm64 / osx-x64 | — | ✔ | macos-latest / macos-15-intel（官方称可用至 2027-08） | 观察期 |
| T2 边缘 | win-x86 | — | ✔ | windows-latest 交叉发布 | 观察期（edge.yml，手动触发） |
| T2 边缘 | linux-arm（32 位） | — | 探索 | QEMU 容器 JIT 冒烟 + 交叉发布尝试 | 观察期（edge.yml，手动触发） |

- **CI 拆分**：`.github/workflows/aot.yml`（分析器门禁 + NativeAOT 矩阵 + LA64 + musl）、`.github/workflows/test.yml`（单元测试矩阵 + LA64 全量单测）、`.github/workflows/edge.yml`（边缘 RID，仅手动触发）；
- **触发约定**：test/aot 在 push / PR 与手动触发时运行，**不采用夜间调度**（多代码库服务端同步到 GitHub 的节奏不固定）；
- **观察期约定**：新增平台 job 均带 `continue-on-error`，连续稳定全绿后逐项去掉该标记转阻断；必选 RID 自接入起即阻断；
- **LA64（linux-loongarch64）**：官方不发 runtime pack、无 NativeAOT 支持（ILCompiler 无 linux-loongarch64 产物），故以 **JIT 冒烟 + 全量单元测试** 覆盖该平台行为；其中 `aot.yml` 的 JIT 冒烟为观察期（continue-on-error，失败不阻断），`test.yml` 的全量单测为阻断门禁；受解释器模式限制，仿真下只运行 IL 产物（x64 编译），不在仿真内跑 MSBuild/csc/vstest；有龙芯真机后可接 self-hosted runner 升级为原生执行；
- **观察期结果回填**：各观察期 RID 的首次运行结果由 CI 结论回填本表（不预填）。

### 6.2 LA64 仿真覆盖范围与限制

龙芯 .NET 10 运行时是**解释器实现**（实测 `IsDynamicCodeSupported=False`），仿真下有三类硬限制：

| 限制 | 证据（本机 WSL + QEMU 实测） | 应对 |
|------|------------------------------|------|
| 仿真内不能跑 MSBuild/csc | `dotnet build` 报 `MSB4018 System.NullReferenceException at InvokeStub_MSBuild.set_Properties`；Roslyn 编译触发 FailFast | 一律在 x64 上编译 IL，LA64 只执行产物 |
| vstest 不支持 loongarch64 | `ProcessHelper.GetCurrentProcessArchitecture()` 抛 `NotSupportedException` | 单测改用 xunit 独立控制台运行器（`xunit.runner.console` net6.0 纯 IL + `DOTNET_ROLL_FORWARD=LatestMajor`） |
| 部分测试类触发运行时内部缺陷 | 段错误 / `AccessViolationException` / `ExecutionEngineException: Illegal instruction`；栈顶多见反射调用桩（`InvokeStub_...`、`ReflectionAttributeInfo.GetNamedArgument`），与库代码无关，且同一命令存在偶发失败 | 按"已实测稳定"的粒度分批执行（每批一个独立进程），每批最多重试 3 次兜底偶发崩溃，连续 3 次失败才判失败；未纳入的类待运行时修复或接入真机 runner 后回收 |

**QEMU 用户态已知缺陷（.NET 代码页双映射触发）**：`QEMU internal SIGSEGV {code=MAPERR, addr=0x20}` 可在任意测试/冒烟进程出现，基线崩溃率随测试内容而异（本机实测 `XUnitTest.Transports.ProxyProtocolTests` 约 50%、`AotSmoke` 约 33%），与库代码无关；仿真环境统一设置 `DOTNET_EnableWriteXorExecute=0` 关闭 .NET 代码页双映射后，实测 `ProxyProtocolTests` 连续 30 次、`AotSmoke` 连续 3 次、全量批次一轮 646 用例零失败（残余偶发仍由每批 3 次重试兜底），设置位置见 `.github/actions/setup-la64`。

**LA64 当前覆盖（全量单测阻断，AOT 冒烟观察期）**：38 个批次共 646 个测试用例 + `AotSmoke` 全场景冒烟，覆盖协议编解码、传输层（TCP/WS/UDP/QUIC 会话与客户端）、会话与补发、路由与规则引擎、集群（模型/服务器/连接聚合/分层/分片快照/降级）、集成与端到端场景；分片路由 E2E 与 QUIC E2E 仍在 x64/arm64 runner 原生执行。

**暂未纳入 LA64 的测试类**（其余平台正常执行）：

| 测试类 | 现象 |
|--------|------|
| `XUnitTest.Auth.MqttSignTests` | 2 个用例断言入口程序集名为 `NewLife.MQTT`，独立控制台运行器下为 `xunit.console`（运行器差异，非平台问题） |
| `XUnitTest.Clients.MqttClientTests`、`XUnitTest.Clusters.ClusterRpcSubscribeTests`、`ClusterSecretTests`、`MqttClusterTests`、`ShardRoutingE2ETests`、`XUnitTest.Integration.DeviceSimulationTests` | 段错误（RC=139） |
| `XUnitTest.Clusters.RaftClusterTests` | 偶发段错误（同一命令时而通过） |
| `XUnitTest.Integration.LargePayloadTests` | 用例全部通过后 FailFast |
| `XUnitTest.Transports.TransportIntegrationTests`、`WsFrameBoundaryTests`、`WsSessionLifecycleTests`、`WssIntegrationTests` | 未纳入：聚合运行中在反射构造路径崩溃（`InvokeStub_TransportIntegrationTests..ctor`） |

## 7. 收口记录（只增不删）

| 项 | 位置 | 处置与理由 |
|---|---|---|
| 动态能力（脚本/插件/表达式编译） | — | 设计性不支持，与 NewLife.Core 口径一致 |
| `MqttSign` 取入口程序集信息 | `MqttSign.cs` | AOT 下 `GetEntryAssembly` 为空时自动回退 `GetExecutingAssembly`，实测签名可用，不改造 |
| Dashboard HttpListener | `Handlers/MqttDashboard.cs` | 实测 AOT 可用，不改造 |
| HttpClient/HttpListener 等 BCL 路径 | `Handlers/MqttDashboard.cs` | 实测通过，不改造 |
| 分片路由环不含自身 | `Clusters/TopicRouter.cs` | 不处理。路由环语义为"主题的转发目标节点"，逐节点计算且天然排除自身（本地投递另行处理）；`ShardRoutingE2ETests` 已按该语义固化验证。 |
| LA64 无 NativeAOT 支持 | `.github/workflows/aot.yml` | 官方/龙芯均无 linux-loongarch64 的 ILCompiler 产物；以 JIT 冒烟 + 全量单测覆盖平台行为；因仿真环境不稳定，aot.yml 的 LA64 冒烟列为观察期（continue-on-error，失败不阻断），有真机后再评估升级。 |
| LA64 仿真下不跑 MSBuild/csc/vstest | `.github/actions/setup-la64`、`test.yml` | 龙芯运行时为解释器模式，MSBuild 任务反射调用与 Roslyn 在仿真下会崩溃（实测 MSB4018 / FailFast），官方 vstest 亦不支持 loongarch64；固定为「x64 编译 IL + LA64 执行」，不追修复。 |
| LA64 部分测试类在仿真下不可运行 | `test.yml`（test-la64 批次清单） | 解释器在反射调用桩上存在缺陷（段错误/AccessViolation/ExecutionEngineException），与库代码无关，且偶发；按已实测稳定粒度分批覆盖 646 个用例、每批最多重试 3 次，其余类待运行时修复或真机 runner 回收，明细见 §6.2。 |

---

（完）
