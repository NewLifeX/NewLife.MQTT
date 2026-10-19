# NewLife.MQTT - Complete MQTT protocol implementation

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
🌐 [中文](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> This translation may lag behind the Chinese version.

NewLife.MQTT is a pure managed MQTT 3.1/3.1.1/5.0 client and server with QoS, retained messages, will, shared subscriptions and TLS, compatible with Alibaba Cloud IoT.

## Installation

```bash
dotnet add package NewLife.MQTT
```

## Quick start

```csharp
var client = new MqttClient("tcp://127.0.0.1:1883");
await client.ConnectAsync();
await client.SubscribeAsync("test/#");
```

## Key features

- Broad coverage of mainstream protocol and format versions
- Streaming and real-time communication capabilities
- Works with mainstream cloud services and managed middleware

## Documentation

> 📖 Full documentation (Chinese): [repository docs](Doc)

## License

MIT License © 2002-2026 NewLife Team

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
