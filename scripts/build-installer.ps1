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
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$installerOutputDirectory = Join-Path $repositoryRoot "artifacts\installer\$Version"
$applicationProject = Join-Path $repositoryRoot 'src\Otium.App\Otium.App.csproj'
$installerProject = Join-Path $repositoryRoot 'installer\Otium.Setup\Otium.Setup.wixproj'
$normalizedThumbprint = ($SigningCertificateThumbprint -replace '\s', '').ToUpperInvariant()

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

$publishedExecutable = Join-Path $publishDirectory 'Otium.exe'
Invoke-BinarySigning $publishedExecutable $signTool $normalizedThumbprint $certificateUsesMachineStore
Assert-ValidSignature $publishedExecutable $normalizedThumbprint

dotnet build $installerProject `
    -c $Configuration `
    -p:OtiumVersion=$Version `
    -p:OtiumPublishDir="$publishDirectory" `
    -p:OtiumSignerThumbprint=$normalizedThumbprint `
    -p:OutputPath="$installerOutputDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Otium installer build failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $installerOutputDirectory "Otium-$Version-win-x64.msi"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer output was not found: $installer"
}

Invoke-BinarySigning $installer $signTool $normalizedThumbprint $certificateUsesMachineStore
Assert-ValidSignature $installer $normalizedThumbprint

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

$manifestPath = Join-Path $installerOutputDirectory 'release-manifest.json'
$manifest = [ordered]@{
    schemaVersion = 1
    product = 'Otium'
    version = $Version
    architecture = 'win-x64'
    package = [IO.Path]::GetFileName($installer)
    sizeBytes = (Get-Item -LiteralPath $installer).Length
    sha256 = $hash.Hash.ToLowerInvariant()
    signerThumbprint = $normalizedThumbprint.ToLowerInvariant()
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output "Installer: $installer"
Write-Output "SHA-256:  $checksumPath"
Write-Output "Manifest: $manifestPath"
