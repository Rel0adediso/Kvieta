# Otium installer

The installer is an x64, per-machine MSI built with the WiX Toolset. It installs
Otium under `Program Files\Otium`, creates a Start menu shortcut, and lets Windows
Installer own the Guardian service lifecycle.

Build a release package from the repository root:

```powershell
.\scripts\build-installer.ps1 -Version 0.17.0
```

Outputs are written to `artifacts\installer\<version>` together with a SHA-256
checksum and `release-manifest.json`. Keeping every version in its own directory
prevents a newer build from deleting the previous rollback package.
The verification and manual-update scripts are copied into the same release
directory so the release artifact is self-contained.

Verify a package before install or update:

```powershell
.\scripts\verify-installer.ps1 `
  -ManifestPath .\artifacts\installer\0.17.0\release-manifest.json
```

Perform a verified manual update with an automatic rollback package:

```powershell
.\scripts\install-update.ps1 `
  -ManifestPath .\artifacts\installer\0.17.1\release-manifest.json `
  -RollbackManifestPath .\artifacts\installer\0.17.0\release-manifest.json
```

The updater rejects same-version and downgrade attempts, verifies both packages
before elevation and again afterward, checks the installed executable and Guardian
service, and restores the previous verified MSI if post-install health checks fail.
Build and installer outputs are intentionally excluded from Git.

The desktop shortcut is disabled by default and can be selected from the MSI's
feature screen. It can also be enabled in an unattended install with:

```powershell
msiexec /i Otium-0.17.0-win-x64.msi ADDLOCAL=MainFeature,DesktopShortcutFeature
```

User settings and usage history remain under the user's local application-data
directory. Protected policy data remains under `ProgramData\Otium`; neither area
is owned or removed by the MSI during an upgrade.
