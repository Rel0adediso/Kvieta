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
$installerOutputDirectory = Join-Path $repositoryRoot 'artifacts\installer'
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

Write-Output "Installer: $installer"
Write-Output "SHA-256:  $checksumPath"
