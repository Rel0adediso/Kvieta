# Otium installer

The installer is an x64, per-machine MSI built with the WiX Toolset. It installs
Otium under `Program Files\Otium`, creates a Start menu shortcut, and lets Windows
Installer own the Guardian service lifecycle.

Build a release package from the repository root:

```powershell
.\scripts\build-installer.ps1 `
  -Version 1.0.0 `
  -SigningCertificateThumbprint <trusted-code-signing-certificate-thumbprint>
```

Outputs are written to `artifacts\installer\<version>` together with a SHA-256
checksum and `release-manifest.json`. Keeping every version in its own directory
prevents a newer build from deleting the previous rollback package.
The verification and manual-update scripts are copied into the same release
directory so the release artifact is self-contained. Release builds require a
non-expired code-signing certificate with a private key plus the Windows SDK
`signtool.exe`. The published executable, MSI, verifier, and updater are all
timestamped and Authenticode-signed with that certificate.

Verify a package before install or update:

```powershell
.\artifacts\installer\1.0.0\verify-installer.ps1 `
  -ManifestPath .\artifacts\installer\1.0.0\release-manifest.json
```

Perform a verified manual update with an automatic rollback package:

```powershell
.\artifacts\installer\1.0.1\install-update.ps1 `
  -ManifestPath .\artifacts\installer\1.0.1\release-manifest.json `
  -RollbackManifestPath .\artifacts\installer\1.0.0\release-manifest.json
```

The updater rejects same-version and downgrade attempts, verifies both packages
before elevation and again afterward, requires the updater, verifier, MSI, and
installed release to use the same pinned Authenticode signer, checks the installed
executable and Guardian service, and restores the previous verified MSI if
post-install health checks fail.
Build and installer outputs are intentionally excluded from Git.

The application is already compressed by the .NET single-file publisher. The MSI
therefore uses the fast MSZIP cabinet level instead of applying the slower default
LZX recompression to the same executable during every release build. The build
scripts also give WiX an ASCII-only temporary path because Windows cabinet tooling
cannot reliably process a Windows user profile path containing Turkish characters.

Build an unsigned development installer for local testing (never for distribution):

```powershell
.\scripts\build-test-installer.ps1 -Version 1.0.0 -ReleaseLabel 1.0.0-alpha
```

Test installers are written to `artifacts\installer-test\<version>`. They contain
the development build (including its explicit test bypass) and are intentionally
separate from signed release artifacts.

The user-facing `Otium-Setup-<release-label>.exe` is a self-contained, bilingual WPF
setup experience with the MSI embedded inside it. It follows the Windows app
theme, explains Otium, detects existing settings, collects mode-specific first-run
choices, requests elevation only for the MSI transaction, saves settings only
after a successful install, and launches the correct Otium surface. The standalone
MSI remains beside it for managed and unattended deployment.
Development packages explicitly allow reinstalling another build with the same
numeric MSI version, so alpha iterations do not pretend to be patch releases.
Signed public builds keep same-version upgrades disabled.

On an interactive first install, the completion page offers a checked-by-default
option to launch Otium. The launch action runs as the installing user and is not
scheduled for repair, uninstall, upgrade, or unattended installation.

The desktop shortcut is disabled by default and can be selected from the MSI's
feature screen. It can also be enabled in an unattended install with:

```powershell
msiexec /i Otium-1.0.0-win-x64.msi ADDLOCAL=MainFeature,DesktopShortcutFeature
```

User settings and usage history remain under the user's local application-data
directory. Protected policy data remains under `ProgramData\Otium`; neither area
is owned or removed by the MSI during an upgrade.
