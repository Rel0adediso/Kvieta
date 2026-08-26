[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter(Mandatory)]
    [ValidatePattern('^(?:[A-Fa-f0-9]\s*){40}$')]
    [string]$SigningCertificateThumbprint,

    [ValidatePattern('^https://')]
    [string]$TimestampServer = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishedArtifactDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerOutputDirectory = Join-Path $repositoryRoot "artifacts\installer\$Version"
$applicationProject = Join-Path $repositoryRoot 'src\Otium.App\Otium.App.csproj'
$setupProject = Join-Path $repositoryRoot 'src\Otium.SetupApp\Otium.SetupApp.csproj'
$installerProject = Join-Path $repositoryRoot 'installer\Otium.Setup\Otium.Setup.wixproj'
$normalizedThumbprint = ($SigningCertificateThumbprint -replace '\s', '').ToUpperInvariant()
$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
$dotnet = if ($null -ne $dotnetCommand) {
    $dotnetCommand.Source
} else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'dotnet.exe was not found. Install the .NET SDK before building Otium.'
}

# makecab/smartcab can misread Turkish characters in the user TEMP path. Keep the
# override local to this PowerShell process and use a writable ASCII path.
$cabinetTempDirectory = Join-Path $env:PUBLIC 'OtiumBuildTemp'
$wixStagingDirectory = Join-Path $env:PUBLIC "OtiumBuildStaging\release\$Version"
$publishDirectory = Join-Path $wixStagingDirectory 'publish'
$wixIntermediateDirectory = Join-Path $wixStagingDirectory 'obj\'
$wixOutputDirectory = Join-Path $wixStagingDirectory 'installer\'
$setupPublishDirectory = Join-Path $wixStagingDirectory 'setup\'
New-Item -ItemType Directory -Path $cabinetTempDirectory -Force | Out-Null
$env:TEMP = $cabinetTempDirectory
$env:TMP = $cabinetTempDirectory

function Get-SigningCertificate([string]$Thumbprint) {
    $certificate = Get-ChildItem -Path 'Cert:\CurrentUser\My', 'Cert:\LocalMachine\My' |
        Where-Object {
            $_.Thumbprint -eq $Thumbprint -and $_.HasPrivateKey -and
            $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if ($null -eq $certificate) {
        throw "A code-signing certificate with private key was not found: $Thumbprint"
    }
    if ($certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)) {
        throw "The code-signing certificate is not currently valid: $Thumbprint"
    }

    return $certificate
}

function Get-SignTool {
    $command = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $windowsKits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $candidate = Get-ChildItem -LiteralPath $windowsKits -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '[\\/]x64$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) {
        throw 'signtool.exe was not found. Install the Windows SDK signing tools.'
    }

    return $candidate.FullName
}

function Invoke-BinarySigning([string]$Path, [string]$SignTool, [string]$Thumbprint, [bool]$MachineStore) {
    $arguments = @('sign', '/sha1', $Thumbprint, '/s', 'My', '/fd', 'SHA256', '/tr', $TimestampServer, '/td', 'SHA256')
    if ($MachineStore) {
        $arguments += '/sm'
    }
    $arguments += $Path
    & $SignTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode signing failed for $Path (signtool $LASTEXITCODE)."
    }
}

function Assert-ValidSignature([string]$Path, [string]$Thumbprint) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $Thumbprint) {
        throw "The generated release file does not have the expected valid signature: $Path"
    }
}

$signingCertificate = Get-SigningCertificate $normalizedThumbprint
$signTool = Get-SignTool
$certificateUsesMachineStore = $signingCertificate.PSParentPath -like '*LocalMachine*'

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $publishedArtifactDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

& $dotnet publish $applicationProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium publish failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $publishDirectory 'Otium.exe'
Invoke-BinarySigning $publishedExecutable $signTool $normalizedThumbprint $certificateUsesMachineStore
Assert-ValidSignature $publishedExecutable $normalizedThumbprint
Copy-Item -LiteralPath $publishedExecutable `
    -Destination (Join-Path $publishedArtifactDirectory 'Otium.exe') -Force

& $dotnet build $installerProject `
    -c $Configuration `
    -p:OtiumVersion=$Version `
    -p:OtiumPublishDir="$publishDirectory" `
    -p:OtiumSignerThumbprint=$normalizedThumbprint `
    -p:BaseIntermediateOutputPath="$wixIntermediateDirectory" `
    -p:OutputPath="$wixOutputDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Otium installer build failed with exit code $LASTEXITCODE."
}

$stagedInstaller = Join-Path $wixOutputDirectory "Otium-$Version-win-x64.msi"
if (-not (Test-Path -LiteralPath $stagedInstaller)) {
    throw "Installer staging output was not found: $stagedInstaller"
}

$installer = Join-Path $installerOutputDirectory "Otium-$Version-win-x64.msi"
Copy-Item -LiteralPath $stagedInstaller -Destination $installer -Force
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer output was not found: $installer"
}

Invoke-BinarySigning $installer $signTool $normalizedThumbprint $certificateUsesMachineStore
Assert-ValidSignature $installer $normalizedThumbprint

& $dotnet publish $setupProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:OtiumInstallerPath="$installer" `
    -p:PublishDir="$setupPublishDirectory\"
if ($LASTEXITCODE -ne 0) {
    throw "Otium setup application publish failed with exit code $LASTEXITCODE."
}

$publishedSetup = Join-Path $setupPublishDirectory 'Otium.Setup.exe'
$setup = Join-Path $installerOutputDirectory "Otium-Setup-$Version.exe"
Copy-Item -LiteralPath $publishedSetup -Destination $setup -Force
Invoke-BinarySigning $setup $signTool $normalizedThumbprint $certificateUsesMachineStore
Assert-ValidSignature $setup $normalizedThumbprint

$verificationScript = Join-Path $installerOutputDirectory 'verify-installer.ps1'
$updateScript = Join-Path $installerOutputDirectory 'install-update.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify-installer.ps1') -Destination $verificationScript -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-update.ps1') -Destination $updateScript -Force
foreach ($scriptPath in @($verificationScript, $updateScript)) {
    $signature = Set-AuthenticodeSignature -LiteralPath $scriptPath `
        -Certificate $signingCertificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode signing failed for $scriptPath ($($signature.Status))."
    }
    Assert-ValidSignature $scriptPath $normalizedThumbprint
}

$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
$checksumPath = "$installer.sha256"
Set-Content -LiteralPath $checksumPath -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installer))" -Encoding ascii
$setupHash = Get-FileHash -LiteralPath $setup -Algorithm SHA256
Set-Content -LiteralPath "$setup.sha256" -Value "$($setupHash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($setup))" -Encoding ascii

& (Join-Path $PSScriptRoot 'verify-package-metadata.ps1') `
    -InstallerPath $installer `
    -SetupPath $setup `
    -ExpectedVersion $Version `
    -ExpectedReleaseLabel $Version `
    -ExpectedSignerThumbprint $normalizedThumbprint `
    -RequireSignature
if ($LASTEXITCODE -ne 0) {
    throw "Release package metadata verification failed with exit code $LASTEXITCODE."
}

$manifestPath = Join-Path $installerOutputDirectory 'release-manifest.json'
& (Join-Path $PSScriptRoot 'write-release-manifest.ps1') `
    -OutputPath $manifestPath `
    -InstallerPath $installer `
    -SetupPath $setup `
    -Version $Version `
    -ReleaseLabel $Version `
    -PackageKind public `
    -SignerThumbprint $normalizedThumbprint
if ($LASTEXITCODE -ne 0) {
    throw "Release manifest generation failed with exit code $LASTEXITCODE."
}
& (Join-Path $PSScriptRoot 'verify-release-manifest.ps1') `
    -ManifestPath $manifestPath `
    -ExpectedPackageKind public

Write-Output "Installer: $installer"
Write-Output "Setup:     $setup"
Write-Output "SHA-256:  $checksumPath"
Write-Output "Manifest: $manifestPath"
