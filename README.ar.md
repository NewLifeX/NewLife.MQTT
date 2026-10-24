# NewLife.MQTT - تنفيذ كامل لبروتوكول MQTT

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> قد تتأخر هذه الترجمة عن [English README](README.md).

**NewLife.MQTT** هو تطبيق كامل لبروتوكول MQTT، **مطوَّر بالكامل داخليًا، بدون أي اعتماد على جهات خارجية، وبترخيص MIT**. توفّر حزمة NuGet واحدة كلًا من `MqttClient` كامل الميزات و`MqttServer` (وسيط) عالي الأداء، وتغطي جميع بيئات التشغيل الرئيسية من .NET Framework 4.5 حتى .NET 10.

## 📖 التنقل في الوثائق

| الوثيقة | الوصف |
|------|------|
| [المتطلبات](Doc/需求文档.md) | رؤية المشروع، أهدافه، متطلبات الوحدات السبع، العناصر المؤجلة وغير الأهداف |
| [قائمة الميزات](Doc/功能清单.md) | تتبّع ثلاثي الأبعاد للتنفيذ والاختبارات والتعليقات لـ 87 نقطة وظيفية |
| [تصميم البنية](Doc/架构设计.md) | بنية من خمس طبقات، نماذج البيانات، الواجهات، القرارات الرئيسية وتصميم الأداء |
| [بنية الخادم](Doc/SRV-服务端架构.md) | تصميم الوسيط: توجيه الرسائل، المصادقة، وواجهة HTTP للإدارة |
| [بنية العنقود](Doc/CLUS-集群架构.md) | خارطة تطوير العنقود بأربع مراحل، إجماع Raft، تقسيم الموضوعات والطوبولوجيا متعددة المستويات |
| [تحليل المنافسين](Doc/竞品分析.md) | مقارنة منظومة MQTT (EMQX/MQTTnet/Mosquitto) وتحليل SWOT |
| [تحليل منافسي العنقود](Doc/CLUS-集群竞品分析.md) | مقارنة مخصّصة لقدرات العنقود (EMQX/HiveMQ/VerneMQ) |
| [اختبارات التكامل](Doc/集成测试说明.md) | بنية الاختبار، طريقة التشغيل، الفئات والتوافق المتبادل (NewLife ↔ MQTTnet، إلزامي في CI) |
| [توافق AOT](Doc/AOT兼容性分析.md) | قياسات NativeAOT: 19 سيناريو تعمل في JIT وAOT، ومصفوفة RID |
| [ملاحظات الإصدار](Doc/更新日志.md) | سجل الإصدارات وتفاصيل الالتزامات |

---

## الميزات الأساسية

| الميزة | الوصف |
|------|------|
| **بروتوكول كامل** | MQTT 3.1 / 3.1.1 / 5.0، 14 نوع حزمة + حزمة AUTH، و30 خاصية في MQTT 5.0 |
| **بدون تبعيات** | يعتمد على NewLife.Core فقط؛ لا حزم خارجية في سلسلة NuGet |
| **عميل وخادم معًا** | حزمة NuGet واحدة `NewLife.MQTT`، دون بناء منفصل |
| **سبعة وسائل نقل** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP موثوق / ProxyProtocol v1/v2 |
| **أداء عالٍ** | ملايين الاتصالات لكل عقدة (تم التحقق من 4 ملايين)؛ الوسيط نحو **1.25 مليون رسالة/ث** واردة و106,000 رسالة/ث من طرف إلى طرف؛ التوجيه ثابت عند 120 نانوثانية لكل إدخال |
| **ميزات مؤسسية مجانية** | العنقود (Raft / تقسيم الموضوعات) / الجسر / محرك القواعد / WebHook / $SYS / ACL |
| **إعادة اشتراك تلقائية** | تُعاد الاشتراكات تلقائيًا بعد إعادة الاتصال |
| **وكيل SOCKS5** | وكيل SOCKS5 مدمج مع مصادقة اسم المستخدم/كلمة المرور لـ TCP وWS |
| **تغطية الإصدارات** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 إلى net10 |
| **NativeAOT** | المسارات الرئيسية مُتحقَّق منها في NativeAOT (واجهة الإدارة، RPC العنقود، TCP، WS، المصادقة، MQTT-SN؛ 19 سيناريو، 0 تحذيرات ILC) |
| **تعدد اللغات (31 لغة)** | تُترجم رسائل وقت التشغيل ولوحة المعلومات وبيانات تعريف الحزمة إلى 31 لغة عبر قوائم NewLife.Core المترجمة، مع الرجوع إلى الإنجليزية عند غياب الترجمة |
| **ناقل الأحداث** | يوفر `MqttEventBus` نشرًا واشتراكًا مُحدَّد النوع عبر موضوعات MQTT: بث افتراضي، واشتراك مشترك `$share` اختياري، ويتوافق مع عقود `IEventBusFactory` في NewLife.Core |
| **الامتثال** | تطوير داخلي كامل، MIT، دون copyleft أو شروط براءات |

---

## مزايا التميّز

### MQTT عبر QUIC (نادر في المجال)
حاليًا **EMQX وNewLife.MQTT فقط** يدعمان وسيلة النقل هذه:
- زمن استجابة منخفض، دون حجب الرأس، مثالي للشبكات الضعيفة والهواتف
- يتطلب .NET 7/8 ميزات معاينة؛ مستقر من .NET 9
- المنفذ الافتراضي 14567، معرّف ALPN `"mqtt"`
- تم التحقق من مسار العميل/الخادم كاملًا من طرف إلى طرف (Linux/libmsquic، `QuicIntegrationTests`)

### MQTT عبر UDP الموثوق (الأول في المجال)
**الأول عالميًا، دون الحاجة إلى msquic في نظام التشغيل، ومتاح على جميع المنصات**:
- رأس إطار 4 بايت + رقم تسلسل + ACK + إعادة إرسال عند انتهاء المهلة
- يغطي المنصات المدمجة مثل ARM Linux 4.x التي لا تدعم msquic

### إعادة الاشتراك تلقائيًا بعد إعادة الاتصال
يتطلب MQTTnet / Eclipse Paho إعادة اشتراك يدوية؛ أما NewLife.MQTT فيفعلها تلقائيًا:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// لا حاجة إلى كود إضافي بعد إعادة الاتصال.
```

### ميزات مؤسسية مدمجة ومجانية
- **جسر الرسائل**: In / Out / Both، تعيين بادئات الموضوعات، حد QoS
- **محرك القواعد**: 5 إجراءات (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 أنواع أحداث غير متزامنة مع إعادة محاولة تلقائية؛ مدمج في MqttHandler + Exchange
- **العنقود**: إجماع Raft + تجزئة متسقة للموضوعات + توجيه متعدد المستويات، مزامنة الاشتراكات وإعادة توجيه الرسائل بين العقد، منفذ RPC مخصص (2883)
- **MQTT-SN v1.2**: بوابة للأجهزة المحدودة، متوافقة مع موضوعات MQTT
- **تخزين دائم**: حزمة اختيارية `NewLife.MQTT.Storage` مع Redis / قاعدة بيانات للرسائل غير المتصلة والجلسات
- **موضوعات $SYS**: 14 موضوعًا قياسيًا مع إحصاءات فورية
- **ProxyProtocol v1/v2**: الحفاظ على عنوان IP الحقيقي للعميل عبر nginx/HAProxy

---

## البدء السريع

### التثبيت

```bash
dotnet add package NewLife.MQTT
```

### تشغيل الخادم

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

### توصيل العميل

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// الاشتراك (يُعاد تلقائيًا بعد إعادة الاتصال)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// النشر
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### التهيئة بسلسلة الاتصال

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## تفاصيل العميل

### تحديد إصدار البروتوكول

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (افتراضي) / V500
};
```

### النشر مع QoS

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### دلالات QoS

QoS الاستهلاك الفعلية هي **الأصغر** بين QoS النشر والاشتراك:

| QoS | الاسم | الوصف |
|-----|------|------|
| 0 | مرة واحدة على الأكثر | دون تأكيد؛ قد تُفقد الرسائل، مناسب لنبضات المستشعرات |
| 1 | مرة واحدة على الأقل | يرد الوسيط بـ PUBACK؛ قد تتكرر الرسائل، مناسب للبيانات المهمة |
| 2 | مرة واحدة بالضبط | مصافحة من أربع خطوات؛ دون فقدان أو تكرار، مناسب للفوترة/الطلبات |

### الاشتراكات باستخدام أحرف البدل

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + يطابق مستوى واحدًا
await client.SubscribeAsync("/device/#", handler);        // # يطابق عدة مستويات
```

### الاشتراكات المشتركة (موازنة الحمل)

التنسيق `$share/{group}/{topic}`؛ يتلقى عدة مستهلكين في المجموعة الرسائل بالتناوب:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"تم الاستلام: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> خادم WebSocket: منذ v12 يستضيفه `HttpServer` منفصل (`EnableWebSocket = true`، `WebSocketPort` افتراضيًا 0 ليخصصه النظام، `WebSocketPath` افتراضيًا `/mqtt`) ولم يعد يشارك منفذ TCP الخاص بـ MQTT؛ يتصل العملاء عبر `ws://host:port/mqtt`. تفعيل `WebSocketCertificate` يمكّن TLS على المنفذ، أي `wss://host:port/mqtt`.

---

## تفاصيل الخادم

### معالج مخصص

ورّث من `MqttHandler` لاعتراض جميع الرسائل، وهو مثالي لمنصات IoT التي تخزن البيانات مباشرة:

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

### مصادقة مخصصة (ACL)

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

## الميزات المؤسسية

### جسر الرسائل

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

### محرك القواعد

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

### نشر العنقود

عنقود من ثلاث عقد (تسرد كل عقدة عناوين العقد الأخرى):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

التشغيل: Join عند البدء ← Ping كل 15 ثانية ← Lease عند الإيقاف؛ تُزال العقد المنتهية (5 دقائق) تلقائيًا.

> منذ v12 يستخدم RPC العنقود HTTP (`ClusterPort` منفذ HTTP؛ العناوين `host:port` أو `http://host:port`)، ويُتحقق من `ClusterSecret` كرمز في كل استدعاء؛ لم تعد NewLife.Remoting مطلوبة.

### محول Alibaba Cloud IoT

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

## جرّبه بسرعة

افتح الشيفرة المصدرية واضبط `Test` كمشروع بدء لتشغيل الخادم والعميل معًا:

![Demo](Doc/Demo.png)

---

## أبرز المزايا

| الميزة | الوصف |
|------|------|
| 🚀 **بروتوكول كامل** | دعم كامل لـ MQTT 3.1.1 (14 حزمة) + ميزات MQTT 5.0 الأساسية (30 خاصية) |
| 🔒 **بدون تبعيات** | NewLife.Core فقط؛ لا مخاطر سلسلة توريد ولا تعارض تراخيص |
| ⚖️ **MIT** | أكثر التراخيص تسامحًا؛ تعديل واستخدام تجاري بحرية، دون copyleft أو براءات |
| 🎯 **عميل + خادم** | حزمة `NewLife.MQTT` واحدة للاثنين؛ تكامل أدنى |
| 🌐 **سبعة وسائل نقل** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP موثوق / ProxyProtocol v1/v2 |
| 🏢 **ميزات مؤسسية مجانية** | العنقود / الجسر / محرك القواعد / WebHook / الإحصاءات / $SYS |
| 🇨🇳 **تطوير داخلي كامل** | ملكية فكرية كاملة، دون مخاطر قانونية خارجية، جاهز للامتثال |
| 🔧 **تغطية الإصدارات** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## مزايا إضافية

### 1. MQTT عبر QUIC (نادر)
- **EMQX وNewLife.MQTT فقط يدعمانه**
- زمن استجابة منخفض، دون حجب الرأس
- متاح في .NET 7+ (معاينة في 7/8، مستقر في 9+)

### 2. MQTT عبر UDP الموثوق (تطوير داخلي)
- **الأول في المجال**
- رأس 4 بايت + تسلسل + Ack + إعادة إرسال عند المهلة
- لا يتطلب QUIC/msquic في النظام؛ يعمل على جميع المنصات

### 3. ميزات مؤسسية مدمجة ومجانية
- **جسر ثنائي الاتجاه:** In/Out/Both، بادئات الموضوعات، حد QoS
- **محرك قواعد (5 إجراءات):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook:** 6 أنواع أحداث، غير متزامن وغير حاجب، مع إعادة محاولة
- **العنقود:** تهيئة يدوية للعقد، مزامنة الاشتراكات وإعادة التوجيه
- **ProxyProtocol:** nginx/HAProxy v1/v2 للحفاظ على IP الحقيقي
- **موضوعات $SYS:** 14 موضوعًا قياسيًا مع إحصاءات فورية

### 4. إعادة الاشتراك تلقائيًا بعد إعادة الاتصال
المنافسون (MQTTnet / Eclipse Paho) يتطلبون إعادة اشتراك يدوية؛ NewLife.MQTT يفعلها تلقائيًا.

### 5. محول Alibaba Cloud IoT مدمج
`AliyunMqttClient` يعمل مباشرة: توقيع HMAC، الإبلاغ عن الخصائص ومزامنة الوقت.

## بروتوكول MQTT
MQTT هو بروتوكول التواصل الأكثر شيوعًا في إنترنت الأشياء، ويشمل العميل والخادم ومنصة إدارة الويب.

يوفّر نموذج نشر/اشتراك أبسط وأخف وأسهل، موجهًا للبيئات المحدودة (نطاق منخفض، زمن استجابة عالٍ، شبكة غير مستقرة). باختصار، صُمم لإنترنت الأشياء. خصائصه الرسمية:
1. نمط نشر/اشتراك مع توزيع 1:n وفصل التطبيقات.
2. نقل مستقل عن محتوى الحمولة.
3. اتصال شبكي عبر TCP/IP.
4. ثلاثة مستويات QoS:
«مرة واحدة على الأكثر»: يعتمد كليًا على TCP/IP؛ قد يحدث فقدان أو تكرار. مناسب لمستشعرات البيئة.
«مرة واحدة على الأقل»: يضمن التسليم، لكن قد يتكرر.
«مرة واحدة بالضبط»: تسليم واحد بالضبط؛ مناسب لأنظمة الفوترة حيث يؤدي التكرار أو الفقدان إلى نتائج خاطئة.
5. نقل صغير بحمل زائد منخفض جدًا (رأس ثابت 2 بايت) وتبادل أدنى.
6. Last Will and Testament لإخطار المعنيين عند الانقطاع غير الطبيعي.

## النشر والاشتراك في MQTT
عند النشر تُحدَّد QoS ويخزنها الوسيط؛ وعند الاشتراك تكون QoS الاستهلاك هي الأصغر بين نشر واشتراك.

التفاصيل:
- اشتراك QoS=0: الاستهلاك دائمًا QoS 0；
- اشتراك QoS=1: نشر QoS=0 ← استهلاك QoS=0؛ نشر QoS=1 أو 2 ← استهلاك QoS=1；
- اشتراك QoS=2: الاستهلاك يطابق QoS المنشورة；
- نشر QoS=0: لا يرد الوسيط؛ الفقدان في الطريق قد يمر دون ملاحظة نظريًا، لكن TCP يكشف الأعطال；
- نشر QoS=1: يرد الوسيط بـ `PubAck`；
- نشر QoS=2: يرد الوسيط بـ `PubRec`، يرسل العميل `PubRel`، يرد الوسيط بـ `PubComp`، وعندها فقط تُعدّ منشورة；
- اشتراك QoS=2: يدفع الوسيط QoS=2، يرد العميل `PubRec`، يرسل الوسيط `PubRel`، يرد العميل `PubComp`، وعندها فقط تُعدّ مستهلكة；
- في QoS=2 لا يلزم أن يكون المستهلك متصلًا؛ يتم التدفق بين الناشر والوسيط.

## تجربة سريعة
افتح الحل، اضبط Test كمشروع بدء وشغّله. افتراضيًا يبدأ TestServer ثم TestClient.

![Demo](Doc/Demo.png)

## الخادم
أضف مرجعًا إلى `NewLife.MQTT` من NuGet وابدأ الخادم:

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

مع المنفذ 1883 والمعالج الافتراضي `MqttHandler` والتبادل `MqttExchange` يبدأ الخادم.

## العميل
أضف مرجعًا إلى `NewLife.MQTT` من NuGet واتصل:

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

// الاشتراك في الموضوع "/test"
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// النشر كل ثانيتين إلى "/test"
while (true)
{
    try
    {
        var msg = "لا ترتيب ثابتًا في التعلم؛ من يصل أولًا هو المعلم" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

يحتاج العميل إلى `عنوان الخادم` و`اسم المستخدم` و`كلمة المرور` و`معرّف العميل`، ثم يتصل عبر `ConnectAsync`. يمكنه الاستهلاك عبر `SubscribeAsync` أو الإنتاج عبر `PublishAsync`.

## الاشتراكات المشتركة (EMQX)
يدعم اشتراكات EMQX المشتركة: يشترك عدة عملاء في الموضوع نفسه ويوزّع الوسيط الحمل. التنسيق: `$share/{group}/{topic}`.

```csharp
// الاشتراك في موضوع مشترك؛ عدة عملاء في المجموعة يستلمون بتوازن
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"رسالة مشتركة: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// النشر في الموضوع الفعلي (دون بادئة $share)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

المزايا:
- **موازنة الحمل**: توزيع متساوٍ داخل المجموعة
- **التوسع الأفقي**: مزيد من المشتركين يعني قدرة أكبر
- **التوافر العالي**: إذا فشل مشترك يكمل الآخرون

### تحديد إصدار بروتوكول MQTT
منذ الإصدار 2.3.2025.0212 يمكن اختيار MQTT 3.1 أو 3.1.1 أو 5.0 عبر الخاصية `Version`:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

الإصدارات المدعومة:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (افتراضي)
- `MqttVersion.V500` - MQTT 5.0

**ملاحظة**: دون تحديد صريح يُستخدم MQTT 3.1.1 للتوافق الخلفي.

## خادم مخصص
عند الحاجة إلى إدارة الاتصالات والرسائل على الخادم، يلزم خادم مخصص. مثلًا يمكن لمنصة IoT تخزين بيانات الأجهزة فور استلامها.

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

غيّر تسجيل المعالج:

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

## العنقود
إدارة العنقود: Join، Ping، Lease.
Join ينضم إلى العنقود ويعلن العقدة؛ يُستدعى N-1 مرة عند البدء.
Ping يحدّث وقت النشاط لكل العقد كل 15 ثانية.
Lease يغادر العنقود؛ يُستدعى N-1 مرة.
كل عقدة تزيل دوريًا العقد المنتهية حسب وقت النشاط.

## خريطة المنظومة

NewLife منظومة متعدّدة الطبقات من مشاريع مفتوحة المصدر تُصدَر بإصدارات مستقلة. الخريطة أدناه مشتركة حرفيًا بين جميع مستودعات NewLife؛ وكل مشروع يرتبط بمستودعه وبحزمته الرئيسية على NuGet.

| المشروع | الوظيفة | التنزيلات |
| --- | --- | --- |
| **الأساس** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | التسجيل والتهيئة والتخزين المؤقت والشبكات والتسلسل والجدولة وتتبّع أداء APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | وسيط بيانات: جداول واحدة بمستوى عشرات المليارات من الصفوف، تقسيم تلقائي للجداول، فصل القراءة عن الكتابة، وأكثر من 10 قواعد بيانات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | مكتبة شبكات عالية الأداء: 1.77 مليون pps / 14.5 Gbps على جهاز واحد (TCP 1KB، 2026-10)، و4 ملايين اتصال TCP طويل الأمد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | إطار اتصال بروتوكولي وRPC لتطبيقات العميل/الخادم وأجهزة إنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | يثبّت تطبيقات .NET كخدمات حارسة لنظام التشغيل: خدمات Windows وsystemd في Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | قوالب مشاريع `dotnet new` للطرفية وweb وAPI والخدمات والبيانات وإنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **البيانات والمراسلة** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | عميل Redis بزمن استجابة بالميكروثانية، وملايين العمليات في الثانية، وقوائم رسائل مدمجة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | عميل RocketMQ مُدار بالكامل لـ Apache RocketMQ وخدمة رسائل Alibaba Cloud. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | عميل وخادم MQTT، بما في ذلك الاتصال بـ Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | قاعدة بيانات هجينة بلغة C#: مدمجة أو شبكية، وعلائقية، وسلاسل زمنية، وقائمة رسائل، وKV في واحد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | برنامج تشغيل MySql وطبقة مساعدة مبنية على مكدّس بيانات NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web والذكاء الاصطناعي والأنظمة الموزّعة** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | إطار منصة إدارة سريعة: الصلاحيات، والدخول الموحّد SSO، وخادم OAuth، وتوليد واجهات CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | منصة خدمات موزّعة: إدارة العقد، وAPM، ومركز الإعدادات، ومركز التسجيل، ومركز النشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | منصة موزّعة للمهام وجدولة البيانات الفورية، مع التقسيم وإعادة المحاولة وإعادة التعبئة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | عميل موحّد لـ 46 مزوّد LLM بالإضافة إلى ChatAI، تطبيق ويب جاهز للمحادثة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **الصناعة وإنترنت الأشياء** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | مكتبة معايير إنترنت الأشياء التي تعرّف عقود البروتوكولات وتجريدات الأجهزة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | تنفيذ Modbus TCP/RTU/ASCII فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | بروتوكول Siemens PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | بروتوكول Omron PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | بروتوكول Mitsubishi Melsec PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | عميل OPC للوصول إلى البيانات الصناعية. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | مكدّس بروتوكول BACnet لأتمتة المباني. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | منصة إنترنت أشياء مفتوحة المصدر وخفيفة، مع إدارة البوابات والأجهزة. | — |
| **تليماتيكا المركبات** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | بروتوكول تحديد مواقع المركبات JT/T 808: أكثر من 70 جسم رسالة، 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | بروتوكول JT/T 809 بين المنصات لجهات إدارة النقل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | فيديو داخل المركبة JT/T 1078: تحليل RTP وإعادة التجميع إلى FLV/fMP4 والبث المباشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **مكوّنات المجال** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | حزمة واحدة لـ Excel وWord وPPT وPDF وأكثر من 15 صيغة مستندات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | صوت عبر المنصات: مرمّزات، وإدخال/إخراج الأجهزة، وDSP، وحاويات، وبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | مرمّزات الفيديو وصيغ الحاويات ومعالجة الإطارات والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | واجهة موحّدة فوق Baidu وAMap وTencent وTianditu مع الترميز الجغرافي وتحويل الإحداثيات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | تحديد الموقع الجغرافي لعناوين IPv4 بقاعدة بيانات غير متصلة مُعيّنة في الذاكرة، وواجهة API عبر الإنترنت كبديل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **الأدوات والمنتجات** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | صندوق أدوات المطوّر: الشبكات والمنفذ التسلسلي والتشفير والتعبيرات النمطية وModbus وMQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | نظام استضافة ذاتية لإدارة الشيفرة المصدرية Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | يثبّت بيئة تشغيل .NET المطابقة تلقائيًا عند التشغيل من مضيف أقدم. | — |

> تعرض الشارات الحزمة الرئيسية لكل مشروع؛ أما حزم السمات والإضافات والحزم التابعة فمذكورة في صفحاتها على NuGet.الحزم التي تتجاوز 30 حزمة وغير المذكورة هنا (FTP وLoRa وإضافات Office وبرامج تشغيل البروتوكولات وغيرها) متاحة في [منظمة NewLifeX](https://github.com/NewLifeX) وعلى [newlifex.com](https://newlifex.com).

## الفريق

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

تأسّست NewLife في عام 2002، وأمضت أكثر من عقدين في منظومة .NET وإنترنت الأشياء، وتدير ما يقارب 100 مشروع مفتوح المصدر (34 منها مشاريع رئيسية)، وتقدّم حلولًا متكاملة للبرمجيات والأجهزة لقطاعات الكهرباء والخدمات اللوجستية والرعاية الصحية والتحكّم الصناعي والإنترنت. ونلتزم بمبدأ "لا أسبقية في التعلّم — الإتقان يكسب الاحترام".

نُزّلت المكوّنات مفتوحة المصدر <!--nuget-total:start-->6.48M<!--nuget-total:end--> مرة من NuGet (على مستوى المنظومة بأكملها، حتى 2026-10).

الموقع: https://newlifex.com · المنظمة مفتوحة المصدر: https://github.com/NewLifeX

<details>
<summary>حساب WeChat الرسمي: 智能大石头 (امسح للاشتراك)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## الترخيص

MIT License © 2002-2026 فريق NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
