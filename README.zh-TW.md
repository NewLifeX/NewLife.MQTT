# NewLife.MQTT - MQTT 協定完整實現

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.MQTT?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.MQTT?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.MQTT?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.MQTT?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.MQTT?logo=github)](https://github.com/NewLifeX/NewLife.MQTT/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 本翻譯可能落後，以 [English README](README.md) 為準。

**NewLife.MQTT** 是一款**完全自主研發、零第三方依賴、MIT 開源**的 MQTT 完整協定實作，以單一 NuGet 套件同時提供功能完整的 `MqttClient` 用戶端與高效能 `MqttServer` 服務端（Broker），涵蓋 .NET Framework 4.5 至 .NET 10 全部主流執行環境。

## 📖 文件導覽

| 文件 | 說明 |
|------|------|
| [需求文件](Doc/需求文档.md) | 專案願景、7 大模組核心需求、暫緩項目與非目標 |
| [功能清單](Doc/功能清单.md) | 87 個功能點的實作／測試／註解三維追蹤 |
| [架構設計](Doc/架构设计.md) | 五層架構、資料模型、介面設計、關鍵決策與效能設計 |
| [SRV 服務端架構](Doc/SRV-服务端架构.md) | Broker 設計：訊息路由、安全驗證、HTTP 管理 API |
| [CLUS 叢集架構](Doc/CLUS-集群架构.md) | 四階段叢集演進、Raft 共識、主題分片與多層拓撲 |
| [競品分析](Doc/竞品分析.md) | MQTT 生態比較（EMQX/MQTTnet/Mosquitto）與 SWOT |
| [CLUS 叢集競品分析](Doc/CLUS-集群竞品分析.md) | 叢集能力專項比較（EMQX/HiveMQ/VerneMQ） |
| [整合測試](Doc/集成测试说明.md) | 測試架構、執行方式、測試分類與交叉相容（NewLife ↔ MQTTnet，CI 強制） |
| [AOT 相容性](Doc/AOT兼容性分析.md) | NativeAOT 量測：19 個主路徑同時通過 JIT 與 AOT，RID 矩陣 |
| [版本記錄](Doc/更新日志.md) | 版本發佈記錄與提交細節 |

---

## 核心特性

| 特性 | 說明 |
|------|------|
| **完整協定** | MQTT 3.1 / 3.1.1 / 5.0，14 種基礎封包 + AUTH 封包，30 個 MQTT 5.0 屬性 |
| **零依賴** | 僅依賴 NewLife.Core；NuGet 依賴鏈無任何第三方套件 |
| **用戶端與服務端一體** | 單一 NuGet 套件 `NewLife.MQTT`，無需分別建置 |
| **七種傳輸** | TCP / TLS / WebSocket / WSS / QUIC（.NET 7+）/ 可靠 UDP / ProxyProtocol v1/v2 |
| **高效能** | 單節點百萬連線（已驗證 400 萬）；Broker 入站約 **125 萬訊息/秒**，單連線端到端 106,000 訊息/秒；主題路由固定 120 奈秒/條目 |
| **企業功能免費** | 叢集（Raft／主題分片）／橋接／規則引擎／WebHook／$SYS／ACL |
| **斷線自動重訂閱** | 重連後自動重新訂閱全部主題，無需額外程式碼 |
| **SOCKS5 代理** | 內建 SOCKS5 代理，支援使用者名稱／密碼驗證，TCP 與 WS 均可轉發 |
| **完整版本覆蓋** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6～net10 |
| **NativeAOT** | 主路徑通過 NativeAOT 驗證（管理 API、叢集 RPC、TCP、WS、驗證、MQTT-SN；19 個情境，ILC 零警告） |
| **合規就緒** | 完全自主研發、MIT 授權，無 copyleft／專利條款 |

---

## 差異化優勢

### MQTT over QUIC（業界罕見）
目前**僅 EMQX 與 NewLife.MQTT** 支援此傳輸：
- 低延遲、無隊頭阻塞，適合弱網與行動場景
- .NET 7/8 需啟用預覽功能；.NET 9 以上穩定
- 預設埠 14567，ALPN 識別碼 `"mqtt"`
- 用戶端／服務端完整路徑已端到端驗證（Linux/libmsquic，`QuicIntegrationTests`）

### MQTT over 可靠 UDP（業界獨創）
**全球首創，無需作業系統 msquic，全平台可用**：
- 自研 4 位元組訊框標頭 + 序號 + ACK 確認 + 逾時重傳
- 涵蓋不支援 msquic 的 ARM Linux 4.x 等嵌入式平台

### 斷線後自動重訂閱
MQTTnet / Eclipse Paho 需使用者手動重訂閱；NewLife.MQTT 自動完成：

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// 重連後無需額外程式碼。
```

### 企業功能內建免費
- **訊息橋接**：In / Out / Both 方向、主題前綴對映、QoS 上限控制
- **規則引擎**：5 種動作（Republish / WebHook / Bridge / Drop / Custom）
- **WebHook**：6 種事件非同步推送，失敗自動重試；整合至 MqttHandler + Exchange 管線
- **叢集**：Raft 共識 + 一致性雜湊主題分片 + 多層拓撲路由，跨節點訂閱同步與訊息轉發，專用 RPC 埠（2883）
- **MQTT-SN v1.2**：受限裝置（感測器）接入閘道，與 MQTT 主題互通
- **持久化儲存**：選用 `NewLife.MQTT.Storage`（Redis／資料庫）實作離線訊息與工作階段保存
- **$SYS 系統主題**：14 個標準主題，即時執行統計
- **ProxyProtocol v1/v2**：經 nginx/HAProxy 保留真實用戶端 IP

---

## 快速開始

### 安裝

```bash
dotnet add package NewLife.MQTT
```

### 啟動服務端

```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MqttHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),
    Log = XTrace.Log,
};
server.Start();
```

### 連線用戶端

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// 訂閱（重連後自動重新訂閱）
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// 發佈
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### 連線字串初始化

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## 用戶端詳細用法

### 指定協定版本

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311（預設）/ V500
};
```

### QoS 發佈

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### QoS 服務品質說明

實際消費 QoS 為發佈 QoS 與訂閱 QoS 的**較小值**：

| QoS | 名稱 | 說明 |
|-----|------|------|
| 0 | 最多一次 | 發送後不確認，可能遺失，適合感測器心跳 |
| 1 | 至少一次 | Broker 回 PUBACK，可能重複，適合重要資料 |
| 2 | 恰好一次 | 四步握手，不遺失不重複，適合計費／訂單 |

### 萬用字元訂閱

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + 符合單層
await client.SubscribeAsync("/device/#", handler);        // # 符合多層
```

### 共享訂閱（負載平衡）

格式 `$share/{group}/{topic}`；同一群組的多個消費者輪流接收訊息：

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"收到：{e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC（.NET 7+）
```

> WebSocket 服務端：v12 起由獨立 `HttpServer` 託管（`EnableWebSocket = true`，`WebSocketPort` 預設 0 由系統分配，`WebSocketPath` 預設 `/mqtt`），不再與 MQTT TCP 埠共用；用戶端仍以 `ws://host:port/mqtt` 連線。設定 `WebSocketCertificate` 可在該埠啟用 TLS，即 `wss://host:port/mqtt`。

---

## 服務端詳細用法

### 自訂處理器

繼承 `MqttHandler` 可攔截各類訊息，適合直接持久化資料的 IoT 平台：

```csharp
public class MyHandler : MqttHandler
{
    private readonly ILog _log;

    public MyHandler(ILog log) => _log = log;

    protected override ConnAck OnConnect(ConnectMessage message)
    {
        _log.Info("Connected[{0}] user={1} clientId={2}",
            Session.Remote.EndPoint, message.Username, message.ClientId);
        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.Info("Disconnected[{0}]", Session.Remote);
        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.Info("[{0} QoS={1}] {2}", message.Topic, (Int32)message.QoS, message.Payload.ToStr());
        return base.OnPublish(message);
    }
}

services.AddTransient<IMqttHandler, MyHandler>();
```

### 自訂驗證（ACL）

```csharp
public class MyAuthenticator : IMqttAuthenticator
{
    public Boolean Authenticate(String clientId, String username, String password)
        => username == "admin" && password == "secret";

    public Boolean AuthorizePublish(String clientId, String topic)
        => !topic.StartsWith("$SYS/");

    public Boolean AuthorizeSubscribe(String clientId, String topic) => true;
}

services.AddSingleton<IMqttAuthenticator, MyAuthenticator>();
```

---

## 企業功能

### 訊息橋接

```csharp
var bridge = new MqttBridge
{
    Name = "to-cloud",
    Server = "tcp://cloud-broker.example.com:1883",
    Direction = BridgeDirection.Both,
    TopicPrefix = "edge/factory1/",
    MaxQoS = QualityOfService.AtLeastOnce,
};
bridge.Start();
```

### 規則引擎

```csharp
var engine = new MqttRuleEngine();
engine.AddRule(new MqttRule
{
    TopicFilter = "/sensor/+/temperature",
    Action = RuleAction.Republish,
    TargetTopic = "/analytics/temperature",
});
engine.AddRule(new MqttRule
{
    TopicFilter = "/alarm/#",
    Action = RuleAction.WebHook,
    WebHookUrl = "https://api.example.com/alarm",
});
```

### 叢集部署

三節點叢集（每個節點列出其他節點位址）：

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

運作方式：啟動時 Join → 每 15 秒 Ping → 關閉時 Lease；逾時（5 分鐘）節點自動移除。

> v12 起叢集 RPC 改用 HTTP（`ClusterPort` 為 HTTP 埠；位址可寫 `host:port` 或 `http://host:port`），`ClusterSecret` 每次呼叫皆作為權杖驗證；不再需要 NewLife.Remoting。

### 阿里雲 IoT 適配

```csharp
var client = new AliyunMqttClient
{
    ProductKey = "a1xxxxxxxx",
    DeviceName = "myDevice",
    DeviceSecret = "xxxxxxxx",
};
await client.ConnectAsync();
await client.PublishPropertyAsync(new { temperature = 25.6 });
```

---

## 快速體驗

開啟原始碼並將 `Test` 設為啟動專案，即可同時執行服務端與用戶端：

![Demo](Doc/Demo.png)

---

## 核心特點

| 特點 | 說明 |
|------|------|
| 🚀 **完整協定** | 完整支援 MQTT 3.1.1（14 種封包）+ MQTT 5.0 核心功能（30 個屬性） |
| 🔒 **零依賴** | 僅依賴 NewLife.Core 基礎庫，無供應鏈風險與授權衝突 |
| ⚖️ **MIT 開源** | 最寬鬆的開源授權，可自由修改與商用，無 copyleft、無專利條款 |
| 🎯 **用戶端 + 服務端** | 單一 `NewLife.MQTT` 套件同時提供兩者，整合成本最低 |
| 🌐 **七種傳輸** | TCP / TLS / WebSocket / WSS / QUIC（.NET 7+）/ 可靠 UDP / ProxyProtocol v1/v2 |
| 🏢 **企業功能免費** | 叢集／橋接／規則引擎／WebHook／統計／$SYS，競品多需企業版或付費外掛 |
| 🇨🇳 **完全自主研發** | 完整自主智慧財產權，無外部依賴法律風險，合規就緒 |
| 🔧 **完整版本覆蓋** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## 差異化優勢

### 1. MQTT over QUIC（業界罕見）
- **僅 EMQX 與 NewLife.MQTT 支援**
- 低延遲、無隊頭阻塞
- .NET 7+ 可用（7/8 預覽，9+ 穩定）

### 2. MQTT over 可靠 UDP（獨創自研）
- **業界唯一**
- 4 位元組訊框標頭 + 序號 + Ack 確認 + 逾時重傳
- 無需作業系統 QUIC/msquic，全平台運作

### 3. 企業功能內建免費
- **雙向訊息橋接：** In/Out/Both、主題前綴對映、QoS 上限
- **規則引擎（5 種動作）：** Republish / WebHook / Bridge / Drop / Custom
- **WebHook：** 6 種事件、非同步非阻塞、支援重試
- **叢集：** 手動節點組態、跨節點訂閱同步與訊息轉發
- **ProxyProtocol：** nginx/HAProxy v1/v2 保留真實用戶端 IP
- **$SYS 系統主題：** 14 個標準主題，即時執行統計

### 4. 重連後自動重訂閱
競品（MQTTnet / Eclipse Paho）需手動重訂閱；NewLife.MQTT 自動完成。

### 5. 阿里雲 IoT 內建適配
`AliyunMqttClient` 開箱即用：HMAC 簽名、屬性上報與時鐘同步。

## MQTT 協定
MQTT 是最受歡迎的 IoT 通訊協定，包含用戶端、服務端與 Web 管理平台。

它提供更簡單、輕量、易用的發佈／訂閱模型，面向受限環境（低頻寬、高延遲、不穩定網路）。簡而言之，就是為物聯網而生。官方特性如下：
1. 採用發佈／訂閱模式，提供一對多訊息分發與應用解耦。
2. 訊息傳輸與承載內容無關。
3. 使用 TCP/IP 提供網路連線。
4. 三種訊息發佈服務品質：
「最多一次」：完全依賴底層 TCP/IP，可能遺失或重複；適合環境感測器，遺失一筆很快就會有下一筆。
「至少一次」：保證送達，但可能重複。
「恰好一次」：保證恰好送達一次；適合計費系統，重複或遺失都會導致錯誤結果。
5. 傳輸量小、額外開銷極低（固定標頭 2 位元組），協定交換最少。
6. 透過 Last Will and Testament 在用戶端異常斷線時通知相關方。

## MQTT 發佈與訂閱
發佈時指定訊息 QoS，Broker 儲存的訊息包含該 QoS；訂閱時消費訊息的 QoS 為發佈與訂閱 QoS 的較小值。

詳細情境：
- 訂閱 QoS=0：無論發佈 QoS 為何，消費皆為 QoS 0；
- 訂閱 QoS=1：發佈 QoS=0 → 消費 QoS=0；發佈 QoS=1 或 2 → 消費 QoS=1；
- 訂閱 QoS=2：消費 QoS 與發佈 QoS 相同；
- 發佈 QoS=0：Broker 完全不回覆；理論上中途遺失無法察覺，但 TCP 可偵測網路故障；
- 發佈 QoS=1：Broker 回覆 `PubAck`，表示已收到；
- 發佈 QoS=2：Broker 回覆 `PubRec`，用戶端再送 `PubRel`，Broker 回覆 `PubComp`，才算完成發佈；
- 訂閱 QoS=2：Broker 推送 QoS=2 訊息，用戶端先回 `PubRec`，Broker 再送 `PubRel`，用戶端回 `PubComp`，才算完成消費；
- 發佈 QoS=2 時，雙重確認流程不需要消費者在線，僅在發佈者與 Broker 之間完成。

## 快速嘗鮮
開啟原始碼方案，將 Test 設為啟動專案並啟動。預設會先後啟動 TestServer 與 TestClient。

![Demo](Doc/Demo.png)

## 服務端
從 NuGet 引用 `NewLife.MQTT`，使用以下程式碼啟動服務端：

```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MqttHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),

    Log = XTrace.Log,
    SessionLog = XTrace.Log,
};
server.Start();
```

指定 1883 埠、預設處理器 `MqttHandler` 與預設交換器 `MqttExchange`，服務端即可啟動。

## 用戶端
從 NuGet 引用 `NewLife.MQTT`，使用以下程式碼連線：

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    //UserName = "admin",
    //Password = "admin",
    ClientId = Guid.NewGuid() + "",
};

await client.ConnectAsync().ConfigureAwait(false);

// 訂閱 "/test" 主題
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// 每 2 秒向 "/test" 發佈訊息
while (true)
{
    try
    {
        var msg = "學無先後，達者為師" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

用戶端連線需要 `伺服器位址`、`使用者名稱`、`密碼` 與 `用戶端識別碼`，再透過 `ConnectAsync` 連線。用戶端可作為消費者以 `SubscribeAsync` 訂閱，也可作為生產者以 `PublishAsync` 發佈。

## 共享訂閱（EMQX）
支援 EMQX 共享訂閱：多個用戶端訂閱同一共享主題，Broker 以負載平衡方式分發訊息。格式為 `$share/{group}/{topic}`。

```csharp
// 訂閱共享主題；同一群組的多個用戶端負載平衡接收
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"收到共享訂閱訊息：{e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// 向實際主題發佈（不含 $share 前綴）
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

優點：
- **負載平衡**：同群組訂閱者平均分攤訊息
- **水平擴充**：增加訂閱者即可提升處理能力
- **高可用**：一個訂閱者故障，其他訂閱者仍可處理

### 指定 MQTT 協定版本
自 2.3.2025.0212 起，用戶端支援以 `Version` 屬性指定 MQTT 3.1、3.1.1 或 5.0：

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // 指定 MQTT 5.0
};
```

支援版本：
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1（預設）
- `MqttVersion.V500` - MQTT 5.0

**注意**：未明確設定 `Version` 時，用戶端預設使用 MQTT 3.1.1 以維持向後相容。

## 自訂服務端
需要在服務端處理用戶端連線與訊息互動邏輯時，需使用自訂服務端。例如 IoT 平台可在收到裝置上報資料時直接儲存，無需再次消費。

```csharp
private class MyHandler : MqttHandler
{
    private readonly ILog _log;

    public MyHandler(ILog log) => _log = log;

    protected override ConnAck OnConnect(ConnectMessage message)
    {
        _log.Info("Client[{0}] connected user={1} pass={2} clientId={3}", Session.Remote.EndPoint, message.Username, message.Password, message.ClientId);

        return base.OnConnect(message);
    }

    protected override MqttMessage OnDisconnect(DisconnectMessage message)
    {
        _log.Info("Client[{0}] disconnected", Session.Remote);

        return base.OnDisconnect(message);
    }

    protected override MqttIdMessage OnPublish(PublishMessage message)
    {
        _log.Info("Client[{0}] published [{1}:qos={2}]: {3}", Session.Remote, message.Topic, (Int32)message.QoS, message.Payload.ToStr());

        return base.OnPublish(message);
    }
}
```

只需調整服務端的處理器註冊：

```csharp
var services = ObjectContainer.Current;
services.AddSingleton<ILog>(XTrace.Log);
services.AddTransient<IMqttHandler, MyHandler>();
services.AddSingleton<MqttExchange, MqttExchange>();

var server = new MqttServer
{
    Port = 1883,
    ServiceProvider = services.BuildServiceProvider(),

    Log = XTrace.Log,
    SessionLog = XTrace.Log,
};
server.Start();
```

## 叢集
叢集管理：Join、Ping、Lease。
Join 加入叢集並告知其他節點本身為叢集成員；啟動時呼叫 N-1 次。
每 15 秒 Ping 全部節點以更新活躍時間。
Lease 離開叢集；呼叫 N-1 次。
每個節點依最後活躍時間定期移除逾時節點。

## 生態版圖

NewLife 生態系按相依分層，各專案獨立版本、獨立發行。下面的版圖在所有 NewLife 儲存庫中逐字一致；每個專案連結到程式碼儲存庫與主 NuGet 套件。

| 專案 | 一句話定位 | 下載量 |
| --- | --- | --- |
| **基礎底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日誌、設定、快取、網路、序列化、排程與 APM 追蹤。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 資料中介層：單表百億級、自動分表、讀寫分離，支援 10 多種資料庫。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高效能網路庫：單機 177 萬 pps、14.5 Gbps（TCP 1KB，2026-10 實測），400 萬 Tcp 長連線。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 協定通訊與 RPC 框架，面向 CS 應用程式與物聯網裝置。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 將 .NET 應用程式安裝為作業系統常駐服務：Windows 服務與 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 專案範本：主控台、Web、API、服務、資料與物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **資料儲存與訊息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 用戶端：微秒級延遲、百萬級吞吐，內建訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 純受控 RocketMQ 用戶端，支援 Apache RocketMQ 與阿里雲訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 用戶端與服務端，含阿里雲物聯網接入。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合式資料庫：內嵌或網路雙模，關係型、時序、訊息佇列與 KV 於一體。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | 建構於 NewLife 資料堆疊之上的 MySql 驅動與輔助層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 與分散式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 快速後台開發框架：權限、SSO、OAuth 服務端與 CRUD 樣板。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散式服務平台：節點管理、APM、設定中心、註冊中心與發佈中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 分散式任務與即時資料排程平台，支援分片、重試與回溯補算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 統一接入 46 家大模型服務商，附帶開箱即用的 ChatAI Web 對話應用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工業與物聯網** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 物聯網標準庫，定義通訊協定契約與裝置抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 建構於 NewLife.IoT 之上的 Modbus TCP/RTU/ASCII 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 建構於 NewLife.IoT 之上的西門子 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 建構於 NewLife.IoT 之上的歐姆龍 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 建構於 NewLife.IoT 之上的三菱 Melsec PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 工業資料存取的 OPC 用戶端。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 樓宇自動化協定堆疊。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 開源輕量級物聯網平台，含閘道與裝置管理。 | — |
| **車聯網** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車輛定位協定：70+ 訊息體，涵蓋 2011/2013/2019 三個版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部標平台互聯協定，面向運輸管理部門。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載視訊：RTP 解析、FLV/fMP4 轉封裝與直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **領域元件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一個套件涵蓋 Excel、Word、PPT、PDF 等 15+ 文件格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音訊：編解碼、裝置 IO、DSP、容器與串流處理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 視訊編解碼、封裝格式、影格處理與串流。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度／高德／騰訊／天地圖統一介面，支援地理編碼與座標轉換。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 歸屬地：離線記憶體映射資料庫 + 線上 API 雙來源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具與產品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開發者工具箱：網路、序列埠、加解密、正規表示式、Modbus 與 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自架 Git 原始碼管理系統。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 從較舊的宿主啟動時，自動安裝相符的 .NET 執行階段。 | — |

> 徽章取各專案主套件；主題套件、擴充套件與衛星套件見對應 NuGet 頁面。未在表中列出的 30 多個套件（FTP、LoRa、Office 擴充、各類協定驅動等）見 [NewLifeX 組織](https://github.com/NewLifeX) 與 [newlifex.com](https://newlifex.com)。

## 新生命開發團隊

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命團隊成立於 2002 年，二十餘年專注 .NET 與物聯網技術生態，主導開源專案近百個（生態版圖收錄 34 個主幹專案），為電力、物流、醫療、工控、網際網路等行業提供軟硬體一體化解決方案；以「學無先後達者為師」為信條。

開源元件在 NuGet 累計下載 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生態口徑，截至 2026-10）。

網站：https://newlifex.com · 開源組織：https://github.com/NewLifeX

<details>
<summary>微信公眾號：智能大石头（掃碼關注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 授權

MIT License © 2002-2026 新生命開發團隊

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
