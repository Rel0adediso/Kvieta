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
![Status](https://img.shields.io/badge/status-Alpha_2.1-C9B98E?style=flat-square&labelColor=292B26)
![License](https://img.shields.io/badge/license-MIT-87946B?style=flat-square&labelColor=292B26)

</div>

Kvieta helps make computer time visible and intentional without turning it into punishment. Schedules, rules, usage history, credentials, and recovery data stay on the Windows device. No Kvieta account is required.

## Download Kvieta Alpha 2.1

[**Download Kvieta Setup for Windows x64**](https://github.com/Rel0adediso/kvieta-app/releases/download/kvieta-alpha-2.1/Kvieta-Setup-Alpha-2.1.exe)

The self-contained setup supports English and Turkish and does not require the
.NET SDK. This community preview is intentionally unsigned, so Windows
SmartScreen may show an **Unknown publisher** warning.

The standalone MSI, checksum files, release manifest, detailed notes, and known
limitations are on the [Kvieta Alpha 2.1 release page](https://github.com/Rel0adediso/kvieta-app/releases/tag/kvieta-alpha-2.1). Verify the Setup EXE with the attached `.sha256` file before running it.

> **Important:** Alpha 2.1 replaces earlier Alpha packages. It can be installed directly over an existing Kvieta installation while preserving settings, usage history, and protected policy.

## Choose the relationship you want with time

| Mode | Designed for | Experience |
|---|---|---|
| **Insights** | Understanding your habits | Records configured app usage locally without restricting anything. |
| **Personal** | Building a routine of your own | Adds schedules, limits, breaks, focus sessions, and optional personal protection. |
| **Family** | A family member's standard Windows account | Protects policy with an administrator PIN and the Kvieta Guardian service. |

## What Kvieta can do

| | |
|---|---|
| **Plan time** | Weekly schedules, daily limits, controlled breaks, temporary allowances, and administrator-approved extra time. |
| **Manage apps** | Blocked, limited, or unlimited rules with daily counters and resilient process identification. |
| **Show the rhythm** | Seven-day insights, a 90-day local history, weekly totals, daily averages, and activity events. |
| **Stay recoverable** | Offline recovery codes plus optional trusted-phone enrollment, QR transfer, revocation, and local PIN-reset approval. |
| **Protect policy** | Guardian-backed supervision, protected policy storage, health checks, repair paths, and verified administrator exit. |
| **Survive real life** | Atomic saves, last-known-good backups, corruption recovery, clock-rollback detection, and concurrent-write protection. |

## New in Kvieta Alpha 2.1

- A clearer, roomier dashboard with readable seven-day activity bars and application usage cards.
- Weekly schedule planning directly in setup for modes that use a managed plan.
- More reliable trusted-device enrollment, transfer, and administrator approval flows.
- Refined administrator, bonus-time, tray, session, and setup surfaces in both themes.
- A deliberate uninstall flow with optional local-data removal and a clear completion result.
- Additional scheduling, Guardian identity, and local companion regression coverage.

## Private by design

Kvieta has no required cloud account and does not send screen-time history to a Kvieta service. The optional phone companion is served by the PC and currently requires the phone to reach that PC on the local network. Approval messages are signed, short-lived, origin-checked, rate-limited, and contain neither the administrator PIN nor recovery codes.

## Project status

**Kvieta Alpha 2.1 is the current community preview.**

- The source is usable today and the Windows package pipeline is in place.
- Debug and Release builds, smoke tests, documentation checks, and public-build bypass checks run as quality gates.
- Community installers are intentionally unsigned, so Windows SmartScreen may show an **Unknown publisher** warning.
- The Alpha 2.1 Setup EXE, MSI, checksums, and manifest identify their exact source commit.
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
