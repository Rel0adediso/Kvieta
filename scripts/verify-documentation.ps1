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
    @{ Name = 'English README Alpha 2 status'; Text = $englishReadme; Pattern = 'Kvieta Alpha 2' }
    @{ Name = 'Turkish README Alpha 2 status'; Text = $turkishReadme; Pattern = 'Kvieta Alpha 2' }
    @{ Name = 'English Alpha 2 download'; Text = $englishReadme; Pattern = 'releases/download/kvieta-alpha-2/Kvieta-Setup-Alpha-2\.exe' }
    @{ Name = 'Turkish Alpha 2 download'; Text = $turkishReadme; Pattern = 'releases/download/kvieta-alpha-2/Kvieta-Setup-Alpha-2\.exe' }
    @{ Name = 'English Alpha 2 checksum'; Text = $englishReadme; Pattern = '44444d2c07add93b5b44e374b4de5a426e7b8759efced119c5252485edf0dd2b' }
    @{ Name = 'Turkish Alpha 2 checksum'; Text = $turkishReadme; Pattern = '44444d2c07add93b5b44e374b4de5a426e7b8759efced119c5252485edf0dd2b' }
    @{ Name = 'English tracking mode'; Text = $englishReadme; Pattern = 'Tracking only' }
    @{ Name = 'Turkish tracking mode'; Text = $turkishReadme; Pattern = 'Sadece takip' }
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

Write-Host "Documentation verification passed ($($requiredFiles.Count) required files, bilingual mode/status/install/uninstall checks)."
