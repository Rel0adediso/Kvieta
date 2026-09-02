<div align="center">

<img src="assets/branding/kvieta-mark.svg" alt="Kvieta logo" width="132" />

# Kvieta

### All in good time.

A calm, local-first way to understand and manage screen time on Windows.

[**Türkçe**](docs/README.tr.md)

![Windows](https://img.shields.io/badge/Windows-native-87946B?style=flat-square&labelColor=292B26)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=292B26)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=292B26)
![Privacy](https://img.shields.io/badge/privacy-local--first-87946B?style=flat-square&labelColor=292B26)
![Status](https://img.shields.io/badge/status-Alpha_2-C9B98E?style=flat-square&labelColor=292B26)
![License](https://img.shields.io/badge/license-MIT-87946B?style=flat-square&labelColor=292B26)

</div>

Kvieta helps make computer time visible and intentional without turning it into punishment. Schedules, rules, usage history, credentials, and recovery data stay on the Windows device. No Kvieta account is required.

## Download Kvieta Alpha 2

[**Download Kvieta Setup for Windows x64**](https://github.com/Rel0adediso/kvieta-app/releases/download/kvieta-alpha-2/Kvieta-Setup-Alpha-2.exe)

The self-contained setup supports English and Turkish and does not require the
.NET SDK. This community preview is intentionally unsigned, so Windows
SmartScreen may show an **Unknown publisher** warning.

```text
SHA-256: 44444d2c07add93b5b44e374b4de5a426e7b8759efced119c5252485edf0dd2b
```

The standalone MSI, checksum files, release manifest, detailed notes, and known
limitations are on the [Kvieta Alpha 2 release page](https://github.com/Rel0adediso/kvieta-app/releases/tag/kvieta-alpha-2).

> **Important:** Alpha 2 replaces every Alpha 1 package. It can be installed directly over an existing Kvieta installation while preserving settings, usage history, and protected policy.

## Choose the relationship you want with time

| Mode | Designed for | Experience |
|---|---|---|
| **Tracking only** | Understanding your habits | Records configured app usage locally without restricting anything. |
| **For myself** | Building a routine of your own | Adds schedules, limits, breaks, focus sessions, and optional personal protection. |
| **For someone I manage** | A standard Windows account managed by another administrator | Protects policy with an administrator PIN and the Kvieta Guardian service. |

## What Kvieta can do

| | |
|---|---|
| **Plan time** | Weekly schedules, daily limits, controlled breaks, temporary allowances, and administrator-approved extra time. |
| **Manage apps** | Blocked, limited, or unlimited rules with daily counters and resilient process identification. |
| **Show the rhythm** | Seven-day insights, a 90-day local history, weekly totals, daily averages, and activity events. |
| **Stay recoverable** | Offline recovery codes plus optional trusted-phone enrollment, QR transfer, revocation, and local PIN-reset approval. |
| **Protect policy** | Guardian-backed supervision, protected policy storage, health checks, repair paths, and verified administrator exit. |
| **Survive real life** | Atomic saves, last-known-good backups, corruption recovery, clock-rollback detection, and concurrent-write protection. |

## New in Kvieta Alpha 2

- A compact wheel-style time picker with hour and minute columns for weekly schedules.
- A refined Recovery Center with clearer health information and safer repair actions.
- Stable one-time recovery codes that can be copied or downloaded again without silently replacing the existing set.
- Direct clock-recovery controls after Windows time corrections, including automatic recovery when trusted time catches up.
- Administrator and Control Center time no longer consumes the managed user's daily allowance.
- More reliable Guardian startup, repair, PIN authorization, and administrator transitions based on physical-device testing.
- Sharper vector branding and improved bilingual layouts throughout the application and installer.

## Private by design

Kvieta has no required cloud account and does not send screen-time history to a Kvieta service. The optional phone companion is served by the PC and currently requires the phone to reach that PC on the local network. Approval messages are signed, short-lived, origin-checked, rate-limited, and contain neither the administrator PIN nor recovery codes.

## Project status

**Kvieta Alpha 2 is the current community preview.**

- The source is usable today and the Windows package pipeline is in place.
- Debug and Release builds, smoke tests, documentation checks, and public-build bypass checks run as quality gates.
- Community installers are intentionally unsigned, so Windows SmartScreen may show an **Unknown publisher** warning.
- The Alpha 2 Setup EXE, MSI, checksums, and manifest are tied to source commit `0444bd2`.
- The broader installer, DPI, Guardian, escape-path, and Windows lifecycle matrix remains open before final `v1.0.0`.

See the [roadmap](docs/ROADMAP.md) for the remaining validation work and [release notes](docs/RELEASE_NOTES.md) for the detailed history.

## Run from source

Requirements: Windows and the .NET 10 SDK.

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj
```

Open the session surface directly:

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj -- --session
```

Run the main quality checks:

```powershell
dotnet build Kvieta.slnx -c Release
dotnet run --project tests/Kvieta.Core.SmokeTests/Kvieta.Core.SmokeTests.csproj -c Release
```

<details>
<summary><strong>Project structure</strong></summary>

| Component | Responsibility |
|---|---|
| `Kvieta.Core` | Scheduling, sessions, policies, models, and resilient local persistence |
| `Kvieta.App` | WPF Control Center, session surface, tray, and Windows integrations |
| Guardian service | Windows service supervising protected sessions and authoritative policy |
| `Kvieta.SetupApp` | Bilingual setup, update, repair, configuration, and removal experience |
| `Kvieta.Core.SmokeTests` | Core behavior, security regressions, and real-process checks |

</details>

## Security boundary

Protected mode is designed primarily for a **standard Windows account** managed through a separate administrator account. No desktop application can guarantee absolute resistance against someone with physical access and Windows administrator privileges. Development-only test gates are not compiled into Public builds. Read the [security model and limitations](docs/SECURITY.md) before relying on protected mode.

## Documentation

- [English user guide](docs/USAGE.md) · [Türkçe kullanım rehberi](docs/KULLANIM.tr.md)
- [Roadmap](docs/ROADMAP.md) · [Release notes](docs/RELEASE_NOTES.md)
- [Support](.github/SUPPORT.md) · [Contributing](.github/CONTRIBUTING.md)

## Development approach

**Human-directed product · AI-assisted development.** Product direction, UX decisions, and hands-on testing are led by [Rel0adediso](https://github.com/Rel0adediso). Architecture, implementation, and test development are carried out collaboratively with OpenAI Codex.

Kvieta is open-source software released under the [MIT License](LICENSE).

---

<div align="center">

**Kvieta** · *All in good time.*

</div>
