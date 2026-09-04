[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$requiredFiles = @(
    'LICENSE'
    'README.md'
    'docs/README.tr.md'
    '.github/CONTRIBUTING.md'
    '.github/SUPPORT.md'
    'docs/ROADMAP.md'
    'docs/RELEASE_NOTES.md'
    'docs/USAGE.md'
    'docs/KULLANIM.tr.md'
    'docs/SECURITY.md'
    'docs/SECURITY.tr.md'
    'docs/RELEASE_PROCESS.md'
)

foreach ($relativePath in $requiredFiles) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required documentation file is missing: $relativePath"
    }
}

$englishReadme = Get-Content -LiteralPath (Join-Path $repositoryRoot 'README.md') -Raw
$turkishReadme = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/README.tr.md') -Raw
$englishGuide = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/USAGE.md') -Raw
$turkishGuide = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/KULLANIM.tr.md') -Raw

$requiredText = @(
    @{ Name = 'English README Alpha 2.1 status'; Text = $englishReadme; Pattern = 'Kvieta Alpha 2\.1' }
    @{ Name = 'Turkish README Alpha 2.1 status'; Text = $turkishReadme; Pattern = 'Kvieta Alpha 2\.1' }
    @{ Name = 'English Alpha 2.1 download'; Text = $englishReadme; Pattern = 'releases/download/kvieta-alpha-2\.1/Kvieta-Setup-Alpha-2\.1\.exe' }
    @{ Name = 'Turkish Alpha 2.1 download'; Text = $turkishReadme; Pattern = 'releases/download/kvieta-alpha-2\.1/Kvieta-Setup-Alpha-2\.1\.exe' }
    @{ Name = 'English Alpha 2.1 checksum guidance'; Text = $englishReadme; Pattern = 'attached `\.sha256` file' }
    @{ Name = 'Turkish Alpha 2.1 checksum guidance'; Text = $turkishReadme; Pattern = 'ekli `\.sha256` dosyasıyla' }
    @{ Name = 'English insights mode'; Text = $englishReadme; Pattern = 'Insights' }
    @{ Name = 'Turkish insights mode'; Text = $turkishReadme; Pattern = 'Farkındalık' }
    @{ Name = 'English family mode'; Text = $englishReadme; Pattern = 'Family' }
    @{ Name = 'Turkish family mode'; Text = $turkishReadme; Pattern = 'Aile' }
    @{ Name = 'English usage update guidance'; Text = $englishGuide; Pattern = 'Installation and update' }
    @{ Name = 'Turkish usage update guidance'; Text = $turkishGuide; Pattern = 'Kurulum ve güncelleme' }
    @{ Name = 'English uninstall guidance'; Text = $englishGuide; Pattern = 'Uninstall' }
    @{ Name = 'Turkish uninstall guidance'; Text = $turkishGuide; Pattern = 'Kaldırma' }
    @{ Name = 'English MIT link'; Text = $englishReadme; Pattern = '\[MIT License\]\(LICENSE\)' }
    @{ Name = 'Turkish MIT link'; Text = $turkishReadme; Pattern = '\[MIT Lisansı\]\(\.\./LICENSE\)' }
)

foreach ($check in $requiredText) {
    if ($check.Text -notmatch $check.Pattern) {
        throw "Documentation check failed: $($check.Name)"
    }
}

$staleClaims = @(
    @{ Name = 'English two-mode claim'; Text = $englishReadme; Pattern = 'offers two ways' }
    @{ Name = 'Turkish two-mode claim'; Text = $turkishReadme; Pattern = 'iki farklı kullanım biçimi' }
    @{ Name = 'English RC claim'; Text = $englishReadme; Pattern = 'release candidate \(RC\)' }
    @{ Name = 'Turkish RC claim'; Text = $turkishReadme; Pattern = 'sürüm adayı \(RC\)' }
)

foreach ($check in $staleClaims) {
    if ($check.Text -match $check.Pattern) {
        throw "Stale documentation claim found: $($check.Name)"
    }
}

$currentProductSurfacePaths = @(
    'README.md'
    'docs/README.tr.md'
    'docs/USAGE.md'
    'docs/KULLANIM.tr.md'
    'src/Kvieta.App/Localization/Strings.en.xaml'
    'src/Kvieta.App/Localization/Strings.tr.xaml'
    'src/Kvieta.SetupApp/SetupWindow.xaml'
    'src/Kvieta.SetupApp/SetupWindow.xaml.cs'
    'src/Kvieta.Core/Models/ProductTerminology.cs'
    '.github/ISSUE_TEMPLATE/bug_report.yml'
)
$currentProductSurfaces = ($currentProductSurfacePaths | ForEach-Object {
    Get-Content -LiteralPath (Join-Path $repositoryRoot $_) -Raw
}) -join "`n"
$retiredProductTerms = @(
    'Tracking only'
    'For myself'
    'For someone I manage'
    'Sadece takip'
    'Kendim için'
    'Yönettiğim biri için'
    'Strict · Guardian'
    'Gözetimli'
)

foreach ($term in $retiredProductTerms) {
    if ($currentProductSurfaces.IndexOf($term, [StringComparison]::Ordinal) -ge 0) {
        throw "Retired product term found on a current surface: $term"
    }
}

Write-Host "Documentation verification passed ($($requiredFiles.Count) required files, bilingual terminology/status/install/uninstall checks)."
