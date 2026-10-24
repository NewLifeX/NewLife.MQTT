# NewLife.MQTT - Implementação completa do protocolo MQTT

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Esta tradução pode estar desatualizada em relação ao [English README](README.md).

**NewLife.MQTT** é uma implementação completa do protocolo MQTT, **totalmente autodesenvolvida, sem dependências de terceiros e licenciada sob MIT**. Um único pacote NuGet fornece tanto um `MqttClient` completo quanto um `MqttServer` (broker) de alto desempenho, cobrindo todos os runtimes principais do .NET Framework 4.5 ao .NET 10.

## 📖 Navegação da documentação

| Documento | Descrição |
|------|------|
| [Requisitos](Doc/需求文档.md) | Visão, objetivos, requisitos dos 7 módulos principais, itens adiados e não objetivos |
| [Lista de recursos](Doc/功能清单.md) | Acompanhamento tridimensional de implementação, testes e comentários de 87 pontos funcionais |
| [Arquitetura](Doc/架构设计.md) | Arquitetura em cinco camadas, modelos de dados, interfaces, decisões-chave e desempenho |
| [Arquitetura do servidor](Doc/SRV-服务端架构.md) | Projeto do broker: roteamento, autenticação e API HTTP de administração |
| [Arquitetura de cluster](Doc/CLUS-集群架构.md) | Roteiro de cluster em quatro fases, consenso Raft, particionamento de tópicos e topologia multicamada |
| [Análise competitiva](Doc/竞品分析.md) | Comparação do ecossistema MQTT (EMQX/MQTTnet/Mosquitto) e SWOT |
| [Análise competitiva de cluster](Doc/CLUS-集群竞品分析.md) | Comparação específica de recursos de cluster (EMQX/HiveMQ/VerneMQ) |
| [Testes de integração](Doc/集成测试说明.md) | Arquitetura de testes, execução, categorias e compatibilidade cruzada (NewLife ↔ MQTTnet, obrigatória no CI) |
| [Compatibilidade AOT](Doc/AOT兼容性分析.md) | Medições NativeAOT: 19 cenários principais em JIT e AOT, matriz de RID |
| [Notas de versão](Doc/更新日志.md) | Histórico de versões e detalhes de commits |

---

## Recursos principais

| Recurso | Descrição |
|------|------|
| **Protocolo completo** | MQTT 3.1 / 3.1.1 / 5.0, 14 tipos de pacote + pacote AUTH, 30 propriedades do MQTT 5.0 |
| **Sem dependências** | Apenas NewLife.Core; nenhum pacote de terceiros na cadeia NuGet |
| **Cliente e servidor unificados** | Um único pacote NuGet `NewLife.MQTT`, sem builds separados |
| **Sete transportes** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP confiável / ProxyProtocol v1/v2 |
| **Alto desempenho** | Milhões de conexões por nó (4 milhões verificadas); broker com ~**1,25 M mensagens/s** de entrada e 106.000 msg/s ponta a ponta; roteamento constante de 120 ns por entrada |
| **Recursos empresariais grátis** | Cluster (Raft / particionamento) / ponte / mecanismo de regras / WebHook / $SYS / ACL |
| **Reassinatura automática** | Todos os tópicos são reassinados após reconexão, sem código extra |
| **Proxy SOCKS5** | Proxy SOCKS5 integrado com usuário/senha para TCP e WS |
| **Cobertura de versões** | net45 / net461 / netstandard2.0 / netstandard2.1 / net6 a net10 |
| **NativeAOT** | Caminhos principais verificados em NativeAOT (API, RPC de cluster, TCP, WS, autenticação, MQTT-SN; 19 cenários, 0 avisos ILC) |
| **Localização em 31 idiomas** | Mensagens de execução, painel e metadados do pacote são localizados em 31 idiomas pelas tabelas compiladas do NewLife.Core; entradas ausentes retornam ao inglês |
| **Barramento de eventos** | `MqttEventBus` oferece publicação/assinatura fortemente tipada sobre tópicos MQTT: difusão por padrão, assinatura compartilhada `$share` opcional e compatível com os contratos `IEventBusFactory` do NewLife.Core |
| **Conformidade** | Autodesenvolvido, MIT, sem copyleft ou cláusulas de patente |

---

## Vantagens diferenciais

### MQTT sobre QUIC (raro no setor)
Atualmente **apenas EMQX e NewLife.MQTT** suportam este transporte:
- Baixa latência, sem bloqueio de cabeça de fila, ideal para redes fracas e móveis
- .NET 7/8 exigem recursos de prévia; estável a partir do .NET 9
- Porta padrão 14567, identificador ALPN `"mqtt"`
- Caminho completo cliente/servidor verificado ponta a ponta (Linux/libmsquic, `QuicIntegrationTests`)

### MQTT sobre UDP confiável (primeiro do setor)
**Primeiro mundial, sem necessidade de msquic do sistema operacional, disponível em todas as plataformas**:
- Cabeçalho de quadro de 4 bytes + número de sequência + ACK + retransmissão por timeout
- Cobre plataformas embarcadas como ARM Linux 4.x sem msquic

### Reassinatura automática após reconexão
MQTTnet / Eclipse Paho exigem reassinatura manual; o NewLife.MQTT faz automaticamente:

```csharp
await client.SubscribeAsync("/device/+/data", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));
// Nenhum código extra após reconectar.
```

### Recursos empresariais integrados e gratuitos
- **Ponte de mensagens**: In / Out / Both, mapeamento de prefixo de tópico, limite de QoS
- **Mecanismo de regras**: 5 ações (Republish / WebHook / Bridge / Drop / Custom)
- **WebHook**: 6 tipos de evento assíncronos com repetição automática; integrado ao MqttHandler + Exchange
- **Cluster**: consenso Raft + particionamento por hash consistente + roteamento multicamada, sincronização de assinaturas e encaminhamento entre nós, porta RPC dedicada (2883)
- **MQTT-SN v1.2**: gateway para dispositivos restritos, interoperável com tópicos MQTT
- **Armazenamento persistente**: pacote opcional `NewLife.MQTT.Storage` com Redis / banco de dados para mensagens offline e sessões
- **Tópicos $SYS**: 14 tópicos padrão com estatísticas em tempo real
- **ProxyProtocol v1/v2**: preserva o IP real do cliente via nginx/HAProxy

---

## Início rápido

### Instalação

```bash
dotnet add package NewLife.MQTT
```

### Iniciar o servidor

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

### Conectar um cliente

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid().ToString(),
};
await client.ConnectAsync();

// Assinar (reassinatura automática após reconexão)
await client.SubscribeAsync("/test/#", e =>
    XTrace.WriteLine($"{e.Topic}: {e.Payload.ToStr()}"));

// Publicar
await client.PublishAsync("/test/hello", "Hello MQTT");
```

### Inicialização por cadeia de conexão

```csharp
var client = new MqttClient();
client.Init("Server=tcp://127.0.0.1:1883;UserName=admin;******;ClientId=myDevice");
await client.ConnectAsync();
```

---

## Uso detalhado do cliente

### Especificar a versão do protocolo

```csharp
var client = new MqttClient
{
    Server = "tcp://127.0.0.1:1883",
    ClientId = "myDevice",
    Version = MqttVersion.V500,   // V310 / V311 (padrão) / V500
};
```

### Publicação com QoS

```csharp
await client.PublishAsync("/sensor/temp", "25.6");                                 // QoS 0
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.AtLeastOnce);  // QoS 1
await client.PublishAsync("/sensor/temp", "25.6", QualityOfService.ExactlyOnce);  // QoS 2
```

### Semântica de QoS

A QoS efetiva de consumo é a **menor** entre a QoS de publicação e a de assinatura:

| QoS | Nome | Descrição |
|-----|------|------|
| 0 | No máximo uma vez | Sem confirmação; perdas possíveis, adequado a batimentos de sensores |
| 1 | Pelo menos uma vez | O broker responde PUBACK; duplicatas possíveis, adequado a dados importantes |
| 2 | Exatamente uma vez | Handshake de quatro etapas; sem perda nem duplicata, adequado a cobrança/pedidos |

### Assinaturas com curingas

```csharp
await client.SubscribeAsync("/device/+/data", handler);   // + corresponde a um nível
await client.SubscribeAsync("/device/#", handler);        // # corresponde a vários níveis
```

### Assinaturas compartilhadas (balanceamento de carga)

Formato `$share/{group}/{topic}`; vários consumidores do mesmo grupo recebem mensagens em rodízio:

```csharp
await client.SubscribeAsync("$share/workers/orders/new", e =>
    XTrace.WriteLine($"Recebido: {e.Payload.ToStr()}"));
```

### TLS / WebSocket / QUIC

```csharp
new MqttClient { Server = "ssl://broker.example.com:8883" };           // TLS
new MqttClient { Server = "ws://broker.example.com:8083" };            // WebSocket
new MqttClient { Server = "wss://broker.example.com:8084" };           // WSS
new MqttClient { Server = "quic://broker.example.com:14567" };         // QUIC (.NET 7+)
```

> Servidor WebSocket: desde a v12 é hospedado por um `HttpServer` separado (`EnableWebSocket = true`, `WebSocketPort` padrão 0 para atribuição do sistema, `WebSocketPath` padrão `/mqtt`) e não compartilha mais a porta TCP do MQTT; os clientes continuam conectando via `ws://host:port/mqtt`. `WebSocketCertificate` habilita TLS nessa porta, ou seja, `wss://host:port/mqtt`.

---

## Uso detalhado do servidor

### Manipulador personalizado

Herde de `MqttHandler` para interceptar todos os tipos de mensagem, ideal para plataformas IoT que armazenam dados diretamente:

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

### Autenticação personalizada (ACL)

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

## Recursos empresariais

### Ponte de mensagens

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

### Mecanismo de regras

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

### Implantação de cluster

Cluster de três nós (cada nó lista os endereços dos demais):

```csharp
var cluster = new ClusterServer
{
    Nodes = new[] { "192.168.1.11:2883", "192.168.1.12:2883" },
    ClusterPort = 2883,
};
cluster.Start();
services.AddSingleton<IMqttExchange, ClusterExchange>();
```

Operação: Join ao iniciar → Ping a cada 15 segundos → Lease ao encerrar; nós expirados (5 minutos) são removidos automaticamente.

> Desde a v12, o RPC de cluster usa HTTP (`ClusterPort` é uma porta HTTP; endereços podem ser `host:port` ou `http://host:port`), e `ClusterSecret` é verificado como token em cada chamada; o NewLife.Remoting não é mais necessário.

### Adaptador Alibaba Cloud IoT

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

## Experimente rapidamente

Abra o código-fonte e defina `Test` como projeto de inicialização para executar servidor e cliente ao mesmo tempo:

![Demo](Doc/Demo.png)

---

## Destaques

| Destaque | Descrição |
|------|------|
| 🚀 **Protocolo completo** | Suporte completo ao MQTT 3.1.1 (14 pacotes) + recursos principais do MQTT 5.0 (30 propriedades) |
| 🔒 **Sem dependências** | Apenas NewLife.Core; sem risco de cadeia de suprimentos nem conflitos de licença |
| ⚖️ **MIT** | Licença permissiva; uso comercial e modificação livres, sem copyleft nem patentes |
| 🎯 **Cliente + servidor** | Um pacote NuGet `NewLife.MQTT` para ambos; integração mínima |
| 🌐 **Sete transportes** | TCP / TLS / WebSocket / WSS / QUIC (.NET 7+) / UDP confiável / ProxyProtocol v1/v2 |
| 🏢 **Recursos empresariais grátis** | Cluster / ponte / mecanismo de regras / WebHook / estatísticas / $SYS |
| 🇨🇳 **Autodesenvolvido** | Propriedade intelectual completa, sem risco jurídico externo, pronto para conformidade |
| 🔧 **Cobertura de versões** | .NET Framework 4.5 / 4.6.1 / .NET Standard 2.0/2.1 / .NET 7/8/9/10 |

## Vantagens adicionais

### 1. MQTT sobre QUIC (raro)
- **Apenas EMQX e NewLife.MQTT suportam**
- Baixa latência, sem bloqueio de cabeça de fila
- Disponível no .NET 7+ (prévia no 7/8, estável no 9+)

### 2. MQTT sobre UDP confiável (autodesenvolvido)
- **Único no setor**
- Cabeçalho de 4 bytes + sequência + Ack + retransmissão por timeout
- Não exige QUIC/msquic do sistema; funciona em todas as plataformas

### 3. Recursos empresariais integrados e gratuitos
- **Ponte bidirecional:** In/Out/Both, prefixos de tópico, limite de QoS
- **Mecanismo de regras (5 ações):** Republish / WebHook / Bridge / Drop / Custom
- **WebHook:** 6 tipos de evento, assíncrono e não bloqueante, com repetição
- **Cluster:** configuração manual de nós, sincronização de assinaturas e encaminhamento
- **ProxyProtocol:** nginx/HAProxy v1/v2 para preservar o IP real
- **Tópicos $SYS:** 14 tópicos padrão com estatísticas em tempo real

### 4. Reassinatura automática após reconexão
Os concorrentes (MQTTnet / Eclipse Paho) exigem reassinatura manual; o NewLife.MQTT faz automaticamente.

### 5. Adaptador Alibaba Cloud IoT integrado
`AliyunMqttClient` funciona imediatamente: assinatura HMAC, relatório de propriedades e sincronização de relógio.

## Protocolo MQTT
MQTT é o protocolo de comunicação IoT mais popular, incluindo cliente, servidor e plataforma web de administração.

Oferece um modelo publicação/assinatura mais simples, leve e fácil de usar, voltado a ambientes restritos (baixa largura de banda, alta latência, rede instável). Em resumo, feito para a Internet das Coisas. Características oficiais:
1. Padrão publicação/assinatura com distribuição 1:n e desacoplamento de aplicações.
2. Transporte independente do conteúdo do payload.
3. Conectividade de rede TCP/IP.
4. Três níveis de QoS:
"no máximo uma vez": depende totalmente do TCP/IP; perdas ou duplicatas possíveis. Adequado a sensores ambientais.
"pelo menos uma vez": entrega garantida, duplicatas possíveis.
"exatamente uma vez": entrega exata; adequado a cobrança, onde duplicatas ou perdas gerariam resultados errados.
5. Transporte pequeno com baixíssima sobrecarga (cabeçalho fixo de 2 bytes) e troca mínima.
6. Last Will and Testament notifica interessados em desconexões anormais.

## Publicação e assinatura MQTT
Ao publicar, a QoS é especificada e armazenada pelo broker; ao assinar, a QoS de consumo é a menor entre publicação e assinatura.

Detalhes:
- Assinatura QoS=0: consumo sempre QoS 0;
- Assinatura QoS=1: publicação QoS=0 → consumo QoS=0; publicação QoS=1 ou 2 → consumo QoS=1;
- Assinatura QoS=2: consumo igual à QoS publicada;
- Publicação QoS=0: o broker não responde; perda intermediária passa despercebida, mas o TCP detecta falhas;
- Publicação QoS=1: o broker responde `PubAck`;
- Publicação QoS=2: o broker responde `PubRec`, o cliente envia `PubRel`, o broker responde `PubComp`; só então é publicada;
- Assinatura QoS=2: o broker envia QoS=2, o cliente responde `PubRec`, o broker envia `PubRel`, o cliente responde `PubComp`; só então é consumida;
- Em QoS=2 o consumidor não precisa estar online; o fluxo ocorre entre publicador e broker.

## Teste rápido
Abra a solução, defina Test como projeto de inicialização e execute. Por padrão, TestServer e TestClient iniciam em sequência.

![Demo](Doc/Demo.png)

## Servidor
Referencie `NewLife.MQTT` do NuGet e inicie o servidor:

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

Com a porta 1883, o manipulador padrão `MqttHandler` e o exchange `MqttExchange`, o servidor inicia.

## Cliente
Referencie `NewLife.MQTT` do NuGet e conecte:

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

// Assinar o tópico "/test"
var rt = await client.SubscribeAsync("/test", (e) =>
{
    XTrace.WriteLine("Received: " + "/test/# =>" + e.Topic + ":" + e.Payload.ToStr());
}).ConfigureAwait(false);

// Publicar a cada 2 segundos em "/test"
while (true)
{
    try
    {
        var msg = "O aprendizado não tem ordem fixa; quem chega primeiro é o mestre" + Rand.NextString(8);
        await client.PublishAsync("/test", msg).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        XTrace.WriteException(ex);
    }
    await Task.Delay(2000).ConfigureAwait(false);
}
```

Um cliente precisa de `endereço do servidor`, `usuário`, `senha` e `identificador do cliente`; depois conecta com `ConnectAsync`. Pode consumir com `SubscribeAsync` ou produzir com `PublishAsync`.

## Assinaturas compartilhadas (EMQX)
Suporta assinaturas compartilhadas do EMQX: vários clientes assinam o mesmo tópico compartilhado e o broker distribui a carga. Formato: `$share/{group}/{topic}`.

```csharp
// Assinar um tópico compartilhado; clientes do grupo recebem com balanceamento
await client.SubscribeAsync("$share/group1/Yh/Drive/+/OrderLock", (e) =>
{
    XTrace.WriteLine($"Mensagem compartilhada: {e.Topic} => {e.Payload.ToStr()}");
}).ConfigureAwait(false);

// Publicar no tópico real (sem o prefixo $share)
await client.PublishAsync("Yh/Drive/1111/OrderLock", "Order locked").ConfigureAwait(false);
```

Vantagens:
- **Balanceamento de carga**: distribuição uniforme no grupo
- **Escala horizontal**: mais assinantes, mais capacidade
- **Alta disponibilidade**: se um assinante falhar, os demais continuam

### Especificar a versão do protocolo MQTT
Desde a versão 2.3.2025.0212, o cliente permite escolher MQTT 3.1, 3.1.1 ou 5.0 pela propriedade `Version`:

```csharp
var client = new MqttClient
{
    Log = XTrace.Log,
    Server = "tcp://127.0.0.1:1883",
    ClientId = Guid.NewGuid() + "",
    Version = MqttVersion.V500, // MQTT 5.0
};
```

Versões suportadas:
- `MqttVersion.V310` - MQTT 3.1
- `MqttVersion.V311` - MQTT 3.1.1 (padrão)
- `MqttVersion.V500` - MQTT 5.0

**Observação**: sem valor explícito, o MQTT 3.1.1 é usado por compatibilidade.

## Servidor personalizado
Quando é necessário controlar conexões e mensagens no servidor, um servidor personalizado é necessário. Por exemplo, uma plataforma IoT pode armazenar os dados reportados diretamente.

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

Altere o registro do manipulador:

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
Gerenciamento de cluster: Join, Ping, Lease.
Join entra no cluster e anuncia o nó; chamado N-1 vezes na inicialização.
Ping atualiza o tempo ativo de todos os nós a cada 15 segundos.
Lease sai do cluster; chamado N-1 vezes.
Cada nó remove periodicamente os nós cujo tempo ativo expirou.

## Mapa do ecossistema

NewLife é um ecossistema em camadas de projetos open source versionados de forma independente. O mapa abaixo é compartilhado literalmente por todos os repositórios NewLife; cada projeto tem links para o seu repositório e para o seu pacote principal no NuGet.

| Projeto | O que faz | Downloads |
| --- | --- | --- |
| **Base** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Log, configuração, cache, rede, serialização, agendamento e rastreamento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de dados: tabelas únicas na escala de dezenas de bilhões de linhas, particionamento automático, separação de leitura e escrita, mais de 10 bancos de dados. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de rede de alto desempenho: 1,77M pps / 14,5 Gbps em uma máquina (TCP 1KB, 2026-10), 4M de conexões TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicação por protocolo e RPC para aplicações cliente/servidor e dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicações .NET como daemons do sistema operacional: serviços do Windows e systemd do Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modelos de projeto `dotnet new` para console, web, API, serviço, dados e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Dados e mensagens** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis com latência em microssegundos, milhões de operações por segundo e filas de mensagens integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente gerenciado para Apache RocketMQ e Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente e servidor MQTT, incluindo conectividade com o Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Banco de dados híbrido em C#: embarcado ou em rede, relacional, séries temporais, fila de mensagens e KV em um só. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Driver MySql e camada auxiliar construídos sobre a pilha de dados NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA e distribuído** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework de plataforma administrativa rápida: permissões, SSO, servidor OAuth e scaffolding CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma de serviços distribuídos: gestão de nós, APM, centro de configuração, registro e centro de publicação. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma distribuída de tarefas e agendamento de dados em tempo real, com particionamento, repetição e reprocessamento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 provedores de LLM, além do ChatAI, uma aplicação web de chat pronta para uso. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca padrão de IoT que define contratos de protocolo e abstrações de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementação Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo Siemens PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo Omron PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo Mitsubishi Melsec PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para acesso a dados industriais. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pilha do protocolo de automação predial BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código aberto leve, com gestão de gateways e dispositivos. | — |
| **Telemática veicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamento veicular JT/T 808: mais de 70 corpos de mensagem, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma-a-plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo veicular JT/T 1078: análise RTP, remux para FLV/fMP4 e transmissão ao vivo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de domínio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Um pacote para Excel, Word, PPT, PDF e mais de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Áudio multiplataforma: codecs, E/S de dispositivos, DSP, contêineres e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs de vídeo, formatos de contêiner, processamento de frames e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unificada sobre Baidu, AMap, Tencent e Tianditu, com geocodificação e conversão de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalização IPv4 com banco de dados offline mapeado em memória e API online como alternativa. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Ferramentas e produtos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caixa de ferramentas do desenvolvedor: rede, porta serial, criptografia, regex, Modbus e MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema auto-hospedado de gerenciamento de código-fonte Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automaticamente o runtime .NET correspondente quando iniciado em um host mais antigo. | — |

> Os badges mostram o pacote principal de cada projeto; pacotes de tema, extensão e satélites estão listados nas respectivas páginas do NuGet.Os mais de 30 pacotes não listados aqui (FTP, LoRa, extensões do Office, drivers de protocolo e outros) estão disponíveis na [organização NewLifeX](https://github.com/NewLifeX) e em [newlifex.com](https://newlifex.com).

## Equipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Fundada em 2002, a equipe NewLife dedica-se há mais de duas décadas ao ecossistema .NET e de IoT, mantém cerca de 100 projetos de código aberto (34 deles principais) e oferece soluções integradas de software e hardware para os setores de energia, logística, saúde, controle industrial e internet. Seguimos o lema "não há hierarquia no aprendizado — o domínio conquista o respeito".

Os componentes de código aberto já foram baixados <!--nuget-total:start-->6.48M<!--nuget-total:end--> vezes no NuGet (todo o ecossistema, até 2026-10).

Website: https://newlifex.com · Organização de código aberto: https://github.com/NewLifeX

<details>
<summary>Conta oficial no WeChat: 智能大石头 (escaneie para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licença

MIT License © 2002-2026 Equipe NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
