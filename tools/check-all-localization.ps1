# 一键执行本仓库全部本地化校验：词表、中文运行时消息、README 结构与同步。
#
# 用法：
#   pwsh -File tools/check-all-localization.ps1
param(
    [String]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$failed = @()

function Invoke-Check([String]$Name, [String[]]$Arguments) {
    Write-Host "=== $Name"
    $script = $Arguments[0]
    $rest = @($Arguments | Select-Object -Skip 1)
    & pwsh -NoProfile -File $script @rest
    if ($LASTEXITCODE -ne 0) { $script:failed += $Name }
}

$tools = Join-Path $Root "tools"

Invoke-Check "词表：NewLife.MQTT" @((Join-Path $tools "check-locales.ps1"), "-LocaleDir", "NewLife.MQTT\Locales", "-Baseline", "zh-Hans.yml", "-SourceDir", "NewLife.MQTT,NewLife.MQTT.Storage,NewLife.MqttServer", "-Strict")
Invoke-Check "词表：NewLife.MQTT.Storage" @((Join-Path $tools "check-locales.ps1"), "-LocaleDir", "NewLife.MQTT.Storage\Locales", "-Baseline", "zh-Hans.yml", "-SourceDir", "NewLife.MQTT,NewLife.MQTT.Storage,NewLife.MqttServer", "-Strict")
Invoke-Check "词表：NewLife.MqttServer" @((Join-Path $tools "check-locales.ps1"), "-LocaleDir", "NewLife.MqttServer\Locales", "-Baseline", "zh-Hans.yml", "-SourceDir", "NewLife.MQTT,NewLife.MQTT.Storage,NewLife.MqttServer", "-Strict")
Invoke-Check "中文运行时消息" @((Join-Path $tools "check-english-exceptions.ps1"))
Invoke-Check "README 结构" @((Join-Path $tools "check-readme-structure.ps1"), "-Root", $Root)
Invoke-Check "README 语言切换行" @((Join-Path $tools "sync-readme-languages.ps1"), "-Root", $Root, "-Check")
Invoke-Check "README NuGet 包版" @((Join-Path $tools "sync-readme-package.ps1"), "-Root", $Root, "-Check")

if ($failed.Count -gt 0) {
    Write-Host "`n本地化校验失败：$($failed -join '、')" -ForegroundColor Red
    exit 1
}

Write-Host "`n本地化校验全部通过" -ForegroundColor Green
