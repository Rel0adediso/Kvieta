[CmdletBinding()]
param(
    [ValidateRange(5, 60)]
    [int]$TimeoutSeconds = 20,

    [switch]$IncludeServiceCrash
)

$ErrorActionPreference = 'Stop'
$serviceName = 'OtiumGuardian'
$installedExecutable = Join-Path $env:ProgramFiles 'Otium\Otium.exe'
$processStatePath = Join-Path $env:ProgramData 'Otium\guardian-process.json'

function Get-GuardianState {
    if (-not (Test-Path -LiteralPath $processStatePath)) {
        return $null
    }

    try {
        return Get-Content -Raw -LiteralPath $processStatePath | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Wait-ForGuardianProcess([int]$PreviousProcessId) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $state = Get-GuardianState
        if ($null -eq $state -or $state.ProcessId -eq $PreviousProcessId) {
            continue
        }

        $process = Get-Process -Id $state.ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $process) {
            return $state.ProcessId
        }
    }

    throw "Guardian session did not return within $TimeoutSeconds seconds."
}

function Wait-ForCurrentGuardianProcess {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $state = Get-GuardianState
        if ($null -ne $state) {
            $process = Get-Process -Id $state.ProcessId -ErrorAction SilentlyContinue
            if ($null -ne $process) {
                return $state
            }
        }

        Start-Sleep -Milliseconds 250
    }

    throw "A live Guardian session was not found within $TimeoutSeconds seconds."
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -eq $service -or $service.Status -ne 'Running') {
    throw 'OtiumGuardian must be installed and running before this test.'
}
if (-not (Test-Path -LiteralPath $installedExecutable)) {
    throw "Installed Otium executable was not found: $installedExecutable"
}

$initialState = Wait-ForCurrentGuardianProcess

Stop-Process -Id $initialState.ProcessId -Force
$replacementProcessId = Wait-ForGuardianProcess -PreviousProcessId $initialState.ProcessId
Write-Output "PASS session-recovery old=$($initialState.ProcessId) new=$replacementProcessId"

if ($IncludeServiceCrash) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'The service crash test requires an elevated PowerShell window.'
    }

    $serviceInstance = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
    if ($null -eq $serviceInstance -or $serviceInstance.ProcessId -le 0) {
        throw 'The Guardian service process could not be resolved.'
    }

    Stop-Process -Id $serviceInstance.ProcessId -Force
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $service = Get-Service -Name $serviceName
        $service.Refresh()
    } while ($service.Status -ne 'Running' -and (Get-Date) -lt $deadline)

    if ($service.Status -ne 'Running') {
        throw "Guardian service did not recover within $TimeoutSeconds seconds."
    }

    Write-Output 'PASS service-recovery'
}
