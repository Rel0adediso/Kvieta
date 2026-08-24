[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'Otium') {
    throw 'The release manifest is not a supported Otium manifest.'
}

if ($manifest.version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.architecture -ne 'win-x64') {
    throw 'The release manifest contains an invalid version or architecture.'
}

$manifestDirectory = Split-Path -Parent $resolvedManifestPath
$packagePath = Join-Path $manifestDirectory ([string]$manifest.package)
$resolvedPackagePath = (Resolve-Path -LiteralPath $packagePath).Path
$package = Get-Item -LiteralPath $resolvedPackagePath

if ($package.Name -ne "Otium-$($manifest.version)-win-x64.msi") {
    throw 'The installer filename does not match the release manifest.'
}

if ($package.Length -ne [long]$manifest.sizeBytes) {
    throw 'The installer size does not match the release manifest.'
}

$actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne ([string]$manifest.sha256).ToLowerInvariant()) {
    throw 'The installer SHA-256 hash does not match the release manifest.'
}

[pscustomobject]@{
    PackagePath = $resolvedPackagePath
    Version = [string]$manifest.version
    Architecture = [string]$manifest.architecture
    SizeBytes = $package.Length
    Sha256 = $actualHash
}
