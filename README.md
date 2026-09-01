<div align="center">

<img src="assets/branding/kvieta-mark.svg" alt="Kvieta" width="150" />

**English** · [Türkçe](docs/README.tr.md)

# Kvieta

### All in good time.

Calm, local-first screen-time management for Windows — no account required.

![Windows](https://img.shields.io/badge/Windows-native-87946B?style=flat-square&labelColor=3F4437)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=3F4437)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=3F4437)
![Privacy](https://img.shields.io/badge/data-on%20device-87946B?style=flat-square&labelColor=3F4437)
![Status](https://img.shields.io/badge/next-Kvieta_Alpha_2-C9B98E?style=flat-square&labelColor=3F4437)
![AI assisted](https://img.shields.io/badge/development-AI%20assisted-87946B?style=flat-square&labelColor=3F4437)

</div>

> Kvieta makes computer time visible and manageable without turning it into punishment. Plans, rules, and usage history remain on the device; no cloud account is required.

## Download Kvieta Alpha 2

[**Download Kvieta Setup for Windows x64**](https://github.com/Rel0adediso/Kvieta/releases/download/alpha-2/Kvieta-Setup-Alpha-2.exe)

**Kvieta Alpha 2 is the current community prerelease.** It adds a
trusted-phone recovery flow, a substantially more reliable Guardian and
administrator-exit lifecycle, safer repair/update handling, and many setup and
session-surface fixes. The public release title is **Kvieta Alpha 2**; its
package-safe Git tag and installer label use `alpha-2` / `Alpha-2` instead
of presenting the release as `v1.0.0-alpha.2`.

The installer is self-contained, supports English and Turkish, and does not require
the .NET SDK. It is intentionally unsigned, so Windows SmartScreen may show an
**Unknown publisher** warning.

Verify the Setup EXE before running it:

```text
SHA-256: 073bee551ce3d6a907eca67cbc76e237448a503eb4c4632b944127ad8ae1c090
```

The standalone MSI, checksum files, release manifest, full notes, and known
limitations are available on the
[Kvieta Alpha 2 release page](https://github.com/Rel0adediso/Kvieta/releases/tag/alpha-2).

## Why Kvieta?

Kvieta offers three ways to use the same local-first foundation:

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

Kvieta combines warm cream and khaki surfaces with an olive-graphite dark theme in a compact Windows interface.

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
| `Kvieta.Core` | Scheduling, sessions, policies, models, and resilient local persistence |
| `Kvieta.App` | WPF Control Center, session surface, tray, and Windows integrations |
| `Kvieta.Core.SmokeTests` | Core behavior, security regressions, and real-process checks |
| Guardian | Windows service supervising the protected session and authoritative policy area |

The solution, projects, assemblies, and namespaces consistently use the **Kvieta** name.

## Current status

**Kvieta Alpha 2 — community prerelease**

- The Release community package contains no development/test bypass.
- Local quality gates and the matching GitHub Actions build/package jobs pass.
- The Alpha 2 Setup EXE, MSI, checksums, and release manifest are tied to source commit `ca2181c`.
- Alpha 2 completed hands-on setup, trusted-phone, Guardian, administrator-exit, session, repair, and power-action testing before publication.
- Settings migration, recovery, Guardian communication, application rules, and session behavior have automated regression coverage.
- Basic protected-surface behavior passed a physical two-monitor test.
- Clean install, Guardian enrollment, expiry behavior, repair, and uninstall are now being validated on a separate Windows device.
- The wider Windows lifecycle, escape-path, DPI, and installer matrix remains open before final `v1.0.0`.

See the [current roadmap](docs/ROADMAP.md) for the remaining v1.0 validation and post-release goals, and the [release notes](docs/RELEASE_NOTES.md) for changes between versions.

## Run from source

Requirements: Windows and the .NET 10 SDK.

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj
```

Open the protected session surface directly:

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj -- --session
```

## Checks

```powershell
dotnet build Kvieta.slnx -c Release
dotnet run --project tests/Kvieta.Core.SmokeTests/Kvieta.Core.SmokeTests.csproj -c Release
```

## Security boundary

Protected mode is primarily designed for a **standard Windows account** managed through a separate administrator account. No desktop application can guarantee absolute resistance against someone with physical access and Windows administrator privileges.

Development-only test gates are not compiled into Public builds. See the [security boundaries](docs/SECURITY.md) for details.

See the [English user guide](docs/USAGE.md) or [Turkish user guide](docs/KULLANIM.tr.md) for installation, first-use, update, recovery, and removal steps. For help or a bug report, see [Support](.github/SUPPORT.md).

## Development approach

**AI-assisted development · Human-directed product.**

Product vision, direction, UX decisions, and hands-on testing are led by **Rel0adediso**. Architecture, implementation, and test development are carried out collaboratively with **OpenAI Codex**.

Rather than claiming “100% AI made,” Kvieta is documented accurately as a human-directed, AI-assisted product.

## License

Kvieta is open-source software released under the [MIT License](LICENSE). Security boundaries and platform limitations still apply; the license is not a promise that Protected mode is impossible to bypass.

---

<div align="center">

**Kvieta** · *All in good time.*

</div>
