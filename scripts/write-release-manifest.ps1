[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][string]$SetupPath,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ReleaseLabel,
    [Parameter(Mandatory = $true)][ValidateSet('test', 'community', 'public')][string]$PackageKind,
    [string]$SignerThumbprint
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$commit = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    $commit = 'unknown'
}

$artifacts = @(
    [ordered]@{ role = 'setup'; file = [IO.Path]::GetFileName($SetupPath); sizeBytes = (Get-Item -LiteralPath $SetupPath).Length; sha256 = (Get-FileHash -LiteralPath $SetupPath -Algorithm SHA256).Hash.ToLowerInvariant() },
    [ordered]@{ role = 'msi'; file = [IO.Path]::GetFileName($InstallerPath); sizeBytes = (Get-Item -LiteralPath $InstallerPath).Length; sha256 = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant() }
)

$manifest = [ordered]@{
    schemaVersion = 2
    product = 'Otium'
    version = $Version
    releaseLabel = $ReleaseLabel
    architecture = 'win-x64'
    packageKind = $PackageKind
    configuration = if ($PackageKind -eq 'test') { 'Debug' } else { 'Release' }
    commit = $commit.Trim()
    signed = $PackageKind -eq 'public'
    signerThumbprint = if ($SignerThumbprint) { $SignerThumbprint.ToLowerInvariant() } else { $null }
    artifacts = $artifacts
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Release manifest: $OutputPath"
