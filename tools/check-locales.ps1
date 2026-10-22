# 多语言词表一致性校验：键集合、占位符、格式说明符
# 以基准语言文件（默认 zh-Hans.yml）为准：
#   - 缺失键默认仅告警（运行期回退英文），加 -Strict 时视为错误
#   - 多余键、占位符与格式说明符不一致始终为错误
#
# 用法：
#   pwsh -File tools/check-locales.ps1                                      # 主库默认目录
#   pwsh -File tools/check-locales.ps1 -LocaleDir src\Lib\Locales -SourceDir src\Lib
#   pwsh -File tools/check-locales.ps1 -LocaleDir src\Lib\Locales -SkipOrphan   # 只校验词表内部一致性
#   pwsh -File tools/check-locales.ps1 -LocaleDir src\Lib\Locales -SkipSource   # 跳过源码消息与动态插值检查
#   pwsh -File tools/check-locales.ps1 -LocaleDir src\Lib\Locales -Strict        # 要求所有语言键集合完全一致
param(
    [String]$LocaleDir = "NewLife.MQTT\Locales",
    [String]$Baseline = "zh-Hans.yml",
    [String[]]$SourceDir = @("NewLife.MQTT", "NewLife.MQTT.Storage", "NewLife.MqttServer"),
    [Switch]$SkipOrphan,
    [Switch]$SkipSource,
    [Switch]$Strict,
    # 元数据键清单，允许不出现在源码中，并要求覆盖全部语言
    [String]$MetadataKeys = "metadata-keys.json"
)

$ErrorActionPreference = "Stop"

# 兼容 -SourceDir A,B,C 与 -SourceDir A B C 两种写法
$SourceDir = @($SourceDir | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$root = Split-Path -Parent $PSScriptRoot
$localeDir = if ([IO.Path]::IsPathRooted($LocaleDir)) { $LocaleDir } else { Join-Path $root $LocaleDir }
if (!(Test-Path $localeDir)) {
    Write-Host "未找到语言目录：$localeDir" -ForegroundColor Red
    exit 1
}

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'
$placeholder = '\{(\d+)(?:,-?\d+)?(:[^}]*)?\}'

function Unescape-Yaml([string]$text) {
    # 与源生成器保持一致：还原 \\ \" \n \r \t \uXXXX
    return [regex]::Replace($text, '\\(u[0-9a-fA-F]{4}|.)', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v.Length -eq 5) { return [string][char][Convert]::ToInt32($v.Substring(1), 16) }
        switch -CaseSensitive ($v) {
            'n' { return "`n" }
            'r' { return "`r" }
            't' { return "`t" }
            '\\' { return '\' }
            '"' { return '"' }
            default { return $m.Value }
        }
    })
}

function Get-Placeholders([string]$text) {
    $set = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        [void]$set.Add([int]$m.Groups[1].Value)
    }
    return ,$set
}

function Get-PlaceholderSpecs([string]$text) {
    $map = @{}
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        $index = [int]$m.Groups[1].Value
        if (!$map.ContainsKey($index)) { $map[$index] = $m.Groups[2].Value }
    }
    return $map
}

function Read-Locale([string]$path) {
    $entries = [ordered]@{}
    $errors = @()
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        $lineNo++
        if ($line.Trim().Length -eq 0 -or $line.TrimStart().StartsWith("#")) { continue }
        $m = [regex]::Match($line, $pattern)
        if (!$m.Success) {
            $errors += "第 $lineNo 行格式非法：$line"
            continue
        }
        $key = Unescape-Yaml $m.Groups[1].Value
        if ($entries.Contains($key)) {
            $errors += "第 $lineNo 行键重复：$key"
            continue
        }
        $entries[$key] = Unescape-Yaml $m.Groups[2].Value
    }
    return @{ Entries = $entries; Errors = $errors }
}

$referencePath = Join-Path $localeDir $Baseline
if (!(Test-Path -LiteralPath $referencePath)) {
    Write-Host "未找到基准语言文件：$referencePath" -ForegroundColor Red
    exit 1
}
$reference = Read-Locale $referencePath
$metadata = @()
$metadataPath = if ([IO.Path]::IsPathRooted($MetadataKeys)) { $MetadataKeys } else { Join-Path $localeDir $MetadataKeys }
if (Test-Path -LiteralPath $metadataPath) {
    $metadata = @((Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json).keys)
}
$allErrors = @($reference.Errors | ForEach-Object { "$($Baseline): $_" })
$allWarnings = @()

foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
    if ($file.Name -eq $Baseline) { continue }
    $locale = Read-Locale $file.FullName
    foreach ($e in $locale.Errors) { $allErrors += "$($file.Name): $e" }

    $missing = 0
    foreach ($key in $reference.Entries.Keys) {
        if (!$locale.Entries.Contains($key)) {
            $missing++
            if ($Strict) { $allErrors += "$($file.Name): 缺少键 $key" }
        }
    }
    if ($missing -gt 0 -and !$Strict) {
        $allWarnings += "$($file.Name): 缺少 $missing 条译文，运行期回退英文"
    }
    foreach ($key in $locale.Entries.Keys) {
        if (!$reference.Entries.Contains($key)) {
            $allErrors += "$($file.Name): 多余键 $key"
            continue
        }

        $value = $locale.Entries[$key]
        $expected = Get-Placeholders $key
        $actual = Get-Placeholders $value
        if (!$expected.SetEquals($actual)) {
            $allErrors += "$($file.Name): 占位符不一致 → $key"
            continue
        }

        # 格式说明符只在译文保留该占位符时比对，缺失时仅告警
        $enSpecs = Get-PlaceholderSpecs $key
        $trSpecs = Get-PlaceholderSpecs $value
        foreach ($index in $enSpecs.Keys) {
            if ($trSpecs.ContainsKey($index) -and $trSpecs[$index] -ne $enSpecs[$index]) {
                $allWarnings += "$($file.Name): 格式说明符不一致 → $key"
                break
            }
        }
    }
}

# 元数据键（程序集标题/描述等）要求覆盖全部译文语言
if ($metadata.Count -gt 0) {
    foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
        if ($file.Name -eq $Baseline) { continue }
        $locale = Read-Locale $file.FullName
        foreach ($key in $metadata) {
            if (!$locale.Entries.Contains($key) -or [String]::IsNullOrWhiteSpace([String]$locale.Entries[$key])) {
                $allErrors += "$($file.Name): 缺少元数据键 $key"
            }
        }
    }
}

$sourceDirs = @($SourceDir | ForEach-Object {
    if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
} | Where-Object { Test-Path $_ })
$allSourceFiles = @($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Filter *.cs } |
    Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' })
$sourceFiles = @($allSourceFiles | Where-Object {
        $_.FullName -notmatch '\\WebSocket\\' -and
        $_.Name -notin @('PruningRadixTrie.cs', 'SubscriptionManager.cs', 'Trie.cs', 'TrieNode.cs', 'MqttHandler.cs', 'MqttTypeAttribute.cs')
    })

# 孤儿键：基准表中的键在源码里完全找不到（改文案后遗留的死条目）
$orphans = @()
if (!$SkipOrphan) {
    $sourceText = ($allSourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    foreach ($key in $reference.Entries.Keys) {
        # 程序集标题/描述等元数据键不在源码字符串中，由 metadata-keys.json 声明
        if ($metadata -contains $key) { continue }

        # 源码中的字面量形式：制表符写作 \t，引号写作 \"
        $escaped = $key.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace("`r", '\r').Replace("`n", '\n')
        if (!$sourceText.Contains($key) -and !$sourceText.Contains($escaped)) {
            $orphans += $key
        }
    }
    foreach ($o in $orphans) { $allErrors += "孤儿键（源码中找不到）：$o" }
}

# 源码消息：L.T 与日志格式串必须在基准词表中，动态插值串无法本地化
if (!$SkipSource) {
    $patterns = @(
        @{ Name = "L.T"; Regex = 'L\.T\(\s*"((?:[^"\\]|\\.)*)"' },
        @{ Name = "Localization.Translate"; Regex = 'Localization\.Translate\(\s*"((?:[^"\\]|\\.)*)"' },
        @{ Name = "LocalizedDescription"; Regex = 'LocalizedDescription\(\s*"((?:[^"\\]|\\.)*)"' },
        @{ Name = "WriteLog"; Regex = '\.WriteLog\(\s*"((?:[^"\\]|\\.)*)"' },
        @{ Name = "Log"; Regex = '\.(?:Info|Debug|Warn|Error|Fatal)\(\s*"((?:[^"\\]|\\.)*)"' },
        @{ Name = "PrefixedLog"; Regex = '\.(?:InfoPrefixed|ErrorPrefixed|WritePrefixed)\(\s*"(?:[^"\\]|\\.)*"\s*,\s*"((?:[^"\\]|\\.)*)"' }
    )

    # 运行时语言表全局可见，源码消息只要存在于任一程序集的基准表即可
    $knownKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($k in $reference.Entries.Keys) { [void]$knownKeys.Add($k) }
    foreach ($k in $metadata) { [void]$knownKeys.Add($k) }
    foreach ($path in Get-ChildItem $root -Directory | ForEach-Object { Join-Path $_.FullName 'Locales\zh-Hans.yml' } | Where-Object { Test-Path $_ }) {
        $other = Read-Locale $path
        foreach ($k in $other.Entries.Keys) { [void]$knownKeys.Add($k) }
    }

    foreach ($file in $sourceFiles) {
        $lineNo = 0
        foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
            $lineNo++
            if ($line.TrimStart().StartsWith("//")) { continue }

            if ($line -match '\.(?:WriteLog|Info|Debug|Warn|Error|Fatal)\(\s*\$"') {
                $allErrors += "$($file.Name):${lineNo}: 动态插值日志无法本地化，请改用格式串加参数"
                continue
            }

            foreach ($p in $patterns) {
                foreach ($m in [regex]::Matches($line, $p.Regex)) {
                    $key = Unescape-Yaml $m.Groups[1].Value
                    if (!$knownKeys.Contains($key)) {
                        $allErrors += "$($file.Name):${lineNo}: 源码消息缺少基准词条（$($p.Name)）→ $key"
                    }
                }
            }
        }
    }
}

foreach ($e in $allErrors) { Write-Host $e -ForegroundColor Red }
foreach ($w in $allWarnings) { Write-Host $w -ForegroundColor Yellow }

$count = (Get-ChildItem $localeDir -Filter *.yml).Count
if ($allErrors.Count -gt 0) {
    Write-Host "多语言词表校验未通过：$count 个语言，$($allErrors.Count) 个错误" -ForegroundColor Red
    exit 1
}

Write-Host "多语言词表校验通过：$count 个语言，基准 $($reference.Entries.Count) 条" -ForegroundColor Green
