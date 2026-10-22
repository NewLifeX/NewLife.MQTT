# NewLife.MQTT - Implementación completa del protocolo MQTT

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Esta traducción puede estar desactualizada; consulte el [English README](README.md).

**NewLife.MQTT** es una implementación completa del protocolo MQTT, **totalmente autodesarrollada, sin dependencias de terceros y con licencia MIT**. Un único paquete NuGet ofrece tanto un `MqttClient` completo como un `MqttServer` (broker) de alto rendimiento, cubriendo todos los runtime principales desde .NET Framework 4.5 hasta .NET 10.

## 📖 Navegación de documentación

| Documento | Descripción |
|------|------|
| [Requisitos](Doc/需求文档.md) | Visión, objetivos, requisitos de los 7 módulos principales, elementos aplazados y no objetivos |
| [Lista de funciones](Doc/功能清单.md) | Seguimiento tridimensional de implementación, pruebas y comentarios de 87 puntos funcionales |
| [Arquitectura](Doc/架构设计.md) | Arquitectura de cinco capas, modelos de datos, interfaces, decisiones clave y diseño de rendimiento |
| [Arquitectura del servidor](Doc/SRV-服务端架构.md) | Diseño del broker: enrutamiento, autenticación y API HTTP de administración |
| [Arquitectura de clúster](Doc/CLUS-集群架构.md) | Hoja de ruta de clúster en cuatro fases, consenso Raft, particionado de temas y topología multicapa |
| [Análisis competitivo](Doc/竞品分析.md) | Comparación del ecosistema MQTT (EMQX/MQTTnet/Mosquitto) y SWOT |
| [Análisis competitivo de clúster](Doc/CLUS-集群竞品分析.md) | Comparación específica de capacidades de clúster (EMQX/HiveMQ/VerneMQ) |
| [Pruebas de integración](Doc/集成测试说明.md) | Arquitectura de pruebas, ejecución, categorías y compatibilidad cruzada (NewLife ↔ MQTTnet, obligatoria en CI) |
| [Compatibilidad AOT](Doc/AOT兼容性分析.md) | Mediciones NativeAOT: 19 escenarios principales en JIT y AOT, matriz de RID |
| [Notas de versión](Doc/更新日志.md) | Historial de versiones y detalles de commits |

---

## Funciones principales

| Función | Descripción |
|------|------|
| **Protocolo completo** | MQTT 3.1 / 3.1.1 / 5.0, 14 tipos de paquete + paquete AUTH, 30 propiedades de MQTT 5.0 |
| **Sin dependencias** | Solo NewLife.Core; sin paquetes de terceros en la cadena NuGet |
| **Cliente y servidor unificados** | Un único paquete NuGet `NewLife.MQTT`, sin compilaciones separadas |
| **Siete transportes** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP fiable / ProxyProtocol v1/v2 |
| **Alto rendimiento** | Millones de conexiones por nodo (4 millones verificadas); broker con ~**1,25 M mensajes/s** de entrada y 106.000 msg/s extremo a extremo; enrutamiento de temas constante a 120 ns por entrada |
| **Funciones empresariales gratis** | Clúster (Raft / particionado) / puente / motor de reglas / WebHook / $SYS / ACL |
| **Resuscripción automática** | Todos los temas se vuelven a suscribir tras reconectar, sin código adicional |
| **Proxy SOCKS5** | Proxy SOCKS5 integrado con usuario/contraseña para TCP y WS |
| **Cobertura de versiones** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 a net10 |
| **NativeAOT** | Rutas principales verificadas en NativeAOT (API, RPC de clúster, TCP, WS, autenticación, MQTT-SN; 19 escenarios, 0 advertencias ILC) |
| **Cumplimiento** | Autodesarrollado, MIT, sin copyleft ni cláusulas de patentes |

---

## Ventajas diferenciales

### MQTT sobre QUIC (poco común en la industria)
Actualmente **solo EMQX y NewLife.MQTT** admiten este transporte:
- Baja latencia, sin bloqueo de cabeza de línea, ideal para redes débiles y móviles
- .NET 7/8 requieren características preliminares; estable desde .NET 9
- Puerto predeterminado 14567, identificador ALPN `"mqtt"`
- Ruta completa cliente/servidor verificada de extremo a extremo (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT sobre UDP fiable (primero en la industria)
**Primicia mundial, sin necesidad de msquic del sistema operativo, disponible en todas las plataformas**:
- Cabecera de trama de 4 bytes + número de secuencia + ACK + retransmisión por tiempo de espera
- Cubre plataformas embebidas como ARM Linux 4.x sin msquic

### Resuscripción automática tras reconectar
MQTTnet / Eclipse Paho exigen resuscribirse manualmente; NewLife.MQTT lo hace automáticamente:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// No se necesita código adicional tras reconectar.
```

### Funciones empresariales integradas y gratuitas
- **Puente de mensajes**: In / Out / Both, mapeo de prefijos de tema, límite de QoS
- **Motor de reglas**: 5 acciones (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 tipos de evento asíncronos con reintento automático; integrado en MqttHandler + Exchange
- **Clúster**: consenso Raft + particionado por hash consistente + enrutamiento multicapa, sincronización de suscripciones y reenvío entre nodos, puerto RPC dedicado (2883)
- **MQTT-SN v1.2**: pasarela para dispositivos restringidos, interoperable con temas MQTT
- **Almacenamiento persistente**: paquete opcional `NewLife.MQTT.Storage` con Redis / base de datos para mensajes offline y sesiones
- **Temas $SYS**: 14 temas estándar con estadísticas en tiempo real
- **ProxyProtocol v1/v2**: conserva la IP real del cliente a través de nginx/HAProxy

---

## Inicio rápido

### Instalación

```bash
dotnet add package NewLife.MQTT
```

### Iniciar el servidor

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

### Conectar un cliente

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// Suscribirse (se vuelve a suscribir automáticamente tras reconectar)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Publicar
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Inicialización mediante cadena de conexión

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Uso del cliente en detalle

### Especificar la versión del protocolo

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (predeterminada) / V500
};
```

### Publicación con QoS

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### Semántica de QoS

La QoS efectiva de consumo es la **menor** entre la QoS de publicación y la de suscripción:

| QoS | Nombre | Descripción |
|-----|------|------|
| 0 | Como máximo una vez | Sin confirmación; pueden perderse mensajes, apto para latidos de sensores |
| 1 | Al menos una vez | El broker responde PUBACK; puede haber duplicados, apto para datos importantes |
| 2 | Exactamente una vez | Handshake de cuatro pasos; sin pérdidas ni duplicados, apto para facturación/pedidos |

### Suscripciones con comodines

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + coincide con un nivel
await client.SubscribeAsync("/device/#", handler);        // # coincide con varios niveles
```

### Suscripciones compartidas (equilibrio de carga)

Formato `$share/{group}/{topic}`; varios consumidores del mismo grupo reciben mensajes por turnos:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Recibido: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> Servidor WebSocket: desde v12 lo aloja un `HttpServer` independiente (`EnableWebSocket = true`, `WebSocketPort` 0 para asignación del sistema, `WebSocketPath` `/mqtt`) y ya no comparte el puerto TCP de MQTT; los clientes siguen conectando por `ws://host:port/mqtt`. `WebSocketCertificate` habilita TLS en ese puerto, es decir `wss://host:port/mqtt`.

---

## Uso del servidor en detalle

### Controlador personalizado

Herede de `MqttHandler` para interceptar todo tipo de mensajes, ideal para plataformas IoT que almacenan datos directamente:

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

### Autenticación personalizada (ACL)

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

## Funciones empresariales

### Puente de mensajes

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

### Motor de reglas

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

### Despliegue de clúster

Clúster de tres nodos (cada nodo lista las direcciones de los demás):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Operación: Join al iniciar → Ping cada 15 segundos → Lease al apagar; los nodos inactivos (5 minutos) se eliminan automáticamente.

> Desde v12, el RPC de clúster usa HTTP (`ClusterPort` es un puerto HTTP; las direcciones pueden ser `host:port` o `http://host:port`), y `ClusterSecret` se verifica como token en cada llamada; ya no se requiere NewLife.Remoting.

### Adaptador de Alibaba Cloud IoT

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

## Pruébalo rápidamente

Abra el código fuente y establezca `Test` como proyecto de inicio para ejecutar servidor y cliente a la vez:

![Demo](Doc/Demo.png)

---

## Aspectos destacados

| Aspecto | Descripción |
|------|------|
| 🚀 **Protocolo completo** | Soporte completo de MQTT 3.1.1 (14 paquetes) + funciones principales de MQTT 5.0 (30 propiedades) |
| 🔒 **Sin dependencias** | Solo NewLife.Core; sin riesgo de cadena de suministro ni conflictos de licencia |
| ⚖️ **MIT** | Licencia permisiva; uso comercial y modificación libres, sin copyleft ni patentes |
| 🎯 **Cliente + servidor** | Un paquete NuGet `NewLife.MQTT` para ambos; integración mínima |
| 🌐 **Siete transportes** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP fiable / ProxyProtocol v1/v2 |
| 🏢 **Funciones empresariales gratis** | Clúster / puente / motor de reglas / WebHook / estadísticas / $SYS |
| 🇨🇳 **Autodesarrollado** | Propiedad intelectual completa, sin riesgos legales externos, listo para cumplimiento |
| 🔧 **Cobertura de versiones** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Ventajas adicionales

### 1. MQTT sobre QUIC (poco común)
- **Solo EMQX y NewLife.MQTT lo admiten**
- Baja latencia, sin bloqueo de cabeza de línea
- Disponible en .NET 7+ (preliminar en 7/8, estable en 9+)

### 2. MQTT sobre UDP fiable (autodesarrollado)
- **Único en la industria**
- Cabecera de 4 bytes + secuencia + Ack + retransmisión por timeout
- No requiere QUIC/msquic del sistema; funciona en todas las plataformas

### 3. Funciones empresariales integradas y gratuitas
- **Puente bidireccional:** In/Out/Both, prefijos de tema, límite de QoS
- **Motor de reglas (5 acciones):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook:** 6 tipos de evento, asíncrono y no bloqueante, con reintentos
- **Clúster:** configuración manual de nodos, sincronización de suscripciones y reenvío
- **ProxyProtocol:** nginx/HAProxy v1/v2 para conservar la IP real
- **Temas $SYS:** 14 temas estándar con estadísticas en tiempo real

### 4. Resuscripción automática tras reconectar
Los competidores (MQTTnet / Eclipse Paho) requieren resuscribirse manualmente; NewLife.MQTT lo hace automáticamente.

### 5. Adaptador de Alibaba Cloud IoT integrado
`AliyunMqttClient` funciona de inmediato: firma HMAC, reporte de propiedades y sincronización de reloj.

## Protocolo MQTT
MQTT es el protocolo de comunicación IoT más popular, con cliente, servidor y plataforma web de administración.

Ofrece un modelo publicación/suscripción más simple, ligero y fácil de usar, orientado a entornos restringidos (poco ancho de banda, alta latencia, red inestable). En resumen, está hecho para el Internet de las Cosas. Sus características oficiales:
1. Patrón publicación/suscripción con distribución 1:n y desacoplamiento de aplicaciones.
2. Transporte independiente del contenido del payload.
3. Conectividad de red TCP/IP.
4. Tres niveles de QoS:
"Como máximo una vez": depende completamente de TCP/IP; puede haber pérdida o duplicados. Apto para sensores ambientales.
"Al menos una vez": garantiza entrega, pero puede duplicar.
"Exactamente una vez": entrega exactamente una vez; apto para facturación, donde duplicados o pérdidas darían resultados incorrectos.
5. Transporte pequeño con muy poca sobrecarga (cabecera fija de 2 bytes) y mínimo intercambio.
6. Last Will and Testament notifica a los interesados cuando un cliente se desconecta de forma anormal.

## Publicación y suscripción MQTT
Al publicar se especifica la QoS y el broker la almacena; al suscribirse, la QoS de consumo es la menor entre publicación y suscripción.

Detalles:
- Suscripción QoS=0: el consumo siempre es QoS 0;
- Suscripción QoS=1: publicación QoS=0 → consumo QoS=0; publicación QoS=1 o 2 → consumo QoS=1;
- Suscripción QoS=2: el consumo coincide con la QoS publicada;
- Publicación QoS=0: el broker no responde; una pérdida intermedia pasaría desapercibida, pero TCP detecta fallos;
- Publicación QoS=1: el broker responde `PubAck`;
- Publicación QoS=2: el broker responde `PubRec`, el cliente envía `PubRel`, el broker responde `PubComp`; solo entonces se considera publicada;
- Suscripción QoS=2: el broker envía QoS=2, el cliente responde `PubRec`, el broker envía `PubRel`, el cliente responde `PubComp`; solo entonces se considera consumida;
- En QoS=2 el consumidor no necesita estar en línea; el flujo ocurre entre publicador y broker.

## Prueba rápida
Abra la solución, establezca Test como proyecto de inicio y ejecútelo. Por defecto TestServer y TestClient arrancan uno tras otro.

![Demo](Doc/Demo.png)

## Servidor
Referencie `NewLife.MQTT` desde NuGet y arranque el servidor:

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

Con el puerto 1883, el controlador predeterminado `MqttHandler` y el exchange `MqttExchange`, el servidor arranca.

## Cliente
Referencie `NewLife.MQTT` desde NuGet y conéctese:

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

// Suscribirse al tema "/test"
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Publicar cada 2 segundos en "/test"
while (true)
{
    try
    {
        var msg = "El aprendizaje no tiene orden fijo; quien llega primero es el maestro" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

Un cliente necesita `dirección del servidor`, `usuario`, `contraseña` e `identificador de cliente`; luego conecta con `ConnectAsync`. Puede actuar como consumidor con `SubscribeAsync` o como productor con `PublishAsync`.

## Suscripciones compartidas (EMQX)
Compatible con suscripciones compartidas de EMQX: varios clientes se suscriben al mismo tema compartido y el broker distribuye la carga. Formato: `$share/{group}/{topic}`.

```csharp
// Suscribirse a un tema compartido; varios clientes del grupo reciben carga equilibrada
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Mensaje compartido: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Publicar en el tema real (sin el prefijo $share)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Ventajas:
- **Equilibrio de carga**: reparto uniforme dentro del grupo
- **Escalado horizontal**: más suscriptores, más capacidad
- **Alta disponibilidad**: si un suscriptor falla, los demás continúan

### Especificar la versión del protocolo MQTT
Desde la versión 2.3.2025.0212 el cliente permite elegir MQTT 3.1, 3.1.1 o 5.0 con la propiedad `Version`:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

Versiones admitidas:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (predeterminada)
- `MqttVersion.V500` - MQTT 5.0

**Nota**: si no se especifica, se usa MQTT 3.1.1 por compatibilidad.

## Servidor personalizado
Cuando se necesita gestionar conexiones y mensajes en el servidor, se requiere un servidor personalizado. Por ejemplo, una plataforma IoT puede almacenar los datos reportados directamente al recibirlos.

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

Cambie el registro del controlador:

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

## Clúster
Gestión de clúster: Join, Ping, Lease.
Join se une al clúster y anuncia el nodo; se llama N-1 veces al iniciar.
Ping actualiza el tiempo activo de todos los nodos cada 15 segundos.
Lease abandona el clúster; se llama N-1 veces.
Cada nodo elimina periódicamente los nodos cuyo tiempo activo expiró.

## Mapa del ecosistema

NewLife es un ecosistema por capas de proyectos de código abierto con versionado independiente. El mapa siguiente se comparte literalmente en todos los repositorios de NewLife; cada proyecto enlaza a su repositorio y a su paquete principal de NuGet.

| Proyecto | Qué hace | Descargas |
| --- | --- | --- |
| **Fundación** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Registro de logs, configuración, caché, red, serialización, programación de tareas y seguimiento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de datos: tablas únicas a escala de decenas de miles de millones, particionado automático, separación lectura/escritura y más de 10 bases de datos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de red de alto rendimiento: 1,77 M pps / 14,5 Gbps en una máquina (TCP 1KB, 2026-10), 4 M conexiones TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicación por protocolo y RPC para aplicaciones cliente/servidor y dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicaciones .NET como demonios del sistema operativo: servicios de Windows y systemd de Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Plantillas de proyecto `dotnet new` para consola, web, API, servicio, datos e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Datos y mensajería** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis con latencia de microsegundos, millones de operaciones por segundo y colas de mensajes integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente administrado para Apache RocketMQ y Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente y servidor MQTT, incluida la conectividad con Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de datos híbrida en C#: embebida o en red, relacional, series temporales, cola de mensajes y KV en una sola. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Controlador y capa de ayuda de MySql construidos sobre la pila de datos de NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA y distribuido** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rápido para plataformas de administración: permisos, SSO, servidor OAuth y andamiaje CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma distribuida de servicios: gestión de nodos, APM, centro de configuración, registro y centro de despliegues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma de tareas distribuidas y programación de datos en tiempo real con particionado, reintentos y relleno histórico. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 proveedores de LLM más ChatAI, una aplicación web de chat lista para usar. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca estándar de IoT que define contratos de protocolo y abstracciones de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementación de Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo para PLC Siemens sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo para PLC Omron sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo para PLC Mitsubishi Melsec sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para el acceso a datos industriales. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pila de protocolo BACnet para la automatización de edificios. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código abierto y ligera con gestión de pasarelas y dispositivos. | — |
| **Telemática vehicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamiento vehicular JT/T 808: más de 70 cuerpos de mensaje, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma a plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo a bordo JT/T 1078: análisis RTP, remultiplexado FLV/fMP4 y transmisión en directo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de dominio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un solo paquete para Excel, Word, PPT, PDF y más de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplataforma: códecs, E/S de dispositivos, DSP, contenedores y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Códecs de vídeo, formatos de contenedor, procesamiento de fotogramas y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interfaz unificada sobre Baidu, AMap, Tencent y Tianditu con geocodificación y conversión de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalización IPv4 con una base de datos offline mapeada en memoria y una API en línea de respaldo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Herramientas y productos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caja de herramientas del desarrollador: red, puerto serie, criptografía, expresiones regulares, Modbus y MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema de gestión de código fuente Git autoalojado. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automáticamente el runtime de .NET correspondiente cuando se ejecuta desde un host más antiguo. | — |

> Las insignias muestran el paquete principal de cada proyecto; los paquetes de temas, extensiones y satélites figuran en sus páginas de NuGet.Los más de 30 paquetes no listados aquí (FTP, LoRa, extensiones de Office, controladores de protocolo, etc.) están disponibles en la [organización NewLifeX](https://github.com/NewLifeX) y en [newlifex.com](https://newlifex.com).

## Equipo

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife se fundó en 2002 y lleva más de dos décadas dedicado al ecosistema .NET e IoT, mantiene cerca de 100 proyectos de código abierto (34 de ellos principales) y ofrece soluciones integradas de software y hardware para los sectores de energía, logística, salud, control industrial e Internet. Nos guía el lema «en el aprendizaje no hay antigüedad: quien domina es maestro».

Los componentes de código abierto acumulan <!--nuget-total:start-->6.48M<!--nuget-total:end--> descargas en NuGet (todo el ecosistema, a fecha de 2026-10).

Sitio web: https://newlifex.com · Organización de código abierto: https://github.com/NewLifeX

<details>
<summary>Cuenta oficial de WeChat: 智能大石头 (escanea para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licencia

MIT License © 2002-2026 Equipo NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
