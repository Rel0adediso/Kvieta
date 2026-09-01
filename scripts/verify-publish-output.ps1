[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$directory = Get-Item -LiteralPath $PublishDirectory -ErrorAction Stop
$executablePath = Join-Path $directory.FullName 'Kvieta.exe'
$executable = Get-Item -LiteralPath $executablePath -ErrorAction Stop
if ($executable.Length -lt 1MB) {
    throw "Self-contained executable is unexpectedly small: $($executable.Length) bytes."
}

$header = [System.IO.File]::ReadAllBytes($executable.FullName)[0..1]
if ($header[0] -ne 0x4D -or $header[1] -ne 0x5A) {
    throw 'Published Kvieta.exe is not a valid Windows PE file.'
}

$info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable.FullName)
if ($info.ProductName -ne 'Kvieta') {
    throw "Published product name mismatch: $($info.ProductName)"
}
if (-not $info.ProductVersion.StartsWith($ExpectedVersion, [System.StringComparison]::Ordinal)) {
    throw "Published product version mismatch. Expected '$ExpectedVersion', found '$($info.ProductVersion)'."
}

$unexpectedFiles = @(Get-ChildItem -LiteralPath $directory.FullName -File | Where-Object { $_.Name -ne 'Kvieta.exe' })
if ($unexpectedFiles.Count -gt 0) {
    throw "Single-file publish contains unexpected loose files: $($unexpectedFiles.Name -join ', ')"
}

Write-Output "Self-contained publish verification passed: $($executable.Length) bytes"

