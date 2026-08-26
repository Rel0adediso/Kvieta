[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [string]$ExpectedCommit,
    [ValidateSet('test', 'public')][string]$ExpectedPackageKind
)

$ErrorActionPreference = 'Stop'
$manifestFile = Get-Item -LiteralPath $ManifestPath -ErrorAction Stop
$manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 2 -or $manifest.product -ne 'Otium' -or $manifest.architecture -ne 'win-x64') {
    throw 'Release manifest identity or schema is invalid.'
}
if ($ExpectedCommit -and $manifest.commit -ne $ExpectedCommit) {
    throw "Manifest commit mismatch. Expected '$ExpectedCommit', found '$($manifest.commit)'."
}
if ($ExpectedPackageKind -and $manifest.packageKind -ne $ExpectedPackageKind) {
    throw "Manifest package kind mismatch. Expected '$ExpectedPackageKind', found '$($manifest.packageKind)'."
}
if ($manifest.packageKind -eq 'public' -and -not $manifest.signed) {
    throw 'Public release manifest must declare signed artifacts.'
}
if ($manifest.packageKind -eq 'test' -and $manifest.signed) {
    throw 'Test release manifest must not declare signed artifacts.'
}

$artifactDirectory = $manifestFile.DirectoryName
$roles = @($manifest.artifacts | ForEach-Object { $_.role })
if ($roles.Count -ne 2 -or 'setup' -notin $roles -or 'msi' -notin $roles) {
    throw 'Release manifest must contain exactly one setup and one MSI artifact.'
}

foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $artifactDirectory $artifact.file
    $file = Get-Item -LiteralPath $path -ErrorAction Stop
    if ($file.Length -ne $artifact.sizeBytes) {
        throw "Artifact size mismatch: $($artifact.file)"
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $artifact.sha256) {
        throw "Artifact SHA-256 mismatch: $($artifact.file)"
    }
}

Write-Output "Release manifest verification passed: $($manifest.releaseLabel) ($($manifest.commit))"

