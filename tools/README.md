# 工具脚本

本目录存放仓库级校验与同步脚本。CI 直接调用这里的脚本，根目录不再保留 `.ps1`。

| 脚本 | 用途 |
|------|------|
| `check-locales.ps1` | 词表一致性校验：键集合、占位符、格式说明符、孤儿键、源码消息与动态插值日志 |
| `check-english-exceptions.ps1` | 检查异常、日志、控制台与界面提示中是否残留中文运行时消息 |
| `sync-readme-languages.ps1` | 按 `readme-languages.json` 重写各 README 的语言切换行，`-Check` 只校验 |
| `sync-readme-package.ps1` | 从 `README.md` 生成 `README.nuget.md`，`-Check` 只校验 |
| `check-readme-structure.ps1` | 校验各译文与英文基线的章节层级、代码块数量与相对链接 |
| `check-all-localization.ps1` | 一键执行以上全部校验 |

## 标准命令

```powershell
# 一键执行全部本地化校验
pwsh -File tools/check-all-localization.ps1

# 词表（主库、存储、服务端逐目录执行）
pwsh -File tools/check-locales.ps1 -LocaleDir NewLife.MQTT/Locales -Baseline zh-Hans.yml -SourceDir NewLife.MQTT,NewLife.MQTT.Storage,NewLife.MqttServer

# 中文运行时消息
pwsh -File tools/check-english-exceptions.ps1

# README 语言切换行
pwsh -File tools/sync-readme-languages.ps1 -Check

# NuGet 包版 README
pwsh -File tools/sync-readme-package.ps1 -Check

# README 结构（章节层级与代码块）
pwsh -File tools/check-readme-structure.ps1
```

## 约定

- 英文原文是词表键与唯一兜底，`Locales/` 下不建 `en.yml`。
- 词表键必须与源码字符串逐字一致，占位符 `{0}` 与格式说明符 `{1:n0}` 必须一致。
- 新增消息时先写英文源文，再补中文；其它语言由统一批次补齐。
- 技术性同形词登记在 `Locales/identical.allow.json`，其余条目禁止与英文源文相同，单元测试会逐条校验。
- README 以英文 `README.md` 为基线，11 种语言的结构、徽章、代码块与链接保持一致。
