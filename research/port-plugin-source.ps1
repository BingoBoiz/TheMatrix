# Port decompiled plugin source into the package with Unity-compatible (C# 9) syntax.
# Converts file-scoped namespaces (C# 10) to block-scoped (C# 9) so Unity 2022.3 can compile.
$ErrorActionPreference = 'Stop'
$dec = 'D:\Unity\TheMatrix\research\decompiled'
$dest = 'D:\Unity\TheMatrix\Packages\com.feeder.mcp\Runtime\Plugins\Feeder.Source'
$order = @('Feeder.ReflectorNet', 'Feeder.McpPlugin.Common', 'Feeder.McpPlugin')

foreach ($src in $order) {
    $from = Join-Path $dec $src
    $to   = Join-Path $dest $src
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    $files = Get-ChildItem $from -Recurse -Include *.cs | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }
    $converted = 0
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($from.Length + 1)
        $target = Join-Path $to $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        $content = [System.IO.File]::ReadAllText($f.FullName)
        if ($content -match '(?m)^namespace\s+([\w.]+);\s*$') {
            $ns = $Matches[1]
            $content = $content -replace '(?m)^namespace\s+[\w.]+;\s*$', "namespace $ns`n{"
            $content = $content.TrimEnd() + "`n}`n"
            $converted++
        }
        [System.IO.File]::WriteAllText($target, $content, (New-Object System.Text.UTF8Encoding($false)))
    }
    "ported $src -> $($files.Count) files ($converted namespace-converted)"
}

# --- asmdefs ---
$asmdefs = @{
    'Feeder.ReflectorNet.asmdef' = @{
        name = 'Feeder.ReflectorNet'; rootNamespace = 'Feeder.ReflectorNet'
        references = @()
        precompiledReferences = @('System.Text.Json.dll', 'Microsoft.Extensions.Logging.Abstractions.dll')
    }
    'Feeder.McpPlugin.Common.asmdef' = @{
        name = 'Feeder.McpPlugin.Common'; rootNamespace = 'Feeder.McpPlugin.Common'
        references = @('Feeder.ReflectorNet')
        precompiledReferences = @('System.Text.Json.dll', 'Microsoft.Extensions.Logging.Abstractions.dll', 'Microsoft.AspNetCore.SignalR.Protocols.Json.dll', 'R3.dll')
    }
    'Feeder.McpPlugin.asmdef' = @{
        name = 'Feeder.McpPlugin'; rootNamespace = 'Feeder.McpPlugin'
        references = @('Feeder.ReflectorNet', 'Feeder.McpPlugin.Common')
        precompiledReferences = @(
            'System.Text.Json.dll', 'Microsoft.Extensions.Logging.dll', 'Microsoft.Extensions.Logging.Abstractions.dll',
            'Microsoft.Extensions.DependencyInjection.dll', 'Microsoft.Extensions.DependencyInjection.Abstractions.dll',
            'Microsoft.Extensions.Options.dll', 'Microsoft.AspNetCore.SignalR.Client.dll', 'Microsoft.AspNetCore.SignalR.Client.Core.dll',
            'Microsoft.AspNetCore.SignalR.Common.dll', 'Microsoft.AspNetCore.SignalR.Protocols.Json.dll',
            'Microsoft.AspNetCore.Http.Connections.Client.dll', 'R3.dll', 'System.Runtime.CompilerServices.Unsafe.dll',
            'System.Text.Encodings.Web.dll')
    }
}
foreach ($name in $asmdefs.Keys) {
    $cfg = $asmdefs[$name]
    $json = @{
        name = $cfg.name; rootNamespace = $cfg.rootNamespace
        references = @($cfg.references)
        includePlatforms = @(); excludePlatforms = @()
        allowUnsafeCode = $false
        overrideReferences = $true
        precompiledReferences = @($cfg.precompiledReferences)
        autoReferenced = $true
        defineConstraints = @(); versionDefines = @()
        noEngineReferences = $false
    } | ConvertTo-Json -Depth 5
    $target = Join-Path $dest $name
    [System.IO.File]::WriteAllText($target, $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
    "wrote asmdef: $name"
}
"PORT DONE"
