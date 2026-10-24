# NewLife.MQTT - MQTT 프로토콜 완전 구현

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 이 번역은 [English README](README.md)보다 늦을 수 있습니다.

**NewLife.MQTT**는 **완전 자체 개발, 서드파티 의존성 제로, MIT 라이선스**의 MQTT 프로토콜 완전 구현입니다. 단일 NuGet 패키지로 기능이 완비된 `MqttClient`와 고성능 `MqttServer`(브로커)를 함께 제공하며, .NET Framework 4.5부터 .NET 10까지 주요 런타임을 지원합니다.

## 📖 문서 안내

| 문서 | 설명 |
|------|------|
| [요구사항](Doc/需求文档.md) | 프로젝트 비전, 7개 핵심 모듈 요구사항, 보류 항목과 비목표 |
| [기능 목록](Doc/功能清单.md) | 87개 기능 포인트의 구현·테스트·주석 3차원 추적 |
| [아키텍처 설계](Doc/架构设计.md) | 5계층 아키텍처, 데이터 모델, 인터페이스, 핵심 결정, 성능 설계 |
| [SRV 서버 아키텍처](Doc/SRV-服务端架构.md) | 브로커 설계: 메시지 라우팅, 인증, HTTP 관리 API |
| [CLUS 클러스터 아키텍처](Doc/CLUS-集群架构.md) | 4단계 클러스터 진화, Raft 합의, 토픽 샤딩, 다계층 토폴로지 |
| [경쟁 분석](Doc/竞品分析.md) | MQTT 생태계 비교(EMQX/MQTTnet/Mosquitto)와 SWOT |
| [클러스터 경쟁 분석](Doc/CLUS-集群竞品分析.md) | 클러스터 기능 비교(EMQX/HiveMQ/VerneMQ) |
| [통합 테스트](Doc/集成测试说明.md) | 테스트 구조, 실행 방법, 범주, 상호운용 테스트(NewLife ↔ MQTTnet, CI 필수) |
| [AOT 호환성](Doc/AOT兼容性分析.md) | NativeAOT 측정: 19개 시나리오 JIT/AOT 검증, RID 매트릭스 |
| [릴리스 노트](Doc/更新日志.md) | 버전 기록과 커밋 상세 |

---

## 핵심 기능

| 기능 | 설명 |
|------|------|
| **완전한 프로토콜** | MQTT 3.1 / 3.1.1 / 5.0, 14개 기본 패킷 + AUTH 패킷, MQTT 5.0 속성 30개 |
| **의존성 제로** | NewLife.Core만 의존, NuGet 체인에 서드파티 없음 |
| **클라이언트 + 서버 통합** | 단일 NuGet 패키지 `NewLife.MQTT`, 별도 빌드 불필요 |
| **7가지 전송** | TCP / TLS / WebSocket / WSS / QUIC(.NET 7+) / 신뢰성 UDP / ProxyProtocol v1/v2 |
| **고성능** | 노드당 수백만 연결(400만 검증), 브로커 수신 약 **125만 msg/s**, 단일 연결 종단 간 106,000 msg/s, 토픽 라우팅 120ns 고정 |
| **엔터프라이즈 기능 무료** | 클러스터(Raft/토픽 샤딩) / 브리지 / 규칙 엔진 / WebHook / $SYS / ACL |
| **자동 재구독** | 재연결 후 모든 토픽 자동 재구독, 추가 코드 불필요 |
| **SOCKS5 프록시** | 사용자명/비밀번호 인증 내장, TCP와 WS 지원 |
| **버전 지원** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6~net10 |
| **NativeAOT** | 주요 경로 NativeAOT 검증(관리 API, 클러스터 RPC, TCP, WS, 인증, MQTT-SN; 19개 시나리오, ILC 경고 0) |
| **31개 언어 지역화** | 런타임 메시지, 대시보드 및 어셈블리 메타데이터를 NewLife.Core 컴파일 타임 사전으로 31개 언어로 지역화하며, 누락된 항목은 영어로 대체됩니다 |
| **이벤트 버스** | `MqttEventBus`는 MQTT 토픽 기반의 강력한 형식 게시/구독을 제공합니다. 기본은 브로드캐스트이며 `$share` 공유 구독을 선택할 수 있고 NewLife.Core `IEventBusFactory` 계약을 따릅니다 |
| **컴플라이언스** | 완전 자체 개발, MIT, copyleft/특허 조항 없음 |

---

## 차별화 우위

### MQTT over QUIC (업계에서 드묾)
현재 **EMQX와 NewLife.MQTT만** 이 전송을 지원합니다:
- 저지연, 헤드오브라인 블로킹 없음, 약전계·모바일 환경에 적합
- .NET 7/8은 미리보기 기능 필요, .NET 9 이상 안정
- 기본 포트 14567, ALPN 식별자 `"mqtt"`
- 클라이언트/서버 전체 경로 종단 간 검증(Linux/libmsquic, `QuicIntegrationTests`)

### MQTT over 신뢰성 UDP (업계 최초)
**세계 최초, OS msquic 불필요, 모든 플랫폼 지원**:
- 4바이트 프레임 헤더 + 시퀀스 번호 + ACK + 타임아웃 재전송
- msquic 미지원 ARM Linux 4.x 등 임베디드 환경 지원

### 재연결 후 자동 재구독
MQTTnet / Eclipse Paho는 수동 재구독이 필요하지만 NewLife.MQTT는 자동입니다:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// 재연결 후 추가 코드가 필요 없습니다.
```

### 엔터프라이즈 기능 기본 탑재·무료
- **메시지 브리지**: In / Out / Both, 토픽 접두사 매핑, QoS 상한
- **규칙 엔진**: 5가지 액션(Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6가지 이벤트 비동기 푸시, 실패 시 자동 재시도, MqttHandler + Exchange 통합
- **클러스터**: Raft 합의 + 일관 해시 토픽 샤딩 + 다계층 라우팅, 노드 간 구독 동기화와 메시지 전달, 전용 RPC 포트(2883)
- **MQTT-SN v1.2**: 제약 장치(센서)용 게이트웨이, MQTT 토픽과 상호운용
- **영구 저장**: 선택형 `NewLife.MQTT.Storage`(Redis / 데이터베이스)로 오프라인 메시지와 세션 저장
- **$SYS 시스템 토픽**: 14개 표준 토픽, 실시간 통계
- **ProxyProtocol v1/v2**: nginx/HAProxy를 거쳐 실제 클라이언트 IP 유지

---

## 빠른 시작

### 설치

```bash
dotnet add package NewLife.MQTT
```

### 서버 시작

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

### 클라이언트 연결

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// 구독(재연결 후 자동 재구독)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// 게시
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### 연결 문자열 초기화

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## 클라이언트 상세

### 프로토콜 버전 지정

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311(기본) / V500
};
```

### QoS 게시

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### QoS 의미

실제 소비 QoS는 게시 QoS와 구독 QoS 중 **작은 값**입니다:

| QoS | 이름 | 설명 |
|-----|------|------|
| 0 | 최대 한 번 | 전송 후 확인 없음, 유실 가능, 센서 하트비트에 적합 |
| 1 | 최소 한 번 | 브로커가 PUBACK 응답, 중복 가능, 중요 데이터에 적합 |
| 2 | 정확히 한 번 | 4단계 핸드셰이크, 유실·중복 없음, 과금/주문에 적합 |

### 와일드카드 구독

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // +는 한 단계
await client.SubscribeAsync("/device/#", handler);        // #는 여러 단계
```

### 공유 구독(부하 분산)

형식 `$share/{group}/{topic}`, 같은 그룹의 여러 소비자가 순차적으로 수신합니다:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"수신: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC(.NET 7+)
```

> WebSocket 서버: v12부터 별도 `HttpServer`가 호스팅하며(`EnableWebSocket = true`, `WebSocketPort` 기본 0으로 시스템 할당, `WebSocketPath` 기본 `/mqtt`), MQTT TCP 포트를 공유하지 않습니다. 클라이언트는 계속 `ws://host:port/mqtt`로 연결합니다. `WebSocketCertificate`를 설정하면 TLS가 활성화되어 `wss://host:port/mqtt`가 됩니다.

---

## 서버 상세

### 사용자 정의 핸들러

`MqttHandler`를 상속해 모든 메시지를 가로챌 수 있으며, 데이터를 직접 저장하는 IoT 플랫폼에 적합합니다:

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

### 사용자 정의 인증(ACL)

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

## 엔터프라이즈 기능

### 메시지 브리지

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

### 규칙 엔진

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

### 클러스터 배포

3노드 클러스터(각 노드가 다른 노드 주소를 나열):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

운영: 시작 시 Join → 15초마다 Ping → 종료 시 Lease, 타임아웃(5분) 노드는 자동 제거됩니다.

> v12부터 클러스터 RPC는 HTTP를 사용합니다(`ClusterPort`는 HTTP 포트, 주소는 `host:port` 또는 `http://host:port`). `ClusterSecret`이 호출마다 토큰으로 검증되며 NewLife.Remoting은 더 이상 필요하지 않습니다.

### Alibaba Cloud IoT 어댑터

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

## 빠르게 체험

소스 코드를 열고 `Test`를 시작 프로젝트로 설정해 서버와 클라이언트를 동시에 실행합니다:

![Demo](Doc/Demo.png)

---

## 핵심 하이라이트

| 하이라이트 | 설명 |
|------|------|
| 🚀 **완전한 프로토콜** | MQTT 3.1.1 완전 지원(14개 패킷) + MQTT 5.0 핵심 기능(속성 30개) |
| 🔒 **의존성 제로** | NewLife.Core만 의존, 공급망 보안 위험 없음, 라이선스 충돌 없음 |
| ⚖️ **MIT 오픈소스** | 가장 관대한 라이선스, 상업적 사용·수정 자유, copyleft/특허 없음 |
| 🎯 **클라이언트 + 서버** | 단일 `NewLife.MQTT` 패키지로 둘 다 제공, 통합 최소화 |
| 🌐 **7가지 전송** | TCP / TLS / WebSocket / WSS / QUIC(.NET 7+) / 신뢰성 UDP / ProxyProtocol v1/v2 |
| 🏢 **무료 엔터프라이즈 기능** | 클러스터 / 브리지 / 규칙 엔진 / WebHook / 통계 / $SYS |
| 🇨🇳 **완전 자체 개발** | 완전한 지식재산권, 외부 의존 법적 위험 없음, 컴플라이언스 대응 |
| 🔧 **버전 지원** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## 추가 우위

### 1. MQTT over QUIC (업계에서 드묾)
- **EMQX와 NewLife.MQTT만 지원**
- 저지연, 헤드오브라인 블로킹 없음
- .NET 7+ 지원(7/8 미리보기, 9+ 안정)

### 2. MQTT over 신뢰성 UDP (자체 개발)
- **업계 유일**
- 4바이트 헤더 + 시퀀스 + Ack + 타임아웃 재전송
- OS QUIC/msquic 불필요, 모든 플랫폼 동작

### 3. 엔터프라이즈 기능 기본 탑재·무료
- **양방향 브리지:** In/Out/Both, 토픽 접두사, QoS 상한
- **규칙 엔진(5액션):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook:** 6가지 이벤트, 비동기·비차단, 재시도 지원
- **클러스터:** 수동 노드 구성, 구독 동기화, 메시지 전달
- **ProxyProtocol:** nginx/HAProxy v1/v2로 실제 클라이언트 IP 유지
- **$SYS 토픽:** 14개 표준 토픽, 실시간 통계

### 4. 재연결 후 자동 재구독
경쟁 제품(MQTTnet / Eclipse Paho)은 수동 재구독이 필요하지만 NewLife.MQTT는 자동입니다.

### 5. Alibaba Cloud IoT 어댑터 내장
`AliyunMqttClient`는 즉시 사용 가능: HMAC 서명, 속성 보고, 시각 동기화.

## MQTT 프로토콜
MQTT는 가장 널리 쓰이는 IoT 통신 프로토콜로 클라이언트, 서버, 웹 관리 플랫폼을 포함합니다.

더 단순하고 가볍고 사용하기 쉬운 Pub/Sub 모델을 제공하며 저대역폭, 고지연, 불안정 네트워크 등 제약 환경을 대상으로 합니다. 요약하면 사물인터넷을 위해 만들어졌습니다. 공식 특징:
1. Pub/Sub 패턴으로 1:N 배포와 애플리케이션 디커플링 제공.
2. 페이로드 내용에 독립적인 전송.
3. TCP/IP 기반 네트워크 연결.
4. 3단계 QoS:
"최대 한 번": TCP/IP에 전적으로 의존, 유실/중복 가능. 환경 센서처럼 한 번의 누락이 문제되지 않는 경우 적합.
"최소 한 번": 전달 보장, 중복 가능.
"정확히 한 번": 정확히 한 번 전달. 중복/유실이 잘못된 결과를 초래하는 과금 시스템 등에 적합.
5. 고정 헤더 2바이트의 낮은 오버헤드와 최소 프로토콜 교환.
6. Last Will and Testament로 비정상 연결 종료를 관계자에게 통지.

## MQTT 게시와 구독
게시 시 QoS를 지정하고 브로커가 저장합니다. 구독 시 소비 QoS는 게시 QoS와 구독 QoS 중 작은 값입니다.

세부:
- 구독 QoS=0: 게시와 무관하게 소비는 항상 QoS 0;
- 구독 QoS=1: 게시 QoS=0 → 소비 QoS=0, 게시 QoS=1/2 → 소비 QoS=1;
- 구독 QoS=2: 소비 QoS는 게시 QoS와 동일;
- 게시 QoS=0: 브로커 응답 없음, 이론상 중간 유실은 감지 불가하지만 TCP가 장애를 감지;
- 게시 QoS=1: 브로커가 `PubAck` 응답;
- 게시 QoS=2: 브로커 `PubRec`, 클라이언트 `PubRel`, 브로커 `PubComp` 후 게시 완료;
- 구독 QoS=2: 브로커가 QoS=2 전송, 클라이언트 `PubRec`, 브로커 `PubRel`, 클라이언트 `PubComp` 후 소비 완료;
- QoS=2는 소비자가 온라인일 필요 없이 게시자와 브로커 사이에서 완료됩니다.

## 빠른 체험
솔루션을 열고 Test를 시작 프로젝트로 설정해 실행합니다. 기본적으로 TestServer와 TestClient가 순서대로 시작됩니다.

![Demo](Doc/Demo.png)

## 서버
NuGet에서 `NewLife.MQTT`를 참조하고 서버를 시작합니다:

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

포트 1883, 기본 핸들러 `MqttHandler`, 기본 Exchange `MqttExchange`로 시작합니다.

## 클라이언트
NuGet에서 `NewLife.MQTT`를 참조하고 연결합니다:

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

// 토픽 "/test" 구독
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// 2초마다 "/test"에 게시
while (true)
{
    try
    {
        var msg = "배움에는 순서가 없다, 먼저 도달한 사람이 스승이다" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

클라이언트는 `서버 주소`, `사용자명`, `비밀번호`, `클라이언트 ID`를 지정해 `ConnectAsync`로 연결합니다. `SubscribeAsync`로 소비하고 `PublishAsync`로 생산할 수 있습니다.

## 공유 구독(EMQX)
EMQX 공유 구독을 지원합니다. 여러 클라이언트가 같은 공유 토픽을 구독하면 브로커가 부하 분산해 전달합니다. 형식은 `$share/{group}/{topic}`입니다.

```csharp
// 공유 토픽 구독, 같은 그룹의 여러 클라이언트가 부하 분산 수신
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"공유 구독 수신: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// 실제 토픽에 게시($share 접두사 없음)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

장점:
- **부하 분산**: 그룹 내 균등 분배
- **수평 확장**: 구독자 추가로 처리량 증가
- **고가용성**: 한 구독자가 실패해도 나머지가 계속 처리

### MQTT 프로토콜 버전 지정
버전 2.3.2025.0212부터 `Version` 속성으로 MQTT 3.1, 3.1.1, 5.0을 선택할 수 있습니다:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

지원 버전:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1(기본)
- `MqttVersion.V500` - MQTT 5.0

**참고**: 명시하지 않으면 하위 호환을 위해 MQTT 3.1.1을 사용합니다.

## 사용자 정의 서버
서버 측 연결과 메시지 처리가 필요하면 사용자 정의 서버가 필요합니다. 예를 들어 IoT 플랫폼이 수신 즉시 장치 데이터를 저장할 수 있습니다.

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

핸들러 등록을 변경합니다:

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

## 클러스터
클러스터 관리: Join, Ping, Lease.
Join은 클러스터에 참여하고 자신을 알립니다. 시작 시 N-1회 호출합니다.
Ping은 15초마다 모든 노드의 활성 시간을 갱신합니다.
Lease는 클러스터에서 이탈합니다. N-1회 호출합니다.
각 노드는 활성 시간이 만료된 노드를 주기적으로 제거합니다.

## 생태계 지도

NewLife는 독립적으로 버전이 관리되는 오픈소스 프로젝트의 계층형 생태계입니다. 아래 지도는 모든 NewLife 리포지토리에서 그대로 동일하게 공유되며, 각 프로젝트는 코드 리포지토리와 기본 NuGet 패키지로 연결됩니다.

| 프로젝트 | 한 줄 소개 | 다운로드 |
| --- | --- | --- |
| **기반** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 로깅, 구성, 캐시, 네트워크, 직렬화, 스케줄링, APM 추적. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 데이터 미들웨어: 단일 테이블 수백억 건, 자동 분할 테이블, 읽기/쓰기 분리, 10여 종 데이터베이스 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 고성능 네트워크 라이브러리: 단일 노드 177만 pps, 14.5 Gbps(TCP 1KB, 2026-10 실측), 400만 Tcp 장기 연결. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS 애플리케이션과 IoT 장치를 위한 프로토콜 통신 및 RPC 프레임워크. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET 애플리케이션을 운영체제 데몬으로 설치: Windows 서비스와 Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | 콘솔, Web, API, 서비스, 데이터, IoT용 `dotnet new` 프로젝트 템플릿. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **데이터 및 메시징** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 클라이언트: 마이크로초 수준 지연, 백만 수준 처리량, 메시지 큐 내장. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ와 알리바바 클라우드 메시지 큐를 지원하는 순수 관리형 RocketMQ 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 클라이언트와 서버, 알리바바 클라우드 IoT 연결 포함. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 하이브리드 데이터베이스: 임베디드/네트워크 두 모드, 관계형, 시계열, 메시지 큐, KV를 하나로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife 데이터 스택 위에 구축한 MySql 드라이버와 도우미 계층. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI 및 분산** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 빠른 관리 플랫폼 프레임워크: 권한, SSO, OAuth 서버, CRUD 스캐폴딩. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 분산 서비스 플랫폼: 노드 관리, APM, 구성 센터, 등록 센터, 배포 센터. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 샤딩, 재시도, 백필을 지원하는 분산 작업 및 실시간 데이터 스케줄링 플랫폼. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46개 LLM 공급자를 통합 연결하는 클라이언트와 바로 사용할 수 있는 ChatAI 웹 채팅 애플리케이션. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **산업 및 IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 프로토콜 계약과 장치 추상을 정의하는 IoT 표준 라이브러리. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 기반으로 구현한 Modbus TCP/RTU/ASCII. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 기반으로 구현한 지멘스 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 기반으로 구현한 오므론 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 기반으로 구현한 미쓰비시 Melsec PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 산업 데이터 수집용 OPC 클라이언트. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 빌딩 자동화 프로토콜 스택. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 게이트웨이와 장치 관리를 갖춘 경량 오픈소스 IoT 플랫폼. | — |
| **차량 텔레매틱스** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 차량 위치 프로토콜: 70개 이상 메시지 본문, 2011/2013/2019 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 운송 관리 부서를 위한 JT/T 809 플랫폼 간 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 차량용 비디오: RTP 파싱, FLV/fMP4 리먹싱, 라이브 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **도메인 컴포넌트** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel, Word, PPT, PDF 등 15개 이상 문서 형식을 하나의 패키지로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 크로스 플랫폼 오디오: 코덱, 장치 IO, DSP, 컨테이너, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 비디오 코덱, 컨테이너 형식, 프레임 처리, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 바이두/고덕/텐센트/톈디투 지도 통합 인터페이스, 지오코딩과 좌표 변환 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 위치 정보: 오프라인 메모리 매핑 데이터베이스와 온라인 API 이중 소스. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **도구 및 제품** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 개발자 도구 모음: 네트워크, 시리얼 포트, 암호화, 정규식, Modbus, MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 셀프 호스팅 Git 소스 코드 관리 시스템. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 구버전 호스트에서 시작할 때 일치하는 .NET 런타임을 자동으로 설치합니다. | — |

> 배지는 각 프로젝트의 기본 패키지를 나타냅니다. 테마, 확장, 위성 패키지는 해당 NuGet 페이지에서 확인하세요.여기에 나열하지 않은 30여 개 패키지(FTP, LoRa, Office 확장, 각종 프로토콜 드라이버 등)는 [NewLifeX 조직](https://github.com/NewLifeX)과 [newlifex.com](https://newlifex.com)에서 확인할 수 있습니다.

## 뉴라이프 개발팀

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

뉴라이프 팀(NewLife)은 2002년에 설립되어 20여 년간 .NET과 IoT 기술 생태계에 집중하며 100여 개의 오픈소스 프로젝트를 주도하고(이 중 34개가 핵심 프로젝트), 전력, 물류, 의료, 산업 제어, 인터넷 등 산업에 소프트웨어와 하드웨어를 아우르는 통합 솔루션을 제공해 왔습니다. "학문에는 선후가 없으며, 이룬 사람이 스승이다"라는 신조를 따릅니다.

오픈소스 컴포넌트는 NuGet에서 누적 <!--nuget-total:start-->6.48M<!--nuget-total:end-->회 다운로드되었습니다(생태계 전체 기준, 2026-10).

웹사이트: https://newlifex.com · 오픈소스 조직: https://github.com/NewLifeX

<details>
<summary>위챗 공식 계정: 智能大石头(스캔하여 팔로우)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 라이선스

MIT License © 2002-2026 NewLife 팀

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
