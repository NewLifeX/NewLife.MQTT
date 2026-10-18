# NewLife.MQTT - Vollständige MQTT-Protokollimplementierung

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.MQTT?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.MQTT?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.MQTT?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.MQTT?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.MQTT/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.MQTT?logo=github)](https://github.com/NewLifeX/NewLife.MQTT/graphs/contributors)

<!-- lang:start -->
🌐 [中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Diese Übersetzung kann hinter der chinesischen Version zurückliegen.

NewLife.MQTT ist ein rein verwalteter MQTT-3.1/3.1.1/5.0-Client und -Server mit QoS, Retained Messages, Will, Shared Subscriptions und TLS.

## Installation

```bash
dotnet add package NewLife.MQTT
```

## Schnellstart

```csharp
var client = new MqttClient("tcp://127.0.0.1:1883");
await client.ConnectAsync();
await client.SubscribeAsync("test/#");
```

## Funktionen

- Breite Abdeckung gängiger Protokoll- und Formatversionen
- Streaming- und Echtzeitkommunikationsfähigkeiten
- Kompatibel mit gängigen Cloud-Diensten und Managed Middleware

## Dokumentation

> 📖 Vollständige Dokumentation (Chinesisch): [Repository-Dokumente](Doc)

## Lizenz

MIT License © 2002-2026 NewLife-Team

[GitHub](https://github.com/NewLifeX/NewLife.MQTT) | [Website](https://newlifex.com)
