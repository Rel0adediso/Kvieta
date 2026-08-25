[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishedArtifactDirectory = Join-Path $repositoryRoot 'artifacts\publish-test\win-x64'
$installerOutputDirectory = Join-Path $repositoryRoot "artifacts\installer-test\$Version"
$applicationProject = Join-Path $repositoryRoot 'src\Otium.App\Otium.App.csproj'
$installerProject = Join-Path $repositoryRoot 'installer\Otium.Setup\Otium.Setup.wixproj'
$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
$dotnet = if ($null -ne $dotnetCommand) {
    $dotnetCommand.Source
} else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'dotnet.exe was not found. Install the .NET SDK before building Otium.'
}

# Native Windows cabinet tooling is not Unicode-safe for every temporary path.
$cabinetTempDirectory = Join-Path $env:PUBLIC 'OtiumBuildTemp'
$wixStagingDirectory = Join-Path $env:PUBLIC "OtiumBuildStaging\test\$Version"
$publishDirectory = Join-Path $wixStagingDirectory 'publish'
$wixIntermediateDirectory = Join-Path $wixStagingDirectory 'obj\'
$wixOutputDirectory = Join-Path $wixStagingDirectory 'installer\'
New-Item -ItemType Directory -Path $cabinetTempDirectory -Force | Out-Null
$env:TEMP = $cabinetTempDirectory
$env:TMP = $cabinetTempDirectory

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $publishedArtifactDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

& $dotnet publish $applicationProject `
    -c Debug `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium development publish failed with exit code $LASTEXITCODE."
}
Copy-Item -LiteralPath (Join-Path $publishDirectory 'Otium.exe') `
    -Destination (Join-Path $publishedArtifactDirectory 'Otium.exe') -Force

& $dotnet build $installerProject `
    -c Release `
    -p:OtiumVersion=$Version `
    -p:OtiumPublishDir="$publishDirectory" `
    -p:OtiumSignerThumbprint=UNSIGNED-DEVELOPMENT-BUILD `
    -p:BaseIntermediateOutputPath="$wixIntermediateDirectory" `
    -p:OutputPath="$wixOutputDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Otium test installer build failed with exit code $LASTEXITCODE."
}

$stagedInstaller = Join-Path $wixOutputDirectory "Otium-$Version-win-x64.msi"
if (-not (Test-Path -LiteralPath $stagedInstaller)) {
    throw "Test installer staging output was not found: $stagedInstaller"
}

$installer = Join-Path $installerOutputDirectory "Otium-$Version-win-x64.msi"
Copy-Item -LiteralPath $stagedInstaller -Destination $installer -Force
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Test installer output was not found: $installer"
}

$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
Set-Content -LiteralPath "$installer.sha256" `
    -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installer))" `
    -Encoding ascii

Write-Warning 'This package is unsigned and must only be used for local development testing.'
Write-Output "Test installer: $installer"
Write-Output "SHA-256:      $($hash.Hash.ToLowerInvariant())"
