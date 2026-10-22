# README 多语言结构校验：各译文必须与英文基线拥有相同的章节层级与代码块数量。
# 只比较结构，不比较文字；允许译文额外包含 License 等本地化章节。
#
# 用法：
#   pwsh -File tools/check-readme-structure.ps1
#   pwsh -File tools/check-readme-structure.ps1 -Root D:\src\MyLib
param(
    [String]$Root = (Split-Path -Parent $PSScriptRoot),
    [String]$Config = "readme-languages.json"
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $Root $Config
if (!(Test-Path -LiteralPath $configPath)) { throw "未找到配置：$configPath" }

$langs = @((Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json).languages)
$baseline = Join-Path $Root $langs[0].file
if (!(Test-Path -LiteralPath $baseline)) { throw "未找到英文基线：$baseline" }

function Get-Structure([String]$path) {
    $lines = Get-Content -LiteralPath $path -Encoding UTF8
    $levels = @{ 1 = 0; 2 = 0; 3 = 0; 4 = 0 }
    $fences = 0
    $links = @()
    foreach ($line in $lines) {
        if ($line -match '^```') { $fences++; continue }
        if ($line -match '^(#{1,4})\s+') {
            $level = $Matches[1].Length
            $levels[$level]++
        }
        foreach ($m in [regex]::Matches($line, '!?\[[^\]]*\]\(([^)]+)\)')) {
            $target = $m.Groups[1].Value.Trim()
            if ($target -match '^(https?://|mailto:|#|//)') { continue }
            # 去掉可选标题与锚点
            $target = ($target -split '\s+')[0].Trim('<', '>')
            $hash = $target.IndexOf('#')
            if ($hash -ge 0) { $target = $target.Substring(0, $hash) }
            if ($target) { $links += $target }
        }
    }
    return [pscustomobject]@{ Levels = $levels; Fences = $fences; Links = $links }
}

$expected = Get-Structure $baseline
$errors = 0

foreach ($lang in $langs | Select-Object -Skip 1) {
    $path = Join-Path $Root $lang.file
    if (!(Test-Path -LiteralPath $path)) { throw "缺少译文：$($lang.file)" }

    $actual = Get-Structure $path
    $issues = @()

    if ($actual.Levels[1] -ne 1) { $issues += "H1 数量为 $($actual.Levels[1])，应为 1" }
    # 译文允许额外增加 License 等 H2 章节，但不得少于英文基线
    if ($actual.Levels[2] -lt $expected.Levels[2]) { $issues += "H2 数量 $($actual.Levels[2]) 少于基线 $($expected.Levels[2])" }
    foreach ($level in 3, 4) {
        if ($actual.Levels[$level] -ne $expected.Levels[$level]) {
            $issues += "H$level 数量 $($actual.Levels[$level]) 与基线 $($expected.Levels[$level]) 不一致"
        }
    }
    if ($actual.Fences -ne $expected.Fences) { $issues += "代码块标记 $($actual.Fences) 与基线 $($expected.Fences) 不一致" }

    foreach ($link in $actual.Links | Sort-Object -Unique) {
        $full = Join-Path $Root $link
        if (!(Test-Path -LiteralPath $full)) { $issues += "相对链接不存在：$link" }
    }

    if ($issues.Count -gt 0) {
        $errors++
        Write-Host "✗ $($lang.file)：$($issues -join '；')" -ForegroundColor Red
    }
    else {
        Write-Host "✓ $($lang.file)"
    }
}

if ($errors -gt 0) {
    Write-Host "`nREADME 结构校验失败：$errors / $($langs.Count - 1) 个译文与英文基线不一致。" -ForegroundColor Red
    exit 1
}

Write-Host "README 结构一致（$($langs.Count) 个语种）"
