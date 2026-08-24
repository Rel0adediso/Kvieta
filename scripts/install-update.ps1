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

function Get-VerifiedRelease([string]$Path) {
    return & $verifyScript -ManifestPath $Path
}

function Get-InstalledOtium {
    try {
        $key = Get-ItemProperty -LiteralPath 'HKLM:\Software\Otium' -ErrorAction Stop
        if (-not $key.ProductCode -or -not $key.InstalledVersion) {
            return $null
        }

        return [pscustomobject]@{
            ProductCode = [string]$key.ProductCode
            Version = [version]$key.InstalledVersion
        }
    }
    catch {
        return $null
    }
}

function Test-OtiumHealth([version]$ExpectedVersion) {
    $installed = Get-InstalledOtium
    $executable = 'C:\Program Files\Otium\Otium.exe'
    $service = Get-Service -Name OtiumGuardian -ErrorAction SilentlyContinue
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
    $logDirectory = Join-Path $env:ProgramData 'Otium\UpdateLogs'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $logPath = Join-Path $logDirectory $LogName
    $propertyText = if ($Properties.Count -eq 0) { '' } else { ' ' + ($Properties -join ' ') }
    $arguments = "$Action `"$Target`"$propertyText /qn /norestart /l*v `"$logPath`""
    $process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" `
        -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    return $process.ExitCode
}

function Invoke-Rollback($RollbackRelease, [version]$RollbackVersion, [bool]$KeepDesktopShortcut) {
    $current = Get-InstalledOtium
    if ($null -ne $current) {
        $removeExitCode = Invoke-Msi '/x' $current.ProductCode "rollback-remove-$($current.Version).log"
        if ($removeExitCode -ne 0) {
            throw "Rollback could not remove version $($current.Version) (MSI $removeExitCode)."
        }
    }

    $properties = if ($KeepDesktopShortcut) { @('ADDLOCAL=MainFeature,DesktopShortcutFeature') } else { @() }
    $installExitCode = Invoke-Msi '/i' $RollbackRelease.PackagePath "rollback-install-$RollbackVersion.log" $properties
    if ($installExitCode -ne 0 -or -not (Test-OtiumHealth $RollbackVersion)) {
        throw "Rollback could not restore Otium $RollbackVersion (MSI $installExitCode)."
    }
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$resolvedRollbackManifestPath = if ([string]::IsNullOrWhiteSpace($RollbackManifestPath)) {
    $null
} else {
    (Resolve-Path -LiteralPath $RollbackManifestPath).Path
}

# Verify once before elevation so invalid packages never trigger a UAC prompt.
$preflightTarget = Get-VerifiedRelease $resolvedManifestPath
$preflightRollback = $null
if ($null -ne $resolvedRollbackManifestPath) {
    $preflightRollback = Get-VerifiedRelease $resolvedRollbackManifestPath
}

$preflightInstalled = Get-InstalledOtium
if ($null -ne $preflightInstalled -and [version]$preflightTarget.Version -le $preflightInstalled.Version) {
    throw "Otium $($preflightTarget.Version) is not newer than installed version $($preflightInstalled.Version)."
}
if ($null -ne $preflightInstalled -and $null -eq $preflightRollback) {
    throw 'A verified rollback manifest is required when updating an installed Otium version.'
}
if ($null -ne $preflightInstalled -and [version]$preflightRollback.Version -ne $preflightInstalled.Version) {
    throw "Rollback package $($preflightRollback.Version) does not match installed version $($preflightInstalled.Version)."
}

if (-not (Test-Administrator)) {
    $hostPath = (Get-Process -Id $PID).Path
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $hostPath
    $startInfo.UseShellExecute = $true
    $startInfo.Verb = 'runas'
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($argument in @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
            '-ManifestPath', $resolvedManifestPath)) {
        $startInfo.ArgumentList.Add($argument)
    }
    if ($null -ne $resolvedRollbackManifestPath) {
        $startInfo.ArgumentList.Add('-RollbackManifestPath')
        $startInfo.ArgumentList.Add($resolvedRollbackManifestPath)
    }
    if ($ForceRollbackForTesting) {
        $startInfo.ArgumentList.Add('-ForceRollbackForTesting')
    }

    try {
        $elevated = [Diagnostics.Process]::Start($startInfo)
        $elevated.WaitForExit()
        exit $elevated.ExitCode
    }
    catch [System.ComponentModel.Win32Exception] {
        throw 'The update was cancelled before administrator approval.'
    }
}

# Verify again after elevation to narrow the package replacement window.
$targetRelease = Get-VerifiedRelease $resolvedManifestPath
$targetVersion = [version]$targetRelease.Version
$rollbackRelease = if ($null -eq $resolvedRollbackManifestPath) {
    $null
} else {
    Get-VerifiedRelease $resolvedRollbackManifestPath
}
$installedBefore = Get-InstalledOtium
$keepDesktopShortcut = Test-Path -LiteralPath 'C:\Users\Public\Desktop\Otium.lnk'

if ($null -ne $installedBefore -and $targetVersion -le $installedBefore.Version) {
    throw "Otium $targetVersion is not newer than installed version $($installedBefore.Version)."
}

if ($null -ne $installedBefore) {
    if ($null -eq $rollbackRelease) {
        throw 'A verified rollback manifest is required when updating an installed Otium version.'
    }

    if ([version]$rollbackRelease.Version -ne $installedBefore.Version) {
        throw "Rollback package $($rollbackRelease.Version) does not match installed version $($installedBefore.Version)."
    }
}

$updateProperties = if ($keepDesktopShortcut) { @('ADDLOCAL=MainFeature,DesktopShortcutFeature') } else { @() }
$updateExitCode = Invoke-Msi '/i' $targetRelease.PackagePath "update-$targetVersion.log" $updateProperties
$targetHealthy = $updateExitCode -eq 0 -and
    -not $ForceRollbackForTesting -and
    (Test-OtiumHealth $targetVersion)
if ($targetHealthy) {
    [pscustomobject]@{
        Status = 'Updated'
        PreviousVersion = if ($null -eq $installedBefore) { $null } else { $installedBefore.Version.ToString() }
        InstalledVersion = $targetVersion.ToString()
    }
    exit 0
}

if ($null -eq $installedBefore) {
    $failedInstall = Get-InstalledOtium
    if ($null -ne $failedInstall) {
        $null = Invoke-Msi '/x' $failedInstall.ProductCode "failed-install-remove-$($failedInstall.Version).log"
    }
    throw "Otium $targetVersion failed its post-install health check (MSI $updateExitCode)."
}

if (Test-OtiumHealth $installedBefore.Version) {
    throw "Otium $targetVersion was not installed; version $($installedBefore.Version) remains healthy (MSI $updateExitCode)."
}

Invoke-Rollback $rollbackRelease $installedBefore.Version $keepDesktopShortcut
[pscustomobject]@{
    Status = 'RolledBack'
    FailedVersion = $targetVersion.ToString()
    RestoredVersion = $installedBefore.Version.ToString()
}

if (-not $ForceRollbackForTesting) {
    throw "Otium $targetVersion failed its health check and $($installedBefore.Version) was restored."
}
