<div align="center">

**English** · [Türkçe](docs/README.tr.md)

# Otium

### All in good time.

Calm, local-first screen-time management for Windows — no account required.

![Windows](https://img.shields.io/badge/Windows-native-87946B?style=flat-square&labelColor=3F4437)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=3F4437)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=3F4437)
![Privacy](https://img.shields.io/badge/data-on%20device-87946B?style=flat-square&labelColor=3F4437)
![Status](https://img.shields.io/badge/version-v1.0.0--alpha.1-C9B98E?style=flat-square&labelColor=3F4437)
![AI assisted](https://img.shields.io/badge/development-AI%20assisted-87946B?style=flat-square&labelColor=3F4437)

</div>

> Otium makes computer time visible and manageable without turning it into punishment. Plans, rules, and usage history remain on the device; no cloud account is required.

## Download Alpha.1

[**Download Otium Setup for Windows x64**](https://github.com/Rel0adediso/Otium/releases/download/v1.0.0-alpha.1/Otium-Setup-1.0.0-alpha.1.exe)

`v1.0.0-alpha.1` is the first community prerelease for real-device testing. The
installer is self-contained, supports English and Turkish, and does not require
the .NET SDK. It is intentionally unsigned, so Windows SmartScreen may show an
**Unknown publisher** warning.

Verify the Setup EXE before running it:

```text
SHA-256: 8736c995fc3e7add6020c30fdc2fbf2d10d1aaaf1786cd64d9b10acfb3d76260
```

The standalone MSI, checksum files, release manifest, full notes, and known
limitations are available on the
[v1.0.0-alpha.1 release page](https://github.com/Rel0adediso/Otium/releases/tag/v1.0.0-alpha.1).

## Why Otium?

Otium offers three ways to use the same local-first foundation:

| Mode | Best for | What it does |
|---|---|---|
| **Tracking only** | Understanding computer habits | Measures configured app usage locally without applying restrictions. |
| **For myself** | Building a personal routine | Adds schedules, limits, breaks, and an optional Balanced session surface without requiring an administrator PIN. |
| **For someone I manage** | A standard Windows account managed by another administrator | Protects policy with an administrator PIN and Guardian, and restores the protected session after common termination attempts. |

## Highlights

### Time and schedules

- Per-day time windows and daily usage limits
- A controlled **Break** state after Win+L instead of automatic resume
- Date-specific temporary allowances that leave the weekly plan unchanged
- Administrator-approved extra time for the current day
- A timer that does not mistake watching or reading for inactivity

### Rhythm and history

- Daily and weekly views covering the last seven days
- Weekly total, daily average, and most-used configured application
- Break, limit, extra-time, and policy-change activity
- Ninety days of on-device history
- Personal progress, reclaimed-time, and opt-in foreground-app insights

### Application rules

- Blocked, limited, and unlimited application policies
- Per-application daily counters and termination at the configured limit
- Awareness tracking for configured unlimited applications
- Publisher, original filename, optional hash, package family, and launcher/child-process identification

### Security and data resilience

- Salted PBKDF2 administrator PIN verification
- Service-managed authoritative policy copy for Protected mode
- Progressive delay after failed PIN attempts
- Detection of clock rollback beyond five minutes
- Cross-process data-file locking
- Validated atomic JSON writes with a last-known-good `.bak` snapshot
- Automatic recovery from a corrupted primary file
- Loss-resistant merging of concurrent counters and history events

## Visual language

Otium combines warm cream and khaki surfaces with an olive-graphite dark theme in a compact Windows interface.

- System / Light / Dark appearance
- Live Turkish / English switching
- Thin custom title bar
- Collapsible, optically aligned navigation
- Compact seven-day schedule
- Theme-aware custom tray menu
- Information-dense but calm screens without oversized dashboard cards

## Project structure

| Component | Responsibility |
|---|---|
| `Otium.Core` | Scheduling, sessions, policies, models, and resilient local persistence |
| `Otium.App` | WPF Control Center, session surface, tray, and Windows integrations |
| `Otium.Core.SmokeTests` | Core behavior, security regressions, and real-process checks |
| Guardian | Windows service supervising the protected session and authoritative policy area |

The solution, projects, assemblies, and namespaces consistently use the **Otium** name.

## Current status

**v1.0.0-alpha.1 — community prerelease in field testing**

- The Release community package contains no development/test bypass.
- Local quality gates and the matching GitHub Actions build/package jobs pass.
- Setup EXE, MSI, checksums, and the release manifest are tied to source commit `4dd94ca`.
- Settings migration, recovery, Guardian communication, application rules, and session behavior have automated regression coverage.
- Basic protected-surface behavior passed a physical two-monitor test.
- Clean install, Guardian enrollment, expiry behavior, repair, and uninstall are now being validated on a separate Windows device.
- The wider Windows lifecycle, escape-path, DPI, and installer matrix remains open before final `v1.0.0`.

See the [current roadmap](docs/ROADMAP.md) for the remaining v1.0 validation and post-release goals, and the [release notes](docs/RELEASE_NOTES.md) for changes between versions.

## Run from source

Requirements: Windows and the .NET 10 SDK.

```powershell
dotnet run --project src/Otium.App/Otium.App.csproj
```

Open the protected session surface directly:

```powershell
dotnet run --project src/Otium.App/Otium.App.csproj -- --session
```

## Checks

```powershell
dotnet build Otium.slnx -c Release
dotnet run --project tests/Otium.Core.SmokeTests/Otium.Core.SmokeTests.csproj -c Release
```

## Security boundary

Protected mode is primarily designed for a **standard Windows account** managed through a separate administrator account. No desktop application can guarantee absolute resistance against someone with physical access and Windows administrator privileges.

Development-only test gates are not compiled into Public builds. See the [security boundaries](docs/SECURITY.md) for details.

See the [English user guide](docs/USAGE.md) or [Turkish user guide](docs/KULLANIM.tr.md) for installation, first-use, update, recovery, and removal steps. For help or a bug report, see [Support](.github/SUPPORT.md).

## Development approach

**AI-assisted development · Human-directed product.**

Product vision, direction, UX decisions, and hands-on testing are led by **Rel0adediso**. Architecture, implementation, and test development are carried out collaboratively with **OpenAI Codex**.

Rather than claiming “100% AI made,” Otium is documented accurately as a human-directed, AI-assisted product.

## License

Otium is open-source software released under the [MIT License](LICENSE). Security boundaries and platform limitations still apply; the license is not a promise that Protected mode is impossible to bypass.

---

<div align="center">

**Otium** · *All in good time.*

</div>
