# 工具脚本

本目录存放仓库级校验与同步脚本。CI 直接调用这里的脚本，根目录不再保留 `.ps1`。

| 脚本 | 用途 |
|------|------|
| `check-locales.ps1` | 词表一致性校验：键集合、占位符、格式说明符、孤儿键、源码消息与动态插值日志 |
| `sync-readme-languages.ps1` | 按 `readme-languages.json` 重写各 README 的语言切换行，`-Check` 只校验 |
| `sync-readme-package.ps1` | 从 `README.md` 生成 `README.nuget.md`，`-Check` 只校验 |

## 标准命令

```powershell
# 词表（主库、存储、服务端逐目录执行）
pwsh -File tools/check-locales.ps1 -LocaleDir NewLife.MQTT/Locales -Baseline zh-Hans.yml -SourceDir NewLife.MQTT,NewLife.MQTT.Storage,NewLife.MqttServer

# README 语言切换行
pwsh -File tools/sync-readme-languages.ps1 -Check

# NuGet 包版 README
pwsh -File tools/sync-readme-package.ps1 -Check
```

## 约定

- 英文原文是词表键与唯一兜底，`Locales/` 下不建 `en.yml`。
- 词表键必须与源码字符串逐字一致，占位符 `{0}` 与格式说明符 `{1:n0}` 必须一致。
- 新增消息时先写英文源文，再补中文；其它语言由统一批次补齐。
- README 以英文 `README.md` 为基线，11 种语言的结构、徽章、代码块与链接保持一致。
