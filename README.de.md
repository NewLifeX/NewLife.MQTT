# NewLife.MQTT - Vollständige MQTT-Protokollimplementierung

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Diese Übersetzung kann hinter dem [English README](README.md) zurückliegen.

**NewLife.MQTT** ist eine **vollständig selbst entwickelte, von Drittanbietern unabhängige, MIT-lizenzierte** komplette MQTT-Protokollimplementierung. Ein einziges NuGet-Paket stellt sowohl einen voll funktionsfähigen `MqttClient` als auch einen hochperformanten `MqttServer` (Broker) bereit und deckt alle gängigen Laufzeiten von .NET Framework 4.5 bis .NET 10 ab.

## 📖 Dokumentationsnavigation

| Dokument | Beschreibung |
|------|------|
| [Anforderungen](Doc/需求文档.md) | Projektvision, Kernziele, funktionale Anforderungen der 7 Hauptmodule, zurückgestellte Punkte und Nicht-Ziele |
| [Funktionsliste](Doc/功能清单.md) | Dreidimensionale Verfolgung von Implementierung, Tests und Kommentaren für 87 Funktionspunkte |
| [Architektur](Doc/架构设计.md) | Gesamtarchitektur mit fünf Schichten, Datenmodelle, Schnittstellendesign, Schlüsselentscheidungen und Performance-Design |
| [SRV-Server-Architektur](Doc/SRV-服务端架构.md) | Detailliertes Broker-Design: Nachrichtenrouting, Sicherheitsauthentifizierung, HTTP-Verwaltungs-API |
| [CLUS-Cluster-Architektur](Doc/CLUS-集群架构.md) | Vierstufige Cluster-Roadmap, Raft-Konsens, Topic-Sharding und mehrstufige Topologie |
| [Wettbewerbsanalyse](Doc/竞品分析.md) | Vergleich der MQTT-Landschaft (EMQX/MQTTnet/Mosquitto usw.) und SWOT |
| [CLUS-Cluster-Wettbewerbsanalyse](Doc/CLUS-集群竞品分析.md) | Gezielter Vergleich der Cluster-Fähigkeiten (EMQX/HiveMQ/VerneMQ) |
| [Integrationstests](Doc/集成测试说明.md) | Testarchitektur, Ausführung, Testkategorien und Kreuzkompatibilität (NewLife ↔ MQTTnet, verpflichtend in CI) |
| [AOT-Kompatibilität](Doc/AOT兼容性分析.md) | NativeAOT-Messungen: 19 Hauptpfade bestehen JIT und AOT, RID-Matrix (Windows/Linux × x64/arm64 + Loongson LA64) |
| [Release Notes](Doc/更新日志.md) | Versionsverlauf und Commit-Details |

---

## Kernfunktionen

| Funktion | Beschreibung |
|------|------|
| **Vollständiges Protokoll** | MQTT 3.1 / 3.1.1 / 5.0, 14 grundlegende Pakettypen + AUTH-Paket, 30 MQTT-5.0-Eigenschaften |
| **Keine Abhängigkeiten** | Nur NewLife.Core; keine Drittanbieterpakete in der NuGet-Kette, kein Risiko in der Lieferkette |
| **Client und Server vereint** | Ein einziges NuGet-Paket `NewLife.MQTT`, keine getrennten Client-/Server-Builds |
| **Sieben Transporte** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / zuverlässiges UDP / ProxyProtocol v1/v2 |
| **Hohe Leistung** | Millionen Verbindungen pro Knoten (4 Millionen verifiziert); Broker mit ca. **1,25 Mio. Nachrichten/s** eingehend und 106.000 Nachrichten/s Ende-zu-Ende auf einer Verbindung; Topic-Routing konstant bei 120 ns pro Eintrag |
| **Enterprise-Funktionen kostenlos** | Cluster (Raft / Topic-Sharding) / Bridging / Rule Engine / WebHook / $SYS / ACL, ohne Aufpreis |
| **Automatisches erneutes Abonnieren** | Alle Topics werden nach dem Wiederverbinden automatisch erneut abonniert |
| **SOCKS5-Proxy** | Integrierter SOCKS5-Proxy mit Benutzername/Passwort; TCP und WS können über den Proxy laufen |
| **Volle Versionsabdeckung** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 bis net10 |
| **NativeAOT** | Hauptpfade unter NativeAOT verifiziert (Verwaltungs-API, Cluster-RPC, TCP, WS, Authentifizierung, MQTT-SN; 19 Szenarien unter JIT und AOT, 0 ILC-Warnungen) |
| **Compliance** | Vollständig selbst entwickelt, MIT, keine Copyleft- oder Patentklauseln, keine ausländischen Lieferkettenrisiken |

---

## Alleinstellungsmerkmale

### MQTT über QUIC (selten in der Branche)
Derzeit unterstützen **nur EMQX und NewLife.MQTT** diesen Transport:
- Geringe Latenz, kein Head-of-Line-Blocking, ideal für schwache Netze und mobile Szenarien
- .NET 7/8 benötigen Preview-Funktionen; ab .NET 9 stabil
- Standardport 14567, ALPN-Kennung `"mqtt"`
- Vollständiger Client-/Server-Pfad (Verbinden/Abonnieren/Veröffentlichen/Empfangen) Ende-zu-Ende verifiziert (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT über zuverlässiges UDP (branchenweit erstmals)
**Weltpremiere, ohne msquic-Unterstützung des Betriebssystems, auf allen Plattformen verfügbar**:
- Eigener 4-Byte-Frame-Header + Sequenznummer + ACK-Bestätigung + Timeout-Wiederholung
- Für eingebettete Plattformen wie ARM Linux 4.x ohne msquic

### Automatisches erneutes Abonnieren nach Wiederverbindung
MQTTnet / Eclipse Paho verlangen manuelles erneutes Abonnieren; NewLife.MQTT erledigt das automatisch:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// Nach dem Wiederverbinden ist kein zusätzlicher Code erforderlich.
```

### Enterprise-Funktionen integriert und kostenlos
- **Message Bridging**: In / Out / Both, Topic-Präfix-Mapping, QoS-Obergrenze
- **Rule Engine**: 5 Aktionen (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 Ereignistypen, asynchron mit automatischer Wiederholung; integriert in die MqttHandler- und Exchange-Pipeline
- **Cluster**: Raft-Konsens + konsistentes Hashing für Topic-Sharding + mehrstufiges Routing, knotenübergreifende Abonnementsynchronisierung und Nachrichtenweiterleitung, eigener RPC-Port (2883)
- **MQTT-SN v1.2**: Gateway für eingeschränkte Geräte (Sensoren), interoperabel mit MQTT-Topics
- **Persistente Speicherung**: optionales Paket `NewLife.MQTT.Storage` mit Redis-/Datenbankimplementierung für Offline-Nachrichten und Sitzungen
- **$SYS-Systemtopics**: 14 Standard-Topics mit Echtzeitstatistiken
- **ProxyProtocol v1/v2**: echte Client-IP über nginx/HAProxy erhalten

---

## Schnellstart

### Installation

```bash
dotnet add package NewLife.MQTT
```

### Server starten

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

### Client verbinden

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// Abonnieren (nach Wiederverbindung automatisch erneut abonniert)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Veröffentlichen
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Initialisierung über Verbindungszeichenfolge

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Client im Detail

### Protokollversion festlegen

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (Standard) / V500
};
```

### QoS-Veröffentlichung

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### QoS-Semantik

Die effektive Konsum-QoS ist die **kleinere** aus Veröffentlichungs- und Abonnement-QoS:

| QoS | Name | Beschreibung |
|-----|------|------|
| 0 | Höchstens einmal | Keine Bestätigung; Nachrichten können verloren gehen, geeignet für Sensor-Heartbeats |
| 1 | Mindestens einmal | Der Broker antwortet mit PUBACK; Duplikate möglich, geeignet für wichtige Daten |
| 2 | Genau einmal | Viertstufiger Handshake; kein Verlust, keine Duplikate, geeignet für Abrechnung/Bestellungen |

### Wildcard-Abonnements

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + entspricht einer Ebene
await client.SubscribeAsync("/device/#", handler);        // # entspricht mehreren Ebenen
```

### Geteilte Abonnements (Lastverteilung)

Format `$share/{group}/{topic}`; mehrere Verbraucher derselben Gruppe erhalten Nachrichten reihum:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Empfangen: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> WebSocket-Server: Seit v12 wird er von einem separaten `HttpServer` gehostet (`EnableWebSocket = true`, `WebSocketPort` standardmäßig 0 für Systemzuweisung, `WebSocketPath` standardmäßig `/mqtt`) und teilt nicht mehr den MQTT-TCP-Port; Clients verbinden weiterhin über `ws://host:port/mqtt`. `WebSocketCertificate` aktiviert TLS auf diesem Port, also `wss://host:port/mqtt`.

---

## Server im Detail

### Benutzerdefinierter Handler

Von `MqttHandler` erben, um alle Nachrichten abzufangen; ideal für IoT-Plattformen, die Daten direkt speichern:

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

### Benutzerdefinierte Authentifizierung (ACL)

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

## Enterprise-Funktionen

### Nachrichten-Bridging

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

### Regel-Engine

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

### Cluster-Bereitstellung

Dreiknoten-Cluster (jeder Knoten listet die Adressen der anderen):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Cluster-Ablauf: Join beim Start → Ping alle 15 Sekunden → Lease beim Herunterfahren; Zeitüberschreitungen (5 Minuten) werden automatisch entfernt.

> Seit v12 nutzt Cluster-RPC HTTP (`ClusterPort` ist ein HTTP-Port; Adressen als `host:port` oder `http://host:port`), und `ClusterSecret` wird bei jedem Aufruf als Token geprüft; NewLife.Remoting ist nicht mehr erforderlich.

### Alibaba-Cloud-IoT-Adapter

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

## Schnell ausprobieren

Öffnen Sie den Quellcode und legen Sie `Test` als Startprojekt fest, um Server und Client gleichzeitig auszuführen:

![Demo](Doc/Demo.png)

---

## Zentrale Highlights

| Highlight | Beschreibung |
|------|------|
| 🚀 **Vollständiges Protokoll** | Vollständige MQTT-3.1.1-Unterstützung (14 Pakettypen) + vollständige MQTT-5.0-Kernfunktionen (30 Eigenschaften) |
| 🔒 **Keine Abhängigkeiten** | Nur die Basisbibliothek NewLife.Core; kein Lieferkettenrisiko, keine Lizenzkonflikte |
| ⚖️ **MIT-Open-Source** | Freizügigste Lizenz; freie kommerzielle Nutzung und Änderung, kein Copyleft, keine Patentklauseln |
| 🎯 **Client + Server vereint** | Ein NuGet-Paket `NewLife.MQTT` für Client und Server; minimale Integration |
| 🌐 **Sieben Transporte** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / zuverlässiges UDP / ProxyProtocol v1/v2 |
| 🏢 **Kostenlose Enterprise-Funktionen** | Cluster / Bridging / Rule Engine / WebHook / Statistik / $SYS; Wettbewerber verlangen Enterprise-Editionen |
| 🇨🇳 **Vollständig selbst entwickelt** | Vollständiges geistiges Eigentum, keine Rechtsrisiken durch externe Abhängigkeiten, compliance-ready |
| 🔧 **Volle Versionsabdeckung** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Weitere Vorteile

### 1. MQTT über QUIC (selten in der Branche)
- **Nur EMQX und NewLife.MQTT unterstützen dies**
- Geringe Latenz, kein Head-of-Line-Blocking
- Verfügbar ab .NET 7+ (Preview unter .NET 7/8, stabil ab .NET 9+)

### 2. MQTT über zuverlässiges UDP (eigenentwickelt)
- **Branchenweit einzigartig**
- 4-Byte-Frame-Header + Sequenznummer + Ack + Timeout-Wiederholung
- Keine QUIC-/msquic-Unterstützung des Betriebssystems erforderlich; auf allen Plattformen verfügbar

### 3. Enterprise-Funktionen integriert und kostenlos
- **Message Bridging (bidirektional):** In/Out/Both, Topic-Präfix-Mapping, QoS-Obergrenze
- **Rule Engine (5 Aktionen):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook-Ereignis-Push:** 6 Ereignistypen, asynchron und nicht blockierend, mit Wiederholung
- **Cluster:** Manuelle Knotenkonfiguration, knotenübergreifende Abonnementsynchronisierung und Nachrichtenweiterleitung
- **ProxyProtocol:** nginx/HAProxy v1/v2 zur Erhaltung der echten Client-IP
- **$SYS-Systemtopics:** 14 Standard-Topics mit Echtzeitstatistiken

### 4. Automatisches erneutes Abonnieren nach Wiederverbindung
Wettbewerber (MQTTnet / Eclipse Paho) verlangen manuelles erneutes Abonnieren; NewLife.MQTT erledigt es automatisch.

### 5. Integrierter Alibaba-Cloud-IoT-Adapter
`AliyunMqttClient` funktioniert sofort: HMAC-Signatur, Property-Reporting und Zeitsynchronisierung.

## MQTT-Protokoll
MQTT ist das beliebteste IoT-Kommunikationsprotokoll und umfasst Client, Server und Web-Verwaltungsplattform.

Es bietet ein Publish/Subscribe-Modell, das einfacher, leichter und benutzerfreundlicher ist und auf eingeschränkte Umgebungen abzielt (geringe Bandbreite, hohe Latenz, instabile Netze). Kurz gesagt: für das Internet der Dinge gemacht. Die offiziellen Merkmale:
1. Publish/Subscribe-Muster mit 1:n-Verteilung und Entkopplung der Anwendungen.
2. Transport unabhängig vom Payload-Inhalt.
3. TCP/IP als Netzwerkgrundlage.
4. Drei QoS-Stufen:
"At most once": Veröffentlichung hängt vollständig vom TCP/IP-Netz ab; Verlust oder Duplikate möglich. Geeignet für Umgebungssensoren, bei denen ein verlorener Wert bald ersetzt wird.
"At least once": Zustellung garantiert, Duplikate möglich.
"Exactly once": Genau eine Zustellung; geeignet für Abrechnungssysteme, bei denen Duplikate oder Verluste falsche Ergebnisse verursachen.
5. Kleiner Transport mit sehr geringem Overhead (fester Header 2 Byte) und minimalem Protokollaustausch.
6. Last Will and Testament benachrichtigt Interessierte bei abnormaler Trennung.

## MQTT-Veröffentlichung und -Abonnement
Beim Veröffentlichen wird die QoS festgelegt und mit der Nachricht im Broker gespeichert; beim Abonnieren ist die Konsum-QoS das Minimum aus Veröffentlichungs- und Abonnement-QoS.

Details:
- Abonnement-QoS=0: Konsum immer QoS 0, unabhängig von der Veröffentlichung;
- Abonnement-QoS=1: Veröffentlichung QoS=0 ergibt Konsum QoS=0; Veröffentlichung QoS=1 oder 2 ergibt Konsum QoS=1;
- Abonnement-QoS=2: Konsum-QoS entspricht der Veröffentlichungs-QoS;
- Veröffentlichung QoS=0: keine Antwort des Brokers; theoretisch unbemerkter Verlust, aber TCP erkennt Netzfehler;
- Veröffentlichung QoS=1: Broker antwortet mit `PubAck`;
- Veröffentlichung QoS=2: Broker antwortet mit `PubRec`, Client sendet `PubRel`, Broker antwortet mit `PubComp`, erst dann gilt die Nachricht als veröffentlicht;
- Abonnement-QoS=2: Broker pusht QoS=2, Client antwortet `PubRec`, Broker sendet `PubRel`, Client antwortet `PubComp`, erst dann gilt die Nachricht als konsumiert;
- Bei QoS=2 muss der Verbraucher nicht online sein; der Ablauf findet zwischen Publisher und Broker statt.

## Schneller Einstieg
Öffnen Sie die Lösung, legen Sie Test als Startprojekt fest und starten Sie sie. Standardmäßig starten TestServer und TestClient nacheinander.

![Demo](Doc/Demo.png)

## Server
Referenzieren Sie `NewLife.MQTT` aus NuGet und starten Sie den Server:

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

Mit Port 1883, dem Standardhandler `MqttHandler` und dem Standard-Exchange `MqttExchange` startet der Server.

## Client
Referenzieren Sie `NewLife.MQTT` aus NuGet und verbinden Sie sich:

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

// Topic "/test" abonnieren
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Alle 2 Sekunden eine Nachricht an "/test" veröffentlichen
while (true)
{
    try
    {
        var msg = "Lernen kennt keine feste Reihenfolge; wer zuerst ankommt, ist der Lehrer" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

Ein Client benötigt `Serveradresse`, `Benutzername`, `Passwort` und `Client-ID`; dann verbindet `ConnectAsync`. Ein Client kann als Verbraucher mit `SubscribeAsync` abonnieren oder als Produzent mit `PublishAsync` veröffentlichen.

## Geteilte Abonnements (EMQX)
Unterstützt EMQX-Shared-Subscriptions: Mehrere Clients abonnieren dasselbe Shared-Topic, der Broker verteilt die Nachrichten lastverteilt. Format: `$share/{group}/{topic}`.

```csharp
// Shared Topic abonnieren; mehrere Clients derselben Gruppe erhalten Nachrichten lastverteilt
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Shared-Subscription-Nachricht: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Auf das eigentliche Topic veröffentlichen (ohne $share-Präfix)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Vorteile:
- **Lastverteilung**: gleichmäßige Verteilung innerhalb der Gruppe
- **Horizontale Skalierung**: mehr Kapazität durch weitere Abonnenten
- **Hochverfügbarkeit**: bei Ausfall eines Abonnenten übernehmen die anderen

### MQTT-Protokollversion festlegen
Ab Version 2.3.2025.0212 kann die Protokollversion über `Version` gewählt werden:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

Unterstützte Versionen:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (Standard)
- `MqttVersion.V500` - MQTT 5.0

**Hinweis**: Ohne explizite Angabe wird MQTT 3.1.1 verwendet.

## Benutzerdefinierter Server
Für serverseitige Verbindungs- und Nachrichtenlogik ist ein benutzerdefinierter Server erforderlich. Eine IoT-Plattform kann Gerätedaten beispielsweise direkt speichern, ohne sie erneut zu konsumieren.

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

Registrierung des benutzerdefinierten Handlers:

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

## Cluster
Cluster-Verwaltung: Join, Ping, Lease.
Join tritt dem Cluster bei und meldet den Knoten; beim Start N-1 Mal aufgerufen.
Ping aktualisiert alle 15 Sekunden die Aktivzeit aller Knoten.
Lease verlässt den Cluster; N-1 Mal aufgerufen.
Jeder Knoten entfernt regelmäßig Knoten, deren Aktivzeit abgelaufen ist.

## Ökosystem-Karte

NewLife ist ein geschichtetes Ökosystem unabhängig versionierter Open-Source-Projekte. Die folgende Karte wird wortgleich in allen NewLife-Repositories verwendet; jedes Projekt verlinkt auf sein Repository und sein primäres NuGet-Paket.

| Projekt | Funktion | Downloads |
| --- | --- | --- |
| **Grundlage** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Protokollierung, Konfiguration, Caching, Netzwerk, Serialisierung, Zeitplanung und APM-Tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Daten-Middleware: einzelne Tabellen in der Größenordnung von zig Milliarden Zeilen, automatisches Sharding, Lese-/Schreibtrennung, über 10 Datenbanken. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Hochperformante Netzwerkbibliothek: 1,77 Mio. pps / 14,5 Gbps auf einer Maschine (TCP 1KB, 2026-10), 4 Mio. langlebige TCP-Verbindungen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protokollkommunikation und RPC-Framework für Client/Server-Anwendungen und IoT-Geräte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installiert .NET-Anwendungen als Betriebssystem-Dienste: Windows-Dienste und Linux-systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new`-Projektvorlagen für Konsole, Web, API, Dienst, Daten und IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Daten & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis-Client mit Mikrosekunden-Latenz, Millionen Operationen pro Sekunde und integrierten Nachrichtenwarteschlangen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Vollständig verwalteter RocketMQ-Client für Apache RocketMQ und Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT-Client und -Server, einschließlich Alibaba Cloud IoT-Anbindung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybriddatenbank in C#: eingebettet oder im Netzwerk, relational, Zeitreihen, Nachrichtenwarteschlange und KV in einem. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql-Treiber und Hilfsschicht auf Basis des NewLife-Datenstacks. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, KI & verteilt** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Schnelles Framework für Administrationsplattformen: Berechtigungen, SSO, OAuth-Server und CRUD-Gerüst. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Verteilte Dienstplattform: Knotenverwaltung, APM, Konfigurationszentrum, Registry und Release-Center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plattform für verteilte Aufgaben und Echtzeit-Datenplanung mit Sharding, Wiederholungen und Backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Einheitlicher Client für 46 LLM-Anbieter plus ChatAI, eine sofort einsatzbereite Web-Chat-Anwendung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrie & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT-Standardbibliothek mit Protokollverträgen und Geräteabstraktionen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus-TCP/RTU/ASCII-Implementierung auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi-Melsec-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC-Client für industriellen Datenzugriff. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet-Protokollstack für die Gebäudeautomation. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Leichtgewichtige Open-Source-IoT-Plattform mit Gateway- und Geräteverwaltung. | — |
| **Fahrzeugtelematik** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T-808-Fahrzeugortungsprotokoll: über 70 Nachrichtenkörper, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T-809-Plattform-zu-Plattform-Protokoll für Verkehrsbehörden. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T-1078-Fahrzeugvideo: RTP-Parsing, FLV/fMP4-Remuxing und Live-Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domänenkomponenten** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Ein Paket für Excel, Word, PPT, PDF und über 15 Dokumentformate. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Plattformübergreifendes Audio: Codecs, Geräte-I/O, DSP, Container und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Videocodecs, Containerformate, Frame-Verarbeitung und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Einheitliche Schnittstelle für Baidu, AMap, Tencent und Tianditu mit Geokodierung und Koordinatenumrechnung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4-Geolokalisierung mit einer offline speichergemappten Datenbank und einer Online-API als Fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Werkzeuge & Produkte** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Entwickler-Toolbox: Netzwerk, serielle Schnittstelle, Krypto, Regex, Modbus und MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Selbst gehostetes Git-Quellcode-Verwaltungssystem. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installiert automatisch die passende .NET-Laufzeit, wenn es von einem älteren Host gestartet wird. | — |

> Die Badges zeigen das primäre Paket jedes Projekts; Theme-, Erweiterungs- und Satellitenpakete sind auf den jeweiligen NuGet-Seiten aufgeführt.Die hier nicht aufgeführten über 30 Pakete (FTP, LoRa, Office-Erweiterungen, Protokolltreiber und mehr) sind in der [NewLifeX-Organisation](https://github.com/NewLifeX) und auf [newlifex.com](https://newlifex.com) verfügbar.

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife wurde 2002 gegründet und widmet sich seit mehr als zwei Jahrzehnten dem .NET- und IoT-Ökosystem, betreut fast 100 Open-Source-Projekte (34 davon Hauptprojekte) und liefert integrierte Hard- und Softwarelösungen für die Bereiche Energie, Logistik, Gesundheitswesen, Industrieautomation und Internet. Unser Leitspruch lautet „Lernen kennt kein Dienstalter – wer es beherrscht, wird zum Lehrmeister“.

Die Open-Source-Komponenten wurden <!--nuget-total:start-->6.48M<!--nuget-total:end--> Mal von NuGet heruntergeladen (ökosystemweit, Stand 2026-10).

Website: https://newlifex.com · Open-Source-Organisation: https://github.com/NewLifeX

<details>
<summary>WeChat-Offizieller Account: 智能大石头 (zum Folgen scannen)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Lizenz

MIT License © 2002-2026 NewLife-Team

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
