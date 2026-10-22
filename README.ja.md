# NewLife.MQTT - MQTT プロトコル完全実装

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> この翻訳は [English README](README.md) より遅れている場合があります。

**NewLife.MQTT** は、**完全自社開発・サードパーティ依存ゼロ・MIT ライセンス**の MQTT プロトコル完全実装です。単一の NuGet パッケージで、機能豊富な `MqttClient` と高性能な `MqttServer`（ブローカー）の両方を提供し、.NET Framework 4.5 から .NET 10 までの主要ランタイムをカバーします。

## 📖 ドキュメントナビゲーション

| ドキュメント | 内容 |
|------|------|
| [要件定義](Doc/需求文档.md) | プロジェクトビジョン、7 大モジュールの要件、保留項目、非目標 |
| [機能一覧](Doc/功能清单.md) | 87 機能ポイントの実装・テスト・コメントの 3 次元追跡 |
| [アーキテクチャ設計](Doc/架构设计.md) | 5 層アーキテクチャ、データモデル、インターフェース、主要決定、性能設計 |
| [SRV サーバー設計](Doc/SRV-服务端架构.md) | ブローカー設計：メッセージルーティング、認証、HTTP 管理 API |
| [CLUS クラスタ設計](Doc/CLUS-集群架构.md) | 4 段階のクラスタ進化、Raft 合意、トピックシャーディング、多層トポロジ |
| [競合分析](Doc/竞品分析.md) | MQTT エコシステム比較（EMQX/MQTTnet/Mosquitto）と SWOT |
| [クラスタ競合分析](Doc/CLUS-集群竞品分析.md) | クラスタ機能の比較（EMQX/HiveMQ/VerneMQ） |
| [統合テスト](Doc/集成测试说明.md) | テスト構成、実行方法、カテゴリ、相互運用テスト（NewLife ↔ MQTTnet、CI 必須） |
| [AOT 互換性](Doc/AOT兼容性分析.md) | NativeAOT 計測：19 シナリオを JIT/AOT で検証、RID マトリクス |
| [リリースノート](Doc/更新日志.md) | バージョン履歴とコミット詳細 |

---

## 主な機能

| 機能 | 説明 |
|------|------|
| **完全なプロトコル** | MQTT 3.1 / 3.1.1 / 5.0、14 種の基本パケット + AUTH パケット、MQTT 5.0 の 30 プロパティ |
| **依存ゼロ** | NewLife.Core のみ。NuGet 依存チェーンにサードパーティなし |
| **クライアントとサーバーを一体化** | 単一の NuGet パッケージ `NewLife.MQTT`。個別ビルド不要 |
| **7 種のトランスポート** | TCP / TLS / WebSocket / WSS / QUIC（.NET 7+）/ 高信頼 UDP / ProxyProtocol v1/v2 |
| **高性能** | ノードあたり数百万接続（400 万接続を検証）。ブローカーは受信約 **125 万 msg/s**、単一接続で 106,000 msg/s。トピックルーティングは 1 エントリ 120 ns で一定 |
| **エンタープライズ機能を無償提供** | クラスタ（Raft / トピックシャーディング）/ ブリッジ / ルールエンジン / WebHook / $SYS / ACL |
| **自動再サブスクライブ** | 再接続後に全トピックを自動で再サブスクライブ |
| **SOCKS5 プロキシ** | ユーザー名/パスワード認証付き SOCKS5 を内蔵。TCP と WS に対応 |
| **バージョン網羅** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6～net10 |
| **NativeAOT** | 主要パスを NativeAOT で検証（管理 API、クラスタ RPC、TCP、WS、認証、MQTT-SN。19 シナリオ、ILC 警告 0） |
| **コンプライアンス** | 完全自社開発、MIT、コピーレフト/特許条項なし |

---

## 差別化優位性

### MQTT over QUIC（業界でも稀）
現在 **EMQX と NewLife.MQTT のみ**がこのトランスポートをサポート：
- 低遅延、ヘッドオブラインブロッキングなし。弱電波・モバイルに最適
- .NET 7/8 はプレビュー機能が必要、.NET 9 以降は安定
- 既定ポート 14567、ALPN 識別子 `"mqtt"`
- クライアント/サーバー全経路を検証済み（Linux/libmsquic、`QuicIntegrationTests`）

### MQTT over 高信頼 UDP（業界初）
**世界初、OS の msquic 不要、全プラットフォームで利用可能**：
- 4 バイトのフレームヘッダ + シーケンス番号 + ACK + タイムアウト再送
- msquic 非対応の ARM Linux 4.x などの組込み環境をカバー

### 再接続後の自動再サブスクライブ
MQTTnet / Eclipse Paho は手動再サブスクライブが必要ですが、NewLife.MQTT は自動です：

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// 再接続後の追加コードは不要です。
```

### エンタープライズ機能を標準搭載・無償
- **メッセージブリッジ**：In / Out / Both、トピックプレフィックス変換、QoS 上限
- **ルールエンジン**：5 アクション（Republish / WebHook / Bridge / Drop / Custom）
- **WebHook**：6 種のイベントを非同期配信、失敗時自動再試行。MqttHandler + Exchange に統合
- **クラスタ**：Raft 合意 + 一貫性ハッシュによるトピックシャーディング + 多層ルーティング、ノード間のサブスクリプション同期とメッセージ転送、専用 RPC ポート（2883）
- **MQTT-SN v1.2**：制約デバイス（センサー）向けゲートウェイ。MQTT トピックと相互運用
- **永続ストレージ**：オプションの `NewLife.MQTT.Storage`（Redis / データベース）でオフラインメッセージとセッションを保存
- **$SYS システムトピック**：14 種の標準トピックでリアルタイム統計
- **ProxyProtocol v1/v2**：nginx/HAProxy 経由で実クライアント IP を保持

---

## クイックスタート

### インストール

```bash
dotnet add package NewLife.MQTT
```

### サーバー起動

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

### クライアント接続

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// サブスクライブ（再接続後は自動で再サブスクライブ）
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// パブリッシュ
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### 接続文字列による初期化

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## クライアントの詳細

### プロトコルバージョンの指定

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311（既定）/ V500
};
```

### QoS パブリッシュ

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### QoS の意味

実際の消費 QoS は、パブリッシュ QoS とサブスクリプション QoS の**小さい方**です：

| QoS | 名称 | 説明 |
|-----|------|------|
| 0 | 最大 1 回 | 送信後の確認なし。消失の可能性あり。センサーのハートビート向け |
| 1 | 最低 1 回 | ブローカーが PUBACK を返す。重複の可能性あり。重要データ向け |
| 2 | 正確に 1 回 | 4 段階ハンドシェイク。消失も重複もなし。課金/注文向け |

### ワイルドカードサブスクリプション

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + は 1 階層
await client.SubscribeAsync("/device/#", handler);        // # は複数階層
```

### 共有サブスクリプション（負荷分散）

形式 `$share/{group}/{topic}`。同じグループの複数コンシューマーが順番に受信します：

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"受信: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC（.NET 7+）
```

> WebSocket サーバー：v12 以降は独立した `HttpServer` がホストします（`EnableWebSocket = true`、`WebSocketPort` は既定 0 でシステム割当、`WebSocketPath` は既定 `/mqtt`）。MQTT の TCP ポートとは共用しません。クライアントは引き続き `ws://host:port/mqtt` で接続します。`WebSocketCertificate` を設定すると TLS が有効になり `wss://host:port/mqtt` になります。

---

## サーバーの詳細

### カスタムハンドラー

`MqttHandler` を継承して全メッセージを横取りできます。データを直接保存する IoT プラットフォームに最適です：

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

### カスタム認証（ACL）

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

## エンタープライズ機能

### メッセージブリッジ

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

### ルールエンジン

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

### クラスタ展開

3 ノード構成（各ノードが他ノードのアドレスを列挙）：

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

運用：起動時 Join → 15 秒ごとに Ping → 終了時 Lease。タイムアウト（5 分）したノードは自動削除されます。

> v12 以降、クラスタ RPC は HTTP を使用します（`ClusterPort` は HTTP ポート。アドレスは `host:port` または `http://host:port`）。`ClusterSecret` が呼び出しごとにトークンとして検証され、NewLife.Remoting は不要になりました。

### Alibaba Cloud IoT アダプター

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

## すぐに試す

ソースコードを開き、`Test` をスタートアッププロジェクトに設定してサーバーとクライアントを同時に実行します：

![Demo](Doc/Demo.png)

---

## 主なハイライト

| ハイライト | 説明 |
|------|------|
| 🚀 **完全なプロトコル** | MQTT 3.1.1 完全対応（14 パケット）+ MQTT 5.0 主要機能（30 プロパティ） |
| 🔒 **依存ゼロ** | NewLife.Core のみ。サプライチェーンリスクなし、ライセンス競合なし |
| ⚖️ **MIT オープンソース** | 最も寛容なライセンス。商用利用・改変自由、コピーレフトなし、特許条項なし |
| 🎯 **クライアント + サーバー** | 単一の `NewLife.MQTT` パッケージで両方を提供。統合が最小限 |
| 🌐 **7 種のトランスポート** | TCP / TLS / WebSocket / WSS / QUIC（.NET 7+）/ 高信頼 UDP / ProxyProtocol v1/v2 |
| 🏢 **無償のエンタープライズ機能** | クラスタ / ブリッジ / ルールエンジン / WebHook / 統計 / $SYS |
| 🇨🇳 **完全自社開発** | 完全な独自知的財産。外部依存の法務リスクなし、コンプライアンス対応 |
| 🔧 **バージョン網羅** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## その他の優位性

### 1. MQTT over QUIC（業界でも稀）
- **EMQX と NewLife.MQTT のみ対応**
- 低遅延、ヘッドオブラインブロッキングなし
- .NET 7+ で利用可能（7/8 はプレビュー、9+ は安定）

### 2. MQTT over 高信頼 UDP（自社開発）
- **業界唯一**
- 4 バイトヘッダ + シーケンス + Ack + タイムアウト再送
- OS の QUIC/msquic 不要。全プラットフォームで動作

### 3. エンタープライズ機能を標準搭載・無償
- **双方向ブリッジ：** In/Out/Both、トピックプレフィックス、QoS 上限
- **ルールエンジン（5 アクション）：** Republish / WebHook / Bridge / Drop / Custom
- **WebHook：** 6 種のイベント、非同期・非ブロッキング、再試行対応
- **クラスタ：** 手動ノード構成、サブスクリプション同期、メッセージ転送
- **ProxyProtocol：** nginx/HAProxy v1/v2 で実クライアント IP を保持
- **$SYS トピック：** 14 種の標準トピックでリアルタイム統計

### 4. 再接続後の自動再サブスクライブ
競合（MQTTnet / Eclipse Paho）は手動再サブスクライブが必要ですが、NewLife.MQTT は自動です。

### 5. Alibaba Cloud IoT アダプター内蔵
`AliyunMqttClient` はすぐに使えます：HMAC 署名、プロパティ報告、時刻同期。

## MQTT プロトコル
MQTT は最も普及している IoT 通信プロトコルで、クライアント、サーバー、Web 管理プラットフォームを含みます。

よりシンプルで軽量、使いやすい Pub/Sub モデルを提供し、制約環境（低帯域、高遅延、不安定なネットワーク）を対象とします。要するにモノのインターネット向けです。公式の特徴：
1. Pub/Sub パターンで 1 対多配信とアプリケーションの疎結合を実現。
2. ペイロード内容に依存しないトランスポート。
3. TCP/IP によるネットワーク接続。
4. 3 段階の QoS：
「最大 1 回」：TCP/IP に完全依存。消失や重複の可能性。環境センサーなど、1 回の欠落が問題にならない用途向け。
「最低 1 回」：配信を保証するが重複の可能性。
「正確に 1 回」：正確に 1 回配信。重複や消失が誤った結果を招く課金システムなど向け。
5. 固定ヘッダ 2 バイトの低オーバーヘッドと最小限のプロトコル交換。
6. Last Will and Testament により異常切断を関係者に通知。

## MQTT のパブリッシュとサブスクライブ
パブリッシュ時に QoS を指定し、ブローカーが保存します。サブスクライブ時の消費 QoS は、パブリッシュ QoS とサブスクリプション QoS の小さい方です。

詳細：
- サブスクリプション QoS=0：パブリッシュに関係なく消費は常に QoS 0；
- サブスクリプション QoS=1：パブリッシュ QoS=0 → 消費 QoS=0、パブリッシュ QoS=1/2 → 消費 QoS=1；
- サブスクリプション QoS=2：消費 QoS はパブリッシュ QoS と同じ；
- パブリッシュ QoS=0：ブローカーは応答しない。途中の消失は理論上不明だが、TCP が障害を検出；
- パブリッシュ QoS=1：ブローカーが `PubAck` を返す；
- パブリッシュ QoS=2：ブローカーが `PubRec`、クライアントが `PubRel`、ブローカーが `PubComp` を返して完了；
- サブスクリプション QoS=2：ブローカーが QoS=2 を送信、クライアントが `PubRec`、ブローカーが `PubRel`、クライアントが `PubComp` を返して完了；
- QoS=2 ではコンシューマーがオンラインである必要はなく、パブリッシャーとブローカーの間で完結。

## クイック体験
ソリューションを開き、Test をスタートアッププロジェクトに設定して起動します。既定では TestServer と TestClient が順に起動します。

![Demo](Doc/Demo.png)

## サーバー
NuGet から `NewLife.MQTT` を参照し、サーバーを起動します：

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

ポート 1883、既定ハンドラー `MqttHandler`、既定 Exchange `MqttExchange` を指定すると起動します。

## クライアント
NuGet から `NewLife.MQTT` を参照し、接続します：

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

// トピック "/test" をサブスクライブ
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// 2 秒ごとに "/test" へパブリッシュ
while (true)
{
    try
    {
        var msg = "学問に先後なし、達した者が師" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

クライアントは `サーバーアドレス`、`ユーザー名`、`パスワード`、`クライアント ID` を指定して `ConnectAsync` で接続します。`SubscribeAsync` で消費、`PublishAsync` で生産できます。

## 共有サブスクリプション（EMQX）
EMQX の共有サブスクリプションに対応：複数クライアントが同じ共有トピックをサブスクライブし、ブローカーが負荷分散して配信します。形式は `$share/{group}/{topic}` です。

```csharp
// 共有トピックをサブスクライブ。同じグループの複数クライアントが負荷分散して受信
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"共有サブスクリプション受信: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// 実際のトピックへパブリッシュ（$share プレフィックスなし）
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

利点：
- **負荷分散**：グループ内で均等に配信
- **水平スケール**：サブスクライバー追加で処理能力向上
- **高可用性**：1 台故障しても他が処理を継続

### MQTT プロトコルバージョンの指定
バージョン 2.3.2025.0212 以降、`Version` プロパティで MQTT 3.1、3.1.1、5.0 を選択できます：

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

対応バージョン：
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1（既定）
- `MqttVersion.V500` - MQTT 5.0

**注意**：明示しない場合は後方互換のため MQTT 3.1.1 が使われます。

## カスタムサーバー
サーバー側で接続やメッセージ処理を制御する場合はカスタムサーバーが必要です。たとえば IoT プラットフォームが受信時にデバイスデータを直接保存できます。

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

ハンドラー登録を変更します：

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

## クラスタ
クラスタ管理：Join、Ping、Lease。
Join はクラスタに参加して自ノードを通知します。起動時に N-1 回呼びます。
Ping は 15 秒ごとに全ノードのアクティブ時刻を更新します。
Lease はクラスタから離脱します。N-1 回呼びます。
各ノードはアクティブ時刻が切れたノードを定期的に削除します。

## エコシステムマップ

NewLife は、独立したバージョン管理を行うオープンソースプロジェクトの階層型エコシステムです。以下のマップはすべての NewLife リポジトリで逐語的に同一の内容を共有しており、各プロジェクトはコードリポジトリと主要な NuGet パッケージにリンクしています。

| プロジェクト | 概要 | ダウンロード数 |
| --- | --- | --- |
| **基盤** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | ログ、構成、キャッシュ、ネットワーク、シリアライズ、スケジューリング、APM トレーシング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | データミドルウェア：単一テーブルで数百億件級、自動シャーディング、読み書き分離、10 種類以上のデータベースに対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能ネットワークライブラリ：単一マシンで 177 万 pps、14.5 Gbps（TCP 1KB、2026-10 実測）、400 万 Tcp 長接続。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS アプリケーションと IoT デバイス向けのプロトコル通信と RPC フレームワーク。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET アプリケーションを OS デーモンとしてインストール：Windows サービスと Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | コンソール、Web、API、サービス、データ、IoT 向けの `dotnet new` プロジェクトテンプレート。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **データとメッセージング** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | マイクロ秒級の遅延、数百万 ops/s のスループット、メッセージキュー内蔵の Redis クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ と Alibaba Cloud Message Queue に対応した純マネージド RocketMQ クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT クライアントとサーバー。Alibaba Cloud IoT への接続を含みます。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 製ハイブリッドデータベース：組み込み/ネットワークの両モード、リレーショナル、時系列、メッセージキュー、KV を 1 つに。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife データスタック上に構築された MySql ドライバーとヘルパー層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI、分散** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 高速管理プラットフォームフレームワーク：権限、SSO、OAuth サーバー、CRUD スキャフォールディング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散サービスプラットフォーム：ノード管理、APM、構成センター、登録センター、リリースセンター。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | シャーディング、リトライ、バックフィルに対応した分散タスクとリアルタイムデータのスケジューリングプラットフォーム。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46 社の LLM プロバイダーに統一接続するクライアントと、すぐに使える Web チャットアプリ ChatAI。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **産業と IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | プロトコル契約とデバイス抽象を定義する IoT 標準ライブラリ。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 上に実装された Modbus TCP/RTU/ASCII。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 上に実装されたシーメンス PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 上に実装されたオムロン PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 上に実装された三菱 Melsec PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 産業データアクセス向けの OPC クライアント。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet ビル管理プロトコルスタック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | ゲートウェイとデバイス管理を備えた軽量なオープンソース IoT プラットフォーム。 | — |
| **車載テレマティクス** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車両位置情報プロトコル：70 以上のメッセージボディ、2011/2013/2019 に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 運輸管理部門向けの JT/T 809 プラットフォーム間プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載ビデオ：RTP 解析、FLV/fMP4 リマックス、ライブ配信。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **ドメインコンポーネント** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel、Word、PPT、PDF など 15 以上のドキュメント形式を 1 パッケージで。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | クロスプラットフォーム音声：コーデック、デバイス IO、DSP、コンテナ、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | ビデオコーデック、コンテナ形式、フレーム処理、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度地図、高徳地図、テンセント地図、天地図を統一インターフェイス化。ジオコーディングと座標変換に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 位置情報：オフラインのメモリマップデータベースとオンライン API のフォールバック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **ツールと製品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開発者向けツールボックス：ネットワーク、シリアルポート、暗号化、正規表現、Modbus、MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | セルフホスト型 Git ソースコード管理システム。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 古いホストから起動した際に対応する .NET ランタイムを自動インストールします。 | — |

> バッジは各プロジェクトの主要パッケージを示します。テーマ、拡張、サテライトパッケージは各 NuGet ページを参照してください。ここに記載していない 30 以上のパッケージ（FTP、LoRa、Office 拡張、各種プロトコルドライバーなど）は [NewLifeX 組織](https://github.com/NewLifeX) と [newlifex.com](https://newlifex.com) で公開しています。

## 新生命開発チーム

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命チームは 2002 年に設立され、20 年以上にわたり .NET と IoT の技術エコシステムに注力し、100 近いオープンソースプロジェクトを主導し（うち 34 が主要プロジェクト）、電力、物流、医療、産業制御、インターネットなどの業界にソフトウェアとハードウェアを一体化したソリューションを提供しています。「学問に先後なし、達する者が師である」を信条としています。

オープンソースコンポーネントは NuGet で累計 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 回ダウンロードされています（エコシステム全体、2026-10 時点）。

ウェブサイト：https://newlifex.com · オープンソース組織：https://github.com/NewLifeX

<details>
<summary>WeChat 公式アカウント：智能大石头（スキャンしてフォロー）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## ライセンス

MIT License © 2002-2026 NewLife チーム

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
