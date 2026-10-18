# NewLife.MQTT - Implementación completa del protocolo MQTT

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.MQTT?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.MQTT?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.MQTT?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.MQTT?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.MQTT?logo=github)](https://github.com/NewLifeX/NewLife.MQTT/graphs/contributors)

<!-- lang:start -->
🌐 [中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Esta traducción puede estar desactualizada; consulte la versión en chino.

NewLife.MQTT es un cliente y servidor MQTT 3.1/3.1.1/5.0 totalmente administrado con QoS, mensajes retenidos, testamento, suscripciones compartidas y TLS.

## Instalación

```bash
dotnet add package NewLife.MQTT
```

## Inicio rápido

```csharp
var client = new MqttClient("tcp://127.0.0.1:1883");
await client.ConnectAsync();
await client.SubscribeAsync("test/#");
```

## Características

- Amplia cobertura de versiones de protocolos y formatos
- Capacidades de streaming y comunicación en tiempo real
- Compatible con servicios cloud y middleware gestionado

## Documentación

> 📖 Documentación completa (en chino): [documentos del repositorio](Doc)

## Licencia

MIT License © 2002-2026 Equipo NewLife

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
