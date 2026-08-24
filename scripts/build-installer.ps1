[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.17.0',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerOutputDirectory = Join-Path $repositoryRoot "artifacts\installer\$Version"
$applicationProject = Join-Path $repositoryRoot 'src\Otium.App\Otium.App.csproj'
$installerProject = Join-Path $repositoryRoot 'installer\Otium.Setup\Otium.Setup.wixproj'

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

dotnet publish $applicationProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium publish failed with exit code $LASTEXITCODE."
}

dotnet build $installerProject `
    -c $Configuration `
    -p:OtiumVersion=$Version `
    -p:OtiumPublishDir="$publishDirectory" `
    -p:OutputPath="$installerOutputDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Otium installer build failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $installerOutputDirectory "Otium-$Version-win-x64.msi"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer output was not found: $installer"
}

$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
$checksumPath = "$installer.sha256"
Set-Content -LiteralPath $checksumPath -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installer))" -Encoding ascii

$manifestPath = Join-Path $installerOutputDirectory 'release-manifest.json'
$manifest = [ordered]@{
    schemaVersion = 1
    product = 'Otium'
    version = $Version
    architecture = 'win-x64'
    package = [IO.Path]::GetFileName($installer)
    sizeBytes = (Get-Item -LiteralPath $installer).Length
    sha256 = $hash.Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify-installer.ps1') `
    -Destination $installerOutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-update.ps1') `
    -Destination $installerOutputDirectory -Force

Write-Output "Installer: $installer"
Write-Output "SHA-256:  $checksumPath"
Write-Output "Manifest: $manifestPath"
