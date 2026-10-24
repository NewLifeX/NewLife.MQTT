# NewLife.MQTT - Implémentation complète du protocole MQTT

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Cette traduction peut être en retard sur la version [English README](README.md).

**NewLife.MQTT** est une implémentation complète du protocole MQTT, **entièrement auto-développée, sans dépendance tierce et sous licence MIT**. Un seul package NuGet fournit à la fois un `MqttClient` complet et un `MqttServer` (broker) hautes performances, couvrant tous les runtimes principaux de .NET Framework 4.5 à .NET 10.

## 📖 Navigation de la documentation

| Document | Description |
|------|------|
| [Exigences](Doc/需求文档.md) | Vision, objectifs, exigences des 7 modules principaux, éléments reportés et non-objectifs |
| [Liste des fonctionnalités](Doc/功能清单.md) | Suivi tridimensionnel implémentation/tests/commentaires de 87 points fonctionnels |
| [Architecture](Doc/架构设计.md) | Architecture en cinq couches, modèles de données, interfaces, décisions clés et performance |
| [Architecture du serveur](Doc/SRV-服务端架构.md) | Conception du broker : routage, authentification et API HTTP d'administration |
| [Architecture du cluster](Doc/CLUS-集群架构.md) | Feuille de route cluster en quatre étapes, consensus Raft, partitionnement de sujets et topologie multi-niveaux |
| [Analyse concurrentielle](Doc/竞品分析.md) | Comparaison de l'écosystème MQTT (EMQX/MQTTnet/Mosquitto) et SWOT |
| [Analyse concurrentielle du cluster](Doc/CLUS-集群竞品分析.md) | Comparaison ciblée des capacités cluster (EMQX/HiveMQ/VerneMQ) |
| [Tests d'intégration](Doc/集成测试说明.md) | Architecture de test, exécution, catégories et compatibilité croisée (NewLife ↔ MQTTnet, obligatoire en CI) |
| [Compatibilité AOT](Doc/AOT兼容性分析.md) | Mesures NativeAOT : 19 scénarios principaux en JIT et AOT, matrice RID |
| [Notes de version](Doc/更新日志.md) | Historique des versions et détails des commits |

---

## Fonctionnalités principales

| Fonction | Description |
|------|------|
| **Protocole complet** | MQTT 3.1 / 3.1.1 / 5.0, 14 types de paquets + paquet AUTH, 30 propriétés MQTT 5.0 |
| **Aucune dépendance** | Uniquement NewLife.Core ; aucun package tiers dans la chaîne NuGet |
| **Client et serveur unifiés** | Un seul package NuGet `NewLife.MQTT`, pas de builds séparés |
| **Sept transports** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP fiable / ProxyProtocol v1/v2 |
| **Hautes performances** | Millions de connexions par nœud (4 millions vérifiées) ; broker à ~**1,25 M messages/s** en entrée et 106 000 msg/s de bout en bout ; routage constant à 120 ns par entrée |
| **Fonctions entreprise gratuites** | Cluster (Raft / partitionnement) / pont / moteur de règles / WebHook / $SYS / ACL |
| **Réabonnement automatique** | Tous les sujets sont réabonnés après reconnexion, sans code supplémentaire |
| **Proxy SOCKS5** | Proxy SOCKS5 intégré avec authentification, pour TCP et WS |
| **Couverture des versions** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 à net10 |
| **NativeAOT** | Chemins principaux vérifiés sous NativeAOT (API, RPC cluster, TCP, WS, authentification, MQTT-SN ; 19 scénarios, 0 avertissement ILC) |
| **Localisation en 31 langues** | Les messages d'exécution, le tableau de bord et les métadonnées du paquet sont localisés en 31 langues via les tables compilées de NewLife.Core ; les entrées manquantes reviennent à l'anglais |
| **Bus d'événements** | `MqttEventBus` fournit une publication/souscription fortement typée sur les thèmes MQTT : diffusion par défaut, abonnement partagé `$share` en option, et compatible avec les contrats `IEventBusFactory` de NewLife.Core |
| **Conformité** | Auto-développé, MIT, sans copyleft ni clause de brevet |

---

## Avantages différenciants

### MQTT sur QUIC (rare dans l'industrie)
Actuellement, **seuls EMQX et NewLife.MQTT** prennent en charge ce transport :
- Faible latence, pas de blocage en tête de ligne, idéal pour les réseaux faibles et mobiles
- .NET 7/8 nécessitent les fonctionnalités preview ; stable depuis .NET 9
- Port par défaut 14567, identifiant ALPN `"mqtt"`
- Chemin client/serveur complet vérifié de bout en bout (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT sur UDP fiable (première de l'industrie)
**Première mondiale, sans msquic du système, disponible sur toutes les plateformes** :
- En-tête de trame de 4 octets + numéro de séquence + ACK + retransmission sur timeout
- Couvre les plateformes embarquées comme ARM Linux 4.x sans msquic

### Réabonnement automatique après reconnexion
MQTTnet / Eclipse Paho exigent un réabonnement manuel ; NewLife.MQTT le fait automatiquement :

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// Aucun code supplémentaire après la reconnexion.
```

### Fonctions entreprise intégrées et gratuites
- **Pont de messages** : In / Out / Both, mappage de préfixes de sujet, plafond QoS
- **Moteur de règles** : 5 actions (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook** : 6 types d'événements asynchrones avec nouvelle tentative automatique ; intégré à MqttHandler + Exchange
- **Cluster** : consensus Raft + partitionnement par hachage cohérent + routage multi-niveaux, synchronisation des abonnements et transfert entre nœuds, port RPC dédié (2883)
- **MQTT-SN v1.2** : passerelle pour dispositifs contraints, interopérable avec les sujets MQTT
- **Stockage persistant** : package optionnel `NewLife.MQTT.Storage` avec Redis / base de données pour messages hors ligne et sessions
- **Sujets $SYS** : 14 sujets standard avec statistiques en temps réel
- **ProxyProtocol v1/v2** : conserve l'IP réelle du client via nginx/HAProxy

---

## Démarrage rapide

### Installation

```bash
dotnet add package NewLife.MQTT
```

### Démarrer le serveur

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

### Connecter un client

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// S'abonner (réabonnement automatique après reconnexion)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Publier
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Initialisation par chaîne de connexion

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Utilisation détaillée du client

### Spécifier la version du protocole

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (par défaut) / V500
};
```

### Publication QoS

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### Sémantique QoS

La QoS effective de consommation est la **plus petite** entre la QoS de publication et celle d'abonnement :

| QoS | Nom | Description |
|-----|------|------|
| 0 | Au plus une fois | Aucune confirmation ; pertes possibles, adapté aux battements de capteurs |
| 1 | Au moins une fois | Le broker répond PUBACK ; doublons possibles, adapté aux données importantes |
| 2 | Exactement une fois | Poignée de main en quatre étapes ; sans perte ni doublon, adapté à la facturation/commandes |

### Abonnements avec jokers

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + correspond à un niveau
await client.SubscribeAsync("/device/#", handler);        // # correspond à plusieurs niveaux
```

### Abonnements partagés (répartition de charge)

Format `$share/{group}/{topic}` ; plusieurs consommateurs du même groupe reçoivent les messages en tourniquet :

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Reçu : {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> Serveur WebSocket : depuis v12, il est hébergé par un `HttpServer` séparé (`EnableWebSocket = true`, `WebSocketPort` à 0 pour une attribution système, `WebSocketPath` à `/mqtt`) et ne partage plus le port TCP MQTT ; les clients se connectent toujours via `ws://host:port/mqtt`. `WebSocketCertificate` active TLS sur ce port, soit `wss://host:port/mqtt`.

---

## Utilisation détaillée du serveur

### Gestionnaire personnalisé

Héritez de `MqttHandler` pour intercepter tous les messages, idéal pour les plateformes IoT qui stockent directement les données :

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

### Authentification personnalisée (ACL)

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

## Fonctions entreprise

### Pont de messages

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

### Moteur de règles

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

### Déploiement cluster

Cluster à trois nœuds (chaque nœud liste les adresses des autres) :

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Fonctionnement : Join au démarrage → Ping toutes les 15 secondes → Lease à l'arrêt ; les nœuds expirés (5 minutes) sont supprimés automatiquement.

> Depuis v12, le RPC cluster utilise HTTP (`ClusterPort` est un port HTTP ; les adresses peuvent être `host:port` ou `http://host:port`), et `ClusterSecret` est vérifié comme jeton à chaque appel ; NewLife.Remoting n'est plus requis.

### Adaptateur Alibaba Cloud IoT

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

## Essayez rapidement

Ouvrez le code source et définissez `Test` comme projet de démarrage pour lancer le serveur et le client simultanément :

![Demo](Doc/Demo.png)

---

## Points clés

| Point | Description |
|------|------|
| 🚀 **Protocole complet** | Prise en charge complète de MQTT 3.1.1 (14 paquets) + fonctions clés de MQTT 5.0 (30 propriétés) |
| 🔒 **Aucune dépendance** | Uniquement NewLife.Core ; aucun risque de chaîne d'approvisionnement ni conflit de licence |
| ⚖️ **MIT** | Licence permissive ; modification et usage commercial libres, sans copyleft ni brevet |
| 🎯 **Client + serveur** | Un package NuGet `NewLife.MQTT` pour les deux ; intégration minimale |
| 🌐 **Sept transports** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP fiable / ProxyProtocol v1/v2 |
| 🏢 **Fonctions entreprise gratuites** | Cluster / pont / moteur de règles / WebHook / statistiques / $SYS |
| 🇨🇳 **Auto-développé** | Propriété intellectuelle complète, sans risque juridique externe, prêt pour la conformité |
| 🔧 **Couverture des versions** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Avantages supplémentaires

### 1. MQTT sur QUIC (rare)
- **Seuls EMQX et NewLife.MQTT le prennent en charge**
- Faible latence, pas de blocage en tête de ligne
- Disponible sur .NET 7+ (preview sur 7/8, stable sur 9+)

### 2. MQTT sur UDP fiable (auto-développé)
- **Unique dans l'industrie**
- En-tête de 4 octets + séquence + Ack + retransmission sur timeout
- Aucun support QUIC/msquic du système requis ; fonctionne partout

### 3. Fonctions entreprise intégrées et gratuites
- **Pont bidirectionnel :** In/Out/Both, préfixes de sujet, plafond QoS
- **Moteur de règles (5 actions) :** Republish / WebHook / Bridge / Drop / Custom
- **WebHook :** 6 types d'événements, asynchrone et non bloquant, avec nouvelles tentatives
- **Cluster :** configuration manuelle des nœuds, synchronisation des abonnements et transfert
- **ProxyProtocol :** nginx/HAProxy v1/v2 pour conserver l'IP réelle
- **Sujets $SYS :** 14 sujets standard avec statistiques en temps réel

### 4. Réabonnement automatique après reconnexion
Les concurrents (MQTTnet / Eclipse Paho) exigent un réabonnement manuel ; NewLife.MQTT le fait automatiquement.

### 5. Adaptateur Alibaba Cloud IoT intégré
`AliyunMqttClient` fonctionne immédiatement : signature HMAC, rapport de propriétés et synchronisation d'horloge.

## Protocole MQTT
MQTT est le protocole de communication IoT le plus populaire, avec client, serveur et plateforme web d'administration.

Il fournit un modèle publication/abonnement plus simple, plus léger et plus facile à utiliser, adapté aux environnements contraints (faible bande passante, latence élevée, réseau instable). En résumé, il est conçu pour l'Internet des objets. Ses caractéristiques officielles :
1. Modèle publication/abonnement avec distribution 1:n et découplage des applications.
2. Transport indépendant du contenu du payload.
3. Connectivité réseau TCP/IP.
4. Trois niveaux de QoS :
« Au plus une fois » : dépend entièrement de TCP/IP ; pertes ou doublons possibles. Adapté aux capteurs environnementaux.
« Au moins une fois » : livraison garantie, doublons possibles.
« Exactement une fois » : livraison unique ; adapté à la facturation, où doublons ou pertes fausseraient les résultats.
5. Transport compact avec très peu de surcharge (en-tête fixe de 2 octets) et échanges minimaux.
6. Last Will and Testament prévient les intéressés en cas de déconnexion anormale.

## Publication et abonnement MQTT
À la publication, la QoS est spécifiée et stockée par le broker ; à l'abonnement, la QoS de consommation est la plus petite des deux.

Détails :
- Abonnement QoS=0 : consommation toujours QoS 0 ;
- Abonnement QoS=1 : publication QoS=0 → consommation QoS=0 ; publication QoS=1 ou 2 → consommation QoS=1 ;
- Abonnement QoS=2 : la consommation correspond à la QoS publiée ;
- Publication QoS=0 : le broker ne répond pas ; une perte intermédiaire passerait inaperçue, mais TCP détecte les pannes ;
- Publication QoS=1 : le broker répond `PubAck` ;
- Publication QoS=2 : le broker répond `PubRec`, le client envoie `PubRel`, le broker répond `PubComp` ; le message est alors publié ;
- Abonnement QoS=2 : le broker envoie QoS=2, le client répond `PubRec`, le broker envoie `PubRel`, le client répond `PubComp` ; le message est alors consommé ;
- En QoS=2, le consommateur n'a pas besoin d'être en ligne ; le flux se déroule entre l'éditeur et le broker.

## Essai rapide
Ouvrez la solution, définissez Test comme projet de démarrage et lancez-la. Par défaut, TestServer et TestClient démarrent l'un après l'autre.

![Demo](Doc/Demo.png)

## Serveur
Référencez `NewLife.MQTT` depuis NuGet et démarrez le serveur :

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

Avec le port 1883, le gestionnaire `MqttHandler` et l'échange `MqttExchange`, le serveur démarre.

## Client
Référencez `NewLife.MQTT` depuis NuGet et connectez-vous :

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

// S'abonner au sujet "/test"
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Publier toutes les 2 secondes sur "/test"
while (true)
{
    try
    {
        var msg = "L'apprentissage n'a pas d'ordre fixe ; le premier arrivé est le maître" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

Un client a besoin de `l'adresse du serveur`, `l'utilisateur`, `le mot de passe` et `l'identifiant client` ; puis `ConnectAsync`. Il peut consommer via `SubscribeAsync` ou produire via `PublishAsync`.

## Abonnements partagés (EMQX)
Prend en charge les abonnements partagés EMQX : plusieurs clients s'abonnent au même sujet partagé et le broker répartit la charge. Format : `$share/{group}/{topic}`.

```csharp
// S'abonner à un sujet partagé ; les clients du groupe reçoivent en répartition de charge
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Message partagé : {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Publier sur le sujet réel (sans le préfixe $share)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Avantages :
- **Répartition de charge** : distribution uniforme dans le groupe
- **Montée en charge horizontale** : plus d'abonnés, plus de capacité
- **Haute disponibilité** : si un abonné tombe, les autres continuent

### Spécifier la version du protocole MQTT
Depuis la version 2.3.2025.0212, le client permet de choisir MQTT 3.1, 3.1.1 ou 5.0 via la propriété `Version` :

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

Versions prises en charge :
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (par défaut)
- `MqttVersion.V500` - MQTT 5.0

**Remarque** : sans valeur explicite, MQTT 3.1.1 est utilisé pour la compatibilité.

## Serveur personnalisé
Lorsqu'il faut gérer les connexions et les messages côté serveur, un serveur personnalisé est nécessaire. Par exemple, une plateforme IoT peut stocker directement les données rapportées.

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

Modifiez l'enregistrement du gestionnaire :

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
Gestion du cluster : Join, Ping, Lease.
Join rejoint le cluster et annonce le nœud ; appelé N-1 fois au démarrage.
Ping met à jour le temps actif de tous les nœuds toutes les 15 secondes.
Lease quitte le cluster ; appelé N-1 fois.
Chaque nœud supprime périodiquement les nœuds dont le temps actif a expiré.

## Carte de l'écosystème

NewLife est un écosystème en couches de projets open source versionnés indépendamment. La carte ci-dessous est reprise à l'identique par tous les dépôts NewLife ; chaque projet renvoie à son dépôt et à son paquet NuGet principal.

| Projet | Rôle | Téléchargements |
| --- | --- | --- |
| **Fondation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Journalisation, configuration, cache, réseau, sérialisation, ordonnancement et traçage APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Intergiciel de données : tables uniques à l'échelle de dizaines de milliards, partitionnement automatique, séparation lecture/écriture et plus de 10 bases de données. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Bibliothèque réseau hautes performances : 1,77 M pps / 14,5 Gbps sur une machine (TCP 1KB, 2026-10), 4 M connexions TCP persistantes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Communication protocolaire et framework RPC pour applications client/serveur et appareils IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installe des applications .NET comme démons système : services Windows et systemd Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modèles de projet `dotnet new` pour console, web, API, service, données et IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Données et messagerie** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Client Redis avec une latence en microsecondes, des millions d'opérations/s et des files de messages intégrées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Client RocketMQ entièrement managé pour Apache RocketMQ et Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Client et serveur MQTT, y compris la connectivité Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de données hybride en C# : embarquée ou en réseau, relationnelle, séries temporelles, file de messages et KV réunis. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Pilote et couche d'assistance MySql construits sur la pile de données NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA et distribué** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rapide de plateforme d'administration : permissions, SSO, serveur OAuth et échafaudage CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plateforme de services distribués : gestion des nœuds, APM, centre de configuration, registre et centre de publication. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plateforme de tâches distribuées et d'ordonnancement de données en temps réel avec partitionnement, reprises et backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Client unifié pour 46 fournisseurs de LLM, plus ChatAI, une application web de chat prête à l'emploi. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industriel et IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Bibliothèque standard IoT définissant les contrats de protocole et les abstractions d'appareils. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implémentation Modbus TCP/RTU/ASCII au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocole pour automate Siemens au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocole pour automate Omron au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocole pour automate Mitsubishi Melsec au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Client OPC pour l'accès aux données industrielles. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pile de protocoles BACnet pour l'automatisation des bâtiments. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plateforme IoT open source légère avec gestion des passerelles et des appareils. | — |
| **Télématique embarquée** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocole de positionnement de véhicules JT/T 808 : plus de 70 corps de message, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocole plateforme à plateforme JT/T 809 pour les autorités de transport. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vidéo embarquée JT/T 1078 : analyse RTP, remuxage FLV/fMP4 et diffusion en direct. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Composants métier** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un seul paquet pour Excel, Word, PPT, PDF et plus de 15 formats de documents. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplateforme : codecs, E/S d'appareils, DSP, conteneurs et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs vidéo, formats de conteneurs, traitement des images et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unifiée pour Baidu, AMap, Tencent et Tianditu avec géocodage et conversion de coordonnées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Géolocalisation IPv4 avec une base de données hors ligne mappée en mémoire et une API en ligne en secours. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Outils et produits** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Boîte à outils du développeur : réseau, port série, cryptographie, regex, Modbus et MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Système de gestion de code source Git auto-hébergé. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installe automatiquement le runtime .NET correspondant lorsqu'il est lancé depuis un hôte plus ancien. | — |

> Les badges indiquent le paquet principal de chaque projet ; les paquets de thèmes, d'extensions et satellites sont répertoriés sur leurs pages NuGet.Les plus de 30 paquets non listés ici (FTP, LoRa, extensions Office, pilotes de protocoles, etc.) sont disponibles dans l'[organisation NewLifeX](https://github.com/NewLifeX) et sur [newlifex.com](https://newlifex.com).

## Équipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife a été fondée en 2002 et œuvre depuis plus de deux décennies dans l'écosystème .NET et IoT, maintient près de 100 projets open source (dont 34 principaux) et fournit des solutions matérielles et logicielles intégrées aux secteurs de l'énergie, de la logistique, de la santé, du contrôle industriel et d'Internet. Notre devise : « l'apprentissage ne connaît pas l'ancienneté — celui qui maîtrise devient maître ».

Les composants open source ont été téléchargés <!--nuget-total:start-->6.48M<!--nuget-total:end--> fois depuis NuGet (à l'échelle de l'écosystème, en date de 2026-10).

Site web : https://newlifex.com · Organisation open source : https://github.com/NewLifeX

<details>
<summary>Compte officiel WeChat : 智能大石头 (scannez pour suivre)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licence

MIT License © 2002-2026 Équipe NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
