[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$SetupPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedVersion,

    [string]$ExpectedReleaseLabel = $ExpectedVersion,
    [ValidateSet('test', 'community', 'public')]
    [string]$ExpectedPackageKind,
    [string]$ExpectedSignerThumbprint,
    [switch]$RequireSignature
)

$ErrorActionPreference = 'Stop'

function Assert-Equal([string]$Name, $Actual, $Expected) {
    if ($Actual -ne $Expected) {
        throw "$Name mismatch. Expected '$Expected', found '$Actual'."
    }
}

function Get-MsiScalar($Database, [string]$Query) {
    $view = $Database.OpenView($Query)
    try {
        $view.Execute()
        $record = $view.Fetch()
        if ($null -eq $record) {
            return $null
        }
        return $record.StringData(1)
    } finally {
        if ($null -ne $view) {
            [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null
        }
    }
}

foreach ($path in @($InstallerPath, $SetupPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Package file was not found: $path"
    }
    if ((Get-Item -LiteralPath $path).Length -le 0) {
        throw "Package file is empty: $path"
    }
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $null
try {
    $database = $installer.OpenDatabase((Resolve-Path $InstallerPath).Path, 0)
    $property = {
        param([string]$name)
        Get-MsiScalar $database "SELECT ``Value`` FROM ``Property`` WHERE ``Property``='$name'"
    }

    Assert-Equal 'ProductName' (& $property 'ProductName') 'Otium'
    Assert-Equal 'Manufacturer' (& $property 'Manufacturer') 'Otium'
    Assert-Equal 'ProductVersion' (& $property 'ProductVersion') $ExpectedVersion
    Assert-Equal 'UpgradeCode' (& $property 'UpgradeCode') '{B7DB180A-83DE-4B93-A8FB-453734F91C10}'
    Assert-Equal 'ARPNOREMOVE' (& $property 'ARPNOREMOVE') '1'
    Assert-Equal 'ARPNOMODIFY' (& $property 'ARPNOMODIFY') '1'
    Assert-Equal 'Guardian service' (Get-MsiScalar $database "SELECT ``Name`` FROM ``ServiceInstall`` WHERE ``Name``='OtiumGuardian'") 'OtiumGuardian'
    Assert-Equal 'Uninstall entry point' (Get-MsiScalar $database "SELECT ``Arguments`` FROM ``Shortcut`` WHERE ``Arguments``='--uninstall'") '--uninstall'
    Assert-Equal 'Embedded cabinet' (Get-MsiScalar $database "SELECT ``Cabinet`` FROM ``Media`` WHERE ``DiskId``=1") '#cab1.cab'
    $packageKind = Get-MsiScalar $database "SELECT ``Value`` FROM ``Registry`` WHERE ``Name``='PackageKind'"
    if ($ExpectedPackageKind) {
        Assert-Equal 'Package kind' $packageKind $ExpectedPackageKind
    } elseif ([string]::IsNullOrWhiteSpace($packageKind)) {
        throw 'Package kind metadata is missing.'
    }
    if ([string]::IsNullOrWhiteSpace((Get-MsiScalar $database "SELECT ``Value`` FROM ``Registry`` WHERE ``Name``='ExecutableSha256'"))) {
        throw 'Executable SHA-256 metadata is missing.'
    }
} finally {
    if ($null -ne $database) {
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
    }
    if ($null -ne $installer) {
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
    }
}

$setupInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $SetupPath).Path)
Assert-Equal 'Setup product name' $setupInfo.ProductName 'Otium Setup'
Assert-Equal 'Setup product version' $setupInfo.ProductVersion $ExpectedReleaseLabel

$setupProcess = Start-Process -FilePath (Resolve-Path $SetupPath).Path -ArgumentList '--verify-package' -PassThru -Wait
if ($setupProcess.ExitCode -ne 0) {
    throw "Embedded MSI verification failed with exit code $($setupProcess.ExitCode)."
}

foreach ($path in @($InstallerPath, $SetupPath)) {
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($RequireSignature) {
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
            throw "A valid Authenticode signature is required: $path"
        }
        if ($ExpectedSignerThumbprint -and $signature.SignerCertificate.Thumbprint -ne $ExpectedSignerThumbprint) {
            throw "Signer thumbprint mismatch: $path"
        }
    } elseif ($signature.Status -notin @('NotSigned', 'Valid')) {
        throw "Package has an invalid Authenticode state '$($signature.Status)': $path"
    }
}

Write-Output "Package metadata verification passed: $ExpectedReleaseLabel"

