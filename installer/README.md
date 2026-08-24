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

Verify a package before install or update:

```powershell
.\scripts\verify-installer.ps1 `
  -ManifestPath .\artifacts\installer\0.17.0\release-manifest.json
```
Build and installer outputs are intentionally excluded from Git.

The desktop shortcut is disabled by default. It can be enabled in an unattended
install with:

```powershell
msiexec /i Otium-0.17.0-win-x64.msi INSTALLDESKTOPSHORTCUT=1
```

User settings and usage history remain under the user's local application-data
directory. Protected policy data remains under `ProgramData\Otium`; neither area
is owned or removed by the MSI during an upgrade.
