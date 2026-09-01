[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ManifestPath,

    [string]$RollbackManifestPath,

    [switch]$ForceRollbackForTesting
)

$ErrorActionPreference = 'Stop'
$verifyScript = Join-Path $PSScriptRoot 'verify-installer.ps1'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-NormalizedThumbprint([string]$Thumbprint) {
    return ($Thumbprint -replace '\s', '').ToUpperInvariant()
}

function Get-CurrentScriptSigner {
    $signature = Get-AuthenticodeSignature -LiteralPath $PSCommandPath
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
        throw 'The Kvieta updater does not have a valid Authenticode signature.'
    }

    return Get-NormalizedThumbprint $signature.SignerCertificate.Thumbprint
}

function Assert-TrustedScriptSignature([string]$Path, [string]$TrustedSigner) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        (Get-NormalizedThumbprint $signature.SignerCertificate.Thumbprint) -ne $TrustedSigner) {
        throw 'The installer verification script does not have a valid signature from the trusted Kvieta signer.'
    }
}

function Get-VerifiedRelease([string]$Path, [string]$TrustedSigner) {
    Assert-TrustedScriptSignature $verifyScript $TrustedSigner
    return & $verifyScript -ManifestPath $Path -TrustedSignerThumbprint $TrustedSigner
}

function Get-InstalledKvieta {
    try {
        $key = Get-ItemProperty -LiteralPath 'HKLM:\Software\Kvieta' -ErrorAction Stop
        if (-not $key.ProductCode -or -not $key.InstalledVersion) {
            return $null
        }

        return [pscustomobject]@{
            ProductCode = [string]$key.ProductCode
            Version = [version]$key.InstalledVersion
            SignerThumbprint = if ([string]::IsNullOrWhiteSpace([string]$key.SignerThumbprint)) {
                $null
            } else {
                Get-NormalizedThumbprint ([string]$key.SignerThumbprint)
            }
        }
    }
    catch {
        return $null
    }
}

function Test-KvietaHealth([version]$ExpectedVersion) {
    $installed = Get-InstalledKvieta
    $executable = 'C:\Program Files\Kvieta\Kvieta.exe'
    $service = Get-Service -Name KvietaGuardian -ErrorAction SilentlyContinue
    if ($null -eq $installed -or $installed.Version -ne $ExpectedVersion -or
        -not (Test-Path -LiteralPath $executable) -or
        $null -eq $service -or $service.Status -ne 'Running') {
        return $false
    }

    $productVersion = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($productVersion)) {
        return $false
    }

    $binaryVersionText = $productVersion.Split('+')[0]
    return [version]$binaryVersionText -eq $ExpectedVersion
}

function Invoke-Msi([string]$Action, [string]$Target, [string]$LogName, [string[]]$Properties = @()) {
    $logDirectory = Join-Path $env:ProgramData 'Kvieta\UpdateLogs'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $logPath = Join-Path $logDirectory $LogName
    $propertyText = if ($Properties.Count -eq 0) { '' } else { ' ' + ($Properties -join ' ') }
    $arguments = "$Action `"$Target`"$propertyText /qn /norestart /l*v `"$logPath`""
    $process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" `
        -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    return $process.ExitCode
}

function Invoke-Rollback($RollbackRelease, [version]$RollbackVersion, [bool]$KeepDesktopShortcut) {
    $current = Get-InstalledKvieta
    if ($null -ne $current) {
        $removeExitCode = Invoke-Msi '/x' $current.ProductCode "rollback-remove-$($current.Version).log"
        if ($removeExitCode -ne 0) {
            throw "Rollback could not remove version $($current.Version) (MSI $removeExitCode)."
        }
    }

    $properties = if ($KeepDesktopShortcut) { @('ADDLOCAL=MainFeature,DesktopShortcutFeature') } else { @() }
    $installExitCode = Invoke-Msi '/i' $RollbackRelease.PackagePath "rollback-install-$RollbackVersion.log" $properties
    if ($installExitCode -ne 0 -or -not (Test-KvietaHealth $RollbackVersion)) {
        throw "Rollback could not restore Kvieta $RollbackVersion (MSI $installExitCode)."
    }
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$resolvedRollbackManifestPath = if ([string]::IsNullOrWhiteSpace($RollbackManifestPath)) {
    $null
} else {
    (Resolve-Path -LiteralPath $RollbackManifestPath).Path
}

$updaterSigner = Get-CurrentScriptSigner
$preflightInstalled = Get-InstalledKvieta
$trustedSigner = if ($null -ne $preflightInstalled -and
    -not [string]::IsNullOrWhiteSpace($preflightInstalled.SignerThumbprint)) {
    $preflightInstalled.SignerThumbprint
} else {
    $updaterSigner
}
if ($updaterSigner -ne $trustedSigner) {
    throw 'The updater signer does not match the signer pinned by the installed Kvieta release.'
}

# Verify once before elevation so invalid packages never trigger a UAC prompt.
$preflightTarget = Get-VerifiedRelease $resolvedManifestPath $trustedSigner
$preflightRollback = $null
if ($null -ne $resolvedRollbackManifestPath) {
    $preflightRollback = Get-VerifiedRelease $resolvedRollbackManifestPath $trustedSigner
}

if ($null -ne $preflightInstalled -and [version]$preflightTarget.Version -le $preflightInstalled.Version) {
    throw "Kvieta $($preflightTarget.Version) is not newer than installed version $($preflightInstalled.Version)."
}
if ($null -ne $preflightInstalled -and $null -eq $preflightRollback) {
    throw 'A verified rollback manifest is required when updating an installed Kvieta version.'
}
if ($null -ne $preflightInstalled -and [version]$preflightRollback.Version -ne $preflightInstalled.Version) {
    throw "Rollback package $($preflightRollback.Version) does not match installed version $($preflightInstalled.Version)."
}

if (-not (Test-Administrator)) {
    $hostPath = (Get-Process -Id $PID).Path
    $startArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -ManifestPath `"$resolvedManifestPath`""
    if ($null -ne $resolvedRollbackManifestPath) {
        $startArguments += " -RollbackManifestPath `"$resolvedRollbackManifestPath`""
    }
    if ($ForceRollbackForTesting) {
        $startArguments += ' -ForceRollbackForTesting'
    }

    try {
        $elevated = Start-Process -FilePath $hostPath -ArgumentList $startArguments `
            -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        exit $elevated.ExitCode
    }
    catch [System.ComponentModel.Win32Exception] {
        throw 'The update was cancelled before administrator approval.'
    }
}

# Verify again after elevation to narrow the package replacement window.
$installedAfterElevation = Get-InstalledKvieta
if ($null -ne $installedAfterElevation -and
    -not [string]::IsNullOrWhiteSpace($installedAfterElevation.SignerThumbprint) -and
    $installedAfterElevation.SignerThumbprint -ne $trustedSigner) {
    throw 'The installed Kvieta signer changed while the update was awaiting elevation.'
}

$targetRelease = Get-VerifiedRelease $resolvedManifestPath $trustedSigner
$targetVersion = [version]$targetRelease.Version
$rollbackRelease = if ($null -eq $resolvedRollbackManifestPath) {
    $null
} else {
    Get-VerifiedRelease $resolvedRollbackManifestPath $trustedSigner
}
$installedBefore = Get-InstalledKvieta
$keepDesktopShortcut = Test-Path -LiteralPath 'C:\Users\Public\Desktop\Kvieta.lnk'

if ($null -ne $installedBefore -and $targetVersion -le $installedBefore.Version) {
    throw "Kvieta $targetVersion is not newer than installed version $($installedBefore.Version)."
}

if ($null -ne $installedBefore) {
    if ($null -eq $rollbackRelease) {
        throw 'A verified rollback manifest is required when updating an installed Kvieta version.'
    }

    if ([version]$rollbackRelease.Version -ne $installedBefore.Version) {
        throw "Rollback package $($rollbackRelease.Version) does not match installed version $($installedBefore.Version)."
    }
}

$updateProperties = if ($keepDesktopShortcut) { @('ADDLOCAL=MainFeature,DesktopShortcutFeature') } else { @() }
$updateExitCode = Invoke-Msi '/i' $targetRelease.PackagePath "update-$targetVersion.log" $updateProperties
$targetHealthy = $updateExitCode -eq 0 -and
    -not $ForceRollbackForTesting -and
    (Test-KvietaHealth $targetVersion)
if ($targetHealthy) {
    [pscustomobject]@{
        Status = 'Updated'
        PreviousVersion = if ($null -eq $installedBefore) { $null } else { $installedBefore.Version.ToString() }
        InstalledVersion = $targetVersion.ToString()
    }
    exit 0
}

if ($null -eq $installedBefore) {
    $failedInstall = Get-InstalledKvieta
    if ($null -ne $failedInstall) {
        $null = Invoke-Msi '/x' $failedInstall.ProductCode "failed-install-remove-$($failedInstall.Version).log"
    }
    throw "Kvieta $targetVersion failed its post-install health check (MSI $updateExitCode)."
}

if (Test-KvietaHealth $installedBefore.Version) {
    throw "Kvieta $targetVersion was not installed; version $($installedBefore.Version) remains healthy (MSI $updateExitCode)."
}

Invoke-Rollback $rollbackRelease $installedBefore.Version $keepDesktopShortcut
[pscustomobject]@{
    Status = 'RolledBack'
    FailedVersion = $targetVersion.ToString()
    RestoredVersion = $installedBefore.Version.ToString()
}

if (-not $ForceRollbackForTesting) {
    throw "Kvieta $targetVersion failed its health check and $($installedBefore.Version) was restored."
}
