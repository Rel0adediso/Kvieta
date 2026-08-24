[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ManifestPath,

    [ValidatePattern('^(?:[A-Fa-f0-9]\s*){40}$')]
    [string]$TrustedSignerThumbprint
)

$ErrorActionPreference = 'Stop'

function Get-NormalizedThumbprint([string]$Thumbprint) {
    return ($Thumbprint -replace '\s', '').ToUpperInvariant()
}

function Get-VerificationSignerThumbprint {
    if (-not [string]::IsNullOrWhiteSpace($TrustedSignerThumbprint)) {
        return Get-NormalizedThumbprint $TrustedSignerThumbprint
    }

    $scriptSignature = Get-AuthenticodeSignature -LiteralPath $PSCommandPath
    if ($scriptSignature.Status -ne 'Valid' -or $null -eq $scriptSignature.SignerCertificate) {
        throw 'The verifier is not Authenticode-signed. Supply a trusted signer thumbprint explicitly for development verification.'
    }

    return Get-NormalizedThumbprint $scriptSignature.SignerCertificate.Thumbprint
}

$trustedSigner = Get-VerificationSignerThumbprint
$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'Otium') {
    throw 'The release manifest is not a supported Otium manifest.'
}

if ($manifest.version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.architecture -ne 'win-x64') {
    throw 'The release manifest contains an invalid version or architecture.'
}

$manifestDirectory = Split-Path -Parent $resolvedManifestPath
$expectedPackageName = "Otium-$($manifest.version)-win-x64.msi"
if ([string]$manifest.package -cne $expectedPackageName) {
    throw 'The installer filename does not match the release manifest.'
}

$packagePath = Join-Path $manifestDirectory ([string]$manifest.package)
$resolvedPackagePath = (Resolve-Path -LiteralPath $packagePath).Path
$package = Get-Item -LiteralPath $resolvedPackagePath

if ($package.Name -cne $expectedPackageName) {
    throw 'The installer filename does not match the release manifest.'
}

if ($package.Length -ne [long]$manifest.sizeBytes) {
    throw 'The installer size does not match the release manifest.'
}

$actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne ([string]$manifest.sha256).ToLowerInvariant()) {
    throw 'The installer SHA-256 hash does not match the release manifest.'
}

if ([string]::IsNullOrWhiteSpace([string]$manifest.signerThumbprint) -or
    (Get-NormalizedThumbprint ([string]$manifest.signerThumbprint)) -ne $trustedSigner) {
    throw 'The release manifest signer does not match the trusted Otium signer.'
}

$packageSignature = Get-AuthenticodeSignature -LiteralPath $resolvedPackagePath
if ($packageSignature.Status -ne 'Valid' -or $null -eq $packageSignature.SignerCertificate -or
    (Get-NormalizedThumbprint $packageSignature.SignerCertificate.Thumbprint) -ne $trustedSigner) {
    throw 'The installer does not have a valid signature from the trusted Otium signer.'
}

[pscustomobject]@{
    PackagePath = $resolvedPackagePath
    Version = [string]$manifest.version
    Architecture = [string]$manifest.architecture
    SizeBytes = $package.Length
    Sha256 = $actualHash
    SignerThumbprint = $trustedSigner
}
