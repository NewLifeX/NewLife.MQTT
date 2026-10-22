# NewLife.MQTT - Полная реализация протокола MQTT

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Перевод может отставать от [English README](README.md).

**NewLife.MQTT** — это **полностью собственный, без сторонних зависимостей, MIT-лицензированный** полный реализация протокола MQTT. Один NuGet-пакет предоставляет и полнофункциональный `MqttClient`, и высокопроизводительный `MqttServer` (брокер), охватывая все основные среды от .NET Framework 4.5 до .NET 10.

## 📖 Навигация по документации

| Документ | Описание |
|------|------|
| [Требования](Doc/需求文档.md) | Видение, цели, требования 7 основных модулей, отложенные пункты и не-цели |
| [Список функций](Doc/功能清单.md) | Трёхмерное отслеживание реализации, тестов и комментариев для 87 функциональных точек |
| [Архитектура](Doc/架构设计.md) | Пятиуровневая архитектура, модели данных, интерфейсы, ключевые решения и производительность |
| [Архитектура сервера](Doc/SRV-服务端架构.md) | Устройство брокера: маршрутизация, аутентификация и HTTP API управления |
| [Архитектура кластера](Doc/CLUS-集群架构.md) | Четырёхэтапная эволюция кластера, консенсус Raft, шардирование топиков и многоуровневая топология |
| [Конкурентный анализ](Doc/竞品分析.md) | Сравнение экосистемы MQTT (EMQX/MQTTnet/Mosquitto) и SWOT |
| [Конкурентный анализ кластера](Doc/CLUS-集群竞品分析.md) | Сравнение кластерных возможностей (EMQX/HiveMQ/VerneMQ) |
| [Интеграционные тесты](Doc/集成测试说明.md) | Структура тестов, запуск, категории и кросс-совместимость (NewLife ↔ MQTTnet, обязательно в CI) |
| [Совместимость с AOT](Doc/AOT兼容性分析.md) | Измерения NativeAOT: 19 сценариев в JIT и AOT, матрица RID |
| [Примечания к выпуску](Doc/更新日志.md) | История версий и подробности коммитов |

---

## Ключевые возможности

| Возможность | Описание |
|------|------|
| **Полный протокол** | MQTT 3.1 / 3.1.1 / 5.0, 14 типов пакетов + пакет AUTH, 30 свойств MQTT 5.0 |
| **Без зависимостей** | Только NewLife.Core; никаких сторонних пакетов в цепочке NuGet |
| **Клиент и сервер вместе** | Один пакет NuGet `NewLife.MQTT`, без отдельных сборок |
| **Семь транспортов** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / надёжный UDP / ProxyProtocol v1/v2 |
| **Высокая производительность** | Миллионы подключений на узел (проверено 4 млн); брокер — около **1,25 млн сообщений/с** на входе и 106 000 сообщений/с сквозь соединение; маршрутизация — 120 нс на запись |
| **Корпоративные функции бесплатно** | Кластер (Raft / шардирование) / мост / движок правил / WebHook / $SYS / ACL |
| **Автоподписка после переподключения** | Все топики переподписываются автоматически |
| **Прокси SOCKS5** | Встроенный SOCKS5 с логином/паролем для TCP и WS |
| **Полный охват версий** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6–net10 |
| **NativeAOT** | Основные пути проверены в NativeAOT (API, кластерный RPC, TCP, WS, аутентификация, MQTT-SN; 19 сценариев, 0 предупреждений ILC) |
| **Соответствие требованиям** | Полностью собственный код, MIT, без copyleft и патентных оговорок |

---

## Отличительные преимущества

### MQTT поверх QUIC (редкость в отрасли)
Сейчас **только EMQX и NewLife.MQTT** поддерживают этот транспорт:
- Низкая задержка, нет блокировки головы очереди, идеально для слабых сетей и мобильных устройств
- .NET 7/8 требуют предварительных функций; стабильно с .NET 9
- Порт по умолчанию 14567, идентификатор ALPN `"mqtt"`
- Полный путь клиент/сервер проверен сквозным образом (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT поверх надёжного UDP (первый в отрасли)
**Первый в мире, без msquic ОС, доступен на всех платформах**:
- 4-байтовый заголовок кадра + номер последовательности + ACK + повтор по таймауту
- Подходит для встраиваемых платформ вроде ARM Linux 4.x без msquic

### Автоподписка после переподключения
MQTTnet / Eclipse Paho требуют ручной переподписки; NewLife.MQTT делает это автоматически:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// После переподключения дополнительный код не нужен.
```

### Корпоративные функции встроены и бесплатны
- **Мост сообщений**: In / Out / Both, отображение префиксов топиков, ограничение QoS
- **Движок правил**: 5 действий (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 типов событий асинхронно с автоповтором; интегрировано в MqttHandler + Exchange
- **Кластер**: консенсус Raft + согласованное хеширование топиков + многоуровневая маршрутизация, синхронизация подписок и пересылка между узлами, отдельный RPC-порт (2883)
- **MQTT-SN v1.2**: шлюз для ограниченных устройств, совместим с топиками MQTT
- **Постоянное хранилище**: необязательный пакет `NewLife.MQTT.Storage` с Redis / БД для офлайн-сообщений и сессий
- **Системные топики $SYS**: 14 стандартных топиков со статистикой в реальном времени
- **ProxyProtocol v1/v2**: сохранение реального IP клиента через nginx/HAProxy

---

## Быстрый старт

### Установка

```bash
dotnet add package NewLife.MQTT
```

### Запуск сервера

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

### Подключение клиента

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// Подписка (после переподключения — автоматически)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Публикация
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Инициализация строкой подключения

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Подробно о клиенте

### Указание версии протокола

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (по умолчанию) / V500
};
```

### Публикация с QoS

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### Семантика QoS

Эффективная QoS потребления — **меньшая** из QoS публикации и подписки:

| QoS | Название | Описание |
|-----|------|------|
| 0 | Не более одного раза | Без подтверждения; возможны потери, подходит для датчиков |
| 1 | Не менее одного раза | Брокер отвечает PUBACK; возможны дубликаты, подходит для важных данных |
| 2 | Ровно один раз | Четырёхшаговое рукопожатие; без потерь и дублей, подходит для биллинга/заказов |

### Подписки с шаблонами

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + — один уровень
await client.SubscribeAsync("/device/#", handler);        // # — несколько уровней
```

### Разделяемые подписки (балансировка)

Формат `$share/{group}/{topic}`; несколько потребителей группы получают сообщения по очереди:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Получено: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> Сервер WebSocket: с v12 размещается отдельным `HttpServer` (`EnableWebSocket = true`, `WebSocketPort` по умолчанию 0 — системное назначение, `WebSocketPath` по умолчанию `/mqtt`) и не использует общий TCP-порт MQTT; клиенты по-прежнему подключаются через `ws://host:port/mqtt`. `WebSocketCertificate` включает TLS на этом порту, то есть `wss://host:port/mqtt`.

---

## Подробно о сервере

### Пользовательский обработчик

Наследуйте `MqttHandler`, чтобы перехватывать все сообщения; идеально для IoT-платформ, сохраняющих данные напрямую:

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

### Пользовательская аутентификация (ACL)

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

## Корпоративные функции

### Мост сообщений

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

### Движок правил

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

### Развёртывание кластера

Кластер из трёх узлов (каждый перечисляет адреса остальных):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Работа: Join при старте → Ping каждые 15 секунд → Lease при остановке; узлы с истёкшим временем (5 минут) удаляются автоматически.

> С v12 кластерный RPC использует HTTP (`ClusterPort` — HTTP-порт; адреса `host:port` или `http://host:port`), `ClusterSecret` проверяется как токен при каждом вызове; NewLife.Remoting больше не нужен.

### Адаптер Alibaba Cloud IoT

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

## Быстрая проверка

Откройте исходники и задайте `Test` стартовым проектом, чтобы запустить сервер и клиент одновременно:

![Demo](Doc/Demo.png)

---

## Ключевые преимущества

| Преимущество | Описание |
|------|------|
| 🚀 **Полный протокол** | Полная поддержка MQTT 3.1.1 (14 пакетов) + основные функции MQTT 5.0 (30 свойств) |
| 🔒 **Без зависимостей** | Только NewLife.Core; нет рисков цепочки поставок и конфликтов лицензий |
| ⚖️ **MIT** | Самая свободная лицензия; коммерческое использование и изменение без copyleft и патентов |
| 🎯 **Клиент + сервер** | Один пакет `NewLife.MQTT` для обоих; минимальная интеграция |
| 🌐 **Семь транспортов** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / надёжный UDP / ProxyProtocol v1/v2 |
| 🏢 **Корпоративные функции бесплатно** | Кластер / мост / движок правил / WebHook / статистика / $SYS |
| 🇨🇳 **Полностью собственный** | Полная интеллектуальная собственность, без внешних юридических рисков, готов к комплаенсу |
| 🔧 **Полный охват версий** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Дополнительные преимущества

### 1. MQTT поверх QUIC (редкость)
- **Поддерживают только EMQX и NewLife.MQTT**
- Низкая задержка, без блокировки головы очереди
- Доступно в .NET 7+ (предпросмотр в 7/8, стабильно в 9+)

### 2. MQTT поверх надёжного UDP (собственная разработка)
- **Единственный в отрасли**
- 4-байтовый заголовок + последовательность + Ack + повтор по таймауту
- Не требует QUIC/msquic ОС; работает на всех платформах

### 3. Корпоративные функции встроены и бесплатны
- **Двунаправленный мост:** In/Out/Both, префиксы топиков, ограничение QoS
- **Движок правил (5 действий):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook:** 6 типов событий, асинхронно и без блокировок, с повторами
- **Кластер:** ручная настройка узлов, синхронизация подписок и пересылка
- **ProxyProtocol:** nginx/HAProxy v1/v2 для сохранения реального IP
- **Топики $SYS:** 14 стандартных топиков со статистикой в реальном времени

### 4. Автоподписка после переподключения
Конкуренты (MQTTnet / Eclipse Paho) требуют ручной переподписки; NewLife.MQTT делает это автоматически.

### 5. Встроенный адаптер Alibaba Cloud IoT
`AliyunMqttClient` работает сразу: подпись HMAC, отчёт свойств и синхронизация времени.

## Протокол MQTT
MQTT — самый популярный протокол IoT-связи, включающий клиент, сервер и веб-платформу управления.

Он предлагает более простую, лёгкую и удобную модель публикации/подписки для ограниченных сред (низкая пропускная способность, высокая задержка, нестабильная сеть). Проще говоря, создан для Интернета вещей. Официальные особенности:
1. Модель публикации/подписки с распределением 1:n и развязкой приложений.
2. Транспорт, независимый от содержимого payload.
3. Сетевое подключение по TCP/IP.
4. Три уровня QoS:
«Не более одного раза»: полностью зависит от TCP/IP; возможны потери и дубликаты. Подходит для датчиков среды.
«Не менее одного раза»: доставка гарантирована, возможны дубликаты.
«Ровно один раз»: ровно одна доставка; подходит для биллинга, где дубликаты или потери дают неверный результат.
5. Компактный транспорт с минимальными накладными расходами (фиксированный заголовок 2 байта) и минимальным обменом.
6. Last Will and Testament уведомляет заинтересованных при аномальном отключении.

## Публикация и подписка MQTT
При публикации указывается QoS, и брокер сохраняет её; при подписке QoS потребления — меньшая из двух.

Подробности:
- Подписка QoS=0: потребление всегда QoS 0;
- Подписка QoS=1: публикация QoS=0 → потребление QoS=0; публикация QoS=1 или 2 → потребление QoS=1;
- Подписка QoS=2: потребление совпадает с опубликованной QoS;
- Публикация QoS=0: брокер не отвечает; потеря по пути теоретически незаметна, но TCP обнаруживает сбои;
- Публикация QoS=1: брокер отвечает `PubAck`;
- Публикация QoS=2: брокер отвечает `PubRec`, клиент отправляет `PubRel`, брокер отвечает `PubComp`; только тогда сообщение опубликовано;
- Подписка QoS=2: брокер отправляет QoS=2, клиент отвечает `PubRec`, брокер отправляет `PubRel`, клиент отвечает `PubComp`; только тогда сообщение потреблено;
- При QoS=2 потребителю не нужно быть онлайн; обмен идёт между издателем и брокером.

## Быстрый опыт
Откройте решение, задайте Test стартовым проектом и запустите. По умолчанию TestServer и TestClient стартуют по очереди.

![Demo](Doc/Demo.png)

## Сервер
Подключите `NewLife.MQTT` из NuGet и запустите сервер:

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

С портом 1883, обработчиком `MqttHandler` и обменом `MqttExchange` сервер запускается.

## Клиент
Подключите `NewLife.MQTT` из NuGet и подключитесь:

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

// Подписка на топик "/test"
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Публикация каждые 2 секунды в "/test"
while (true)
{
    try
    {
        var msg = "В учении нет фиксированного порядка; кто пришёл первым, тот и учитель" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

Клиенту нужны `адрес сервера`, `имя пользователя`, `пароль` и `идентификатор клиента`; затем `ConnectAsync`. Клиент может потреблять через `SubscribeAsync` или публиковать через `PublishAsync`.

## Разделяемые подписки (EMQX)
Поддерживаются разделяемые подписки EMQX: несколько клиентов подписываются на один общий топик, брокер распределяет нагрузку. Формат: `$share/{group}/{topic}`.

```csharp
// Подписка на общий топик; клиенты группы получают с балансировкой
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Общее сообщение: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Публикация в реальный топик (без префикса $share)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Преимущества:
- **Балансировка нагрузки**: равномерное распределение в группе
- **Горизонтальное масштабирование**: больше подписчиков — больше мощность
- **Высокая доступность**: при сбое одного подписчика остальные продолжают

### Указание версии протокола MQTT
С версии 2.3.2025.0212 клиент позволяет выбрать MQTT 3.1, 3.1.1 или 5.0 через свойство `Version`:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

Поддерживаемые версии:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (по умолчанию)
- `MqttVersion.V500` - MQTT 5.0

**Примечание**: без явного значения используется MQTT 3.1.1 для обратной совместимости.

## Пользовательский сервер
Когда требуется управлять подключениями и сообщениями на сервере, нужен пользовательский сервер. Например, IoT-платформа может сохранять данные устройств сразу при получении.

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

Измените регистрацию обработчика:

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

## Кластер
Управление кластером: Join, Ping, Lease.
Join вступает в кластер и сообщает о узле; вызывается N-1 раз при старте.
Ping обновляет время активности всех узлов каждые 15 секунд.
Lease покидает кластер; вызывается N-1 раз.
Каждый узел периодически удаляет узлы с истёкшим временем активности.

## Карта экосистемы

NewLife — это многоуровневая экосистема независимо версионируемых open-source проектов. Карта ниже дословно используется во всех репозиториях NewLife; каждый проект ссылается на свой репозиторий и свой основной пакет NuGet.

| Проект | Что делает | Загрузки |
| --- | --- | --- |
| **Базовый** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Логирование, конфигурация, кэширование, сеть, сериализация, планирование и APM-трассировка. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Промежуточное ПО данных: отдельные таблицы масштаба десятков миллиардов строк, автоматическое секционирование, разделение чтения и записи, более 10 баз данных. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Высокопроизводительная сетевая библиотека: 1,77M pps / 14,5 Гбит/с на машину (TCP 1KB, 2026-10), 4M долгоживущих TCP-соединений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Протокольная связь и RPC-фреймворк для клиент-серверных приложений и IoT-устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Устанавливает .NET-приложения как демоны ОС: службы Windows и systemd в Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Шаблоны проектов `dotnet new` для консоли, web, API, службы, данных и IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Данные и обмен сообщениями** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Клиент Redis с микросекундной задержкой, миллионами операций в секунду и встроенными очередями сообщений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Полностью управляемый клиент RocketMQ для Apache RocketMQ и Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Клиент и сервер MQTT, включая подключение к Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Гибридная база данных на C#: встроенная или сетевая, реляционная, временных рядов, очередь сообщений и KV в одном. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Драйвер MySql и вспомогательный слой на основе стека данных NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI и распределённые системы** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Фреймворк быстрой разработки админ-платформ: права, SSO, сервер OAuth и CRUD-скаффолдинг. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Распределённая сервисная платформа: управление узлами, APM, центр конфигурации, реестр и центр публикации. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Распределённая платформа задач и планирования данных в реальном времени с секционированием, повторами и дозаполнением. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Единый клиент для 46 поставщиков LLM плюс ChatAI — готовое веб-приложение для чата. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Промышленность и IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Стандартная библиотека IoT, определяющая контракты протоколов и абстракции устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Реализация Modbus TCP/RTU/ASCII поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Протокол Siemens PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Протокол Omron PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Протокол Mitsubishi Melsec PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Клиент OPC для доступа к промышленным данным. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Стек протокола автоматизации зданий BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Лёгкая open-source платформа IoT с управлением шлюзами и устройствами. | — |
| **Телематика транспорта** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Протокол позиционирования транспорта JT/T 808: более 70 типов сообщений, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Межплатформенный протокол JT/T 809 для органов управления транспортом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Автомобильное видео JT/T 1078: разбор RTP, ремультиплексирование в FLV/fMP4 и прямая трансляция. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Прикладные компоненты** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Один пакет для Excel, Word, PPT, PDF и более 15 форматов документов. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Кроссплатформенное аудио: кодеки, ввод-вывод устройств, DSP, контейнеры и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Видеокодеки, форматы контейнеров, обработка кадров и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Единый интерфейс к Baidu, AMap, Tencent и Tianditu с геокодированием и преобразованием координат. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Геолокация IPv4 с офлайн-базой, отображаемой в память, и онлайн-API как резервом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Инструменты и продукты** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Инструменты разработчика: сеть, последовательный порт, шифрование, регулярные выражения, Modbus и MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Самостоятельно размещаемая система управления исходным кодом Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Автоматически устанавливает подходящую среду выполнения .NET при запуске на старом хосте. | — |

> Бейджи показывают основной пакет каждого проекта; пакеты тем, расширений и сателлитов перечислены на соответствующих страницах NuGet.Более 30 пакетов, не перечисленных здесь (FTP, LoRa, расширения Office, драйверы протоколов и другие), доступны в [организации NewLifeX](https://github.com/NewLifeX) и на [newlifex.com](https://newlifex.com).

## Команда

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Команда NewLife основана в 2002 году и более двух десятилетий работает в экосистеме .NET и IoT, поддерживает около 100 проектов с открытым кодом (34 из них — основные) и предлагает интегрированные программно-аппаратные решения для энергетики, логистики, здравоохранения, промышленной автоматизации и интернет-отрасли. Мы следуем девизу «в учении нет старших — мастерство приносит уважение».

Open-source компоненты скачали <!--nuget-total:start-->6.48M<!--nuget-total:end--> раз с NuGet (по всей экосистеме, по состоянию на 2026-10).

Сайт: https://newlifex.com · Организация open source: https://github.com/NewLifeX

<details>
<summary>Официальный аккаунт WeChat: 智能大石头 (отсканируйте, чтобы подписаться)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Лицензия

MIT License © 2002-2026 Команда NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
