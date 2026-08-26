[CmdletBinding()]
param(
    [string]$AssemblyPath = 'src\Otium.App\bin\Release\net10.0-windows\Otium.dll'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedAssemblyPath = if ([System.IO.Path]::IsPathRooted($AssemblyPath)) {
    $AssemblyPath
} else {
    Join-Path $repositoryRoot $AssemblyPath
}

if (-not (Test-Path -LiteralPath $resolvedAssemblyPath -PathType Leaf)) {
    throw "Public assembly was not found: $resolvedAssemblyPath"
}

$bytes = [System.IO.File]::ReadAllBytes($resolvedAssemblyPath)
$assemblyHex = [System.Convert]::ToHexString($bytes)
$forbiddenMarkers = @(
    'ForceUnlockForTestingAsync',
    'ForceApplyPendingForTestingAsync',
    'Test bypass',
    'Test atlaması',
    'OTIUM_DEVELOPMENT_BUILD'
)

foreach ($marker in $forbiddenMarkers) {
    $patterns = @(
        [System.Text.Encoding]::UTF8.GetBytes($marker),
        [System.Text.Encoding]::Unicode.GetBytes($marker)
    )

    foreach ($pattern in $patterns) {
        $patternHex = [System.Convert]::ToHexString($pattern)
        if ($assemblyHex.Contains($patternHex, [System.StringComparison]::Ordinal)) {
            throw "Development-only marker '$marker' was found in the public assembly."
        }
    }
}

Write-Output "Public build verification passed: $resolvedAssemblyPath"
