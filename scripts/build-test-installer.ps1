[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z.-]*$')]
    [string]$ReleaseLabel = '1.0.0-alpha',

    [switch]$CommunityRelease
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageKind = if ($CommunityRelease) { 'community' } else { 'test' }
$buildConfiguration = if ($CommunityRelease) { 'Release' } else { 'Debug' }
$artifactKind = if ($CommunityRelease) { 'community' } else { 'test' }
$publishedArtifactDirectory = Join-Path $repositoryRoot "artifacts\publish-$artifactKind\win-x64"
$installerOutputDirectory = Join-Path $repositoryRoot "artifacts\installer-$artifactKind\$Version"
$applicationProject = Join-Path $repositoryRoot 'src\Otium.App\Otium.App.csproj'
$setupProject = Join-Path $repositoryRoot 'src\Otium.SetupApp\Otium.SetupApp.csproj'
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
$wixStagingDirectory = Join-Path $env:PUBLIC "OtiumBuildStaging\$artifactKind\$Version"
$publishDirectory = Join-Path $wixStagingDirectory 'publish'
$wixIntermediateDirectory = Join-Path $wixStagingDirectory 'obj\'
$wixOutputDirectory = Join-Path $wixStagingDirectory 'installer\'
$setupPublishDirectory = Join-Path $wixStagingDirectory 'setup\'
New-Item -ItemType Directory -Path $cabinetTempDirectory -Force | Out-Null
$env:TEMP = $cabinetTempDirectory
$env:TMP = $cabinetTempDirectory

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $publishedArtifactDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

$sourceCommit = ((& git -C $repositoryRoot rev-parse HEAD) | Select-Object -First 1).Trim()
$repositoryDirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0

& $dotnet publish $applicationProject `
    -c $buildConfiguration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:RepositoryCommit=$sourceCommit `
    -p:RepositoryDirty=$repositoryDirty `
    -p:PublishSingleFile=true `
    -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium development publish failed with exit code $LASTEXITCODE."
}
Copy-Item -LiteralPath (Join-Path $publishDirectory 'Otium.exe') `
    -Destination (Join-Path $publishedArtifactDirectory 'Otium.exe') -Force
$publishedExecutableHash = (Get-FileHash -LiteralPath (Join-Path $publishDirectory 'Otium.exe') -Algorithm SHA256).Hash

& $dotnet build $installerProject `
    -c Release `
    -p:OtiumVersion=$Version `
    -p:OtiumReleaseLabel=$ReleaseLabel `
    -p:OtiumPublishDir="$publishDirectory" `
    -p:OtiumSignerThumbprint=$(if ($CommunityRelease) { 'UNSIGNED-COMMUNITY-BUILD' } else { 'UNSIGNED-DEVELOPMENT-BUILD' }) `
    -p:OtiumPackageKind=$packageKind `
    -p:OtiumExecutableSha256=$publishedExecutableHash `
    -p:OtiumAllowSameVersionUpgrades=yes `
    -p:SuppressSpecificWarnings=1076 `
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

& $dotnet publish $setupProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:InformationalVersion=$ReleaseLabel `
    -p:OtiumInstallerPath="$stagedInstaller" `
    -p:PublishDir="$setupPublishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium setup application publish failed with exit code $LASTEXITCODE."
}

$publishedSetup = Join-Path $setupPublishDirectory 'Otium.Setup.exe'
$setup = Join-Path $installerOutputDirectory "Otium-Setup-$ReleaseLabel.exe"
Copy-Item -LiteralPath $publishedSetup -Destination $setup -Force
$setupHash = Get-FileHash -LiteralPath $setup -Algorithm SHA256
Set-Content -LiteralPath "$setup.sha256" `
    -Value "$($setupHash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($setup))" `
    -Encoding ascii

& (Join-Path $PSScriptRoot 'verify-package-metadata.ps1') `
    -InstallerPath $installer `
    -SetupPath $setup `
    -ExpectedVersion $Version `
    -ExpectedReleaseLabel $ReleaseLabel `
    -ExpectedPackageKind $packageKind
if ($LASTEXITCODE -ne 0) {
    throw "Test package metadata verification failed with exit code $LASTEXITCODE."
}

$manifestPath = Join-Path $installerOutputDirectory 'release-manifest.json'
& (Join-Path $PSScriptRoot 'write-release-manifest.ps1') `
    -OutputPath $manifestPath `
    -InstallerPath $installer `
    -SetupPath $setup `
    -Version $Version `
    -ReleaseLabel $ReleaseLabel `
    -PackageKind $packageKind
if ($LASTEXITCODE -ne 0) {
    throw "Test release manifest generation failed with exit code $LASTEXITCODE."
}
& (Join-Path $PSScriptRoot 'verify-release-manifest.ps1') `
    -ManifestPath $manifestPath `
    -ExpectedPackageKind $packageKind

if ($CommunityRelease) {
    Write-Warning 'This community package is unsigned. Windows SmartScreen may display an unknown publisher warning.'
} else {
    Write-Warning 'This package is unsigned and must only be used for local development testing.'
}
Write-Output "Test setup:     $setup"
Write-Output "Setup SHA-256:  $($setupHash.Hash.ToLowerInvariant())"
Write-Output "Test installer: $installer"
Write-Output "SHA-256:      $($hash.Hash.ToLowerInvariant())"
Write-Output "Manifest:      $manifestPath"
