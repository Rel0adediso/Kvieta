<div align="center">

**English** · [Türkçe](README.tr.md)

# Otium

### All in good time.

Calm, local-first screen-time management for Windows — no account required.

![Windows](https://img.shields.io/badge/Windows-native-87946B?style=flat-square&labelColor=3F4437)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=3F4437)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=3F4437)
![Privacy](https://img.shields.io/badge/data-on%20device-87946B?style=flat-square&labelColor=3F4437)
![Status](https://img.shields.io/badge/version-v1.0.0%20RC-C9B98E?style=flat-square&labelColor=3F4437)
![AI assisted](https://img.shields.io/badge/development-AI%20assisted-87946B?style=flat-square&labelColor=3F4437)

</div>

> Otium makes computer time visible and manageable without turning it into punishment. Plans, rules, and usage history remain on the device; no cloud account is required.

## Why Otium?

Otium offers two ways to use the same policy and session engine:

| For myself | For someone I manage |
|---|---|
| Delays impulsive attempts to relax self-imposed rules. | Protects rules with an administrator PIN and Guardian service. |
| Helps people build their own routine without requiring a PIN. | Resists common bypasses by a standard Windows user. |
| Keeps counting while the Control Center is open or minimized. | Can restore the session surface after it is forcibly terminated. |

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
- Planned personal progress, reclaimed-time, and foreground-app insights

### Application rules

- Blocked, limited, and unlimited application policies
- Per-application daily counters and termination at the configured limit
- Awareness tracking for configured unlimited applications
- Planned publisher, original filename, hash, and launcher/child-process identification

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

**v1.0.0 local release candidate (RC)**

- Release build: `0` warnings, `0` errors
- Single-file, self-contained Windows package
- Matching Turkish and English localization resources
- Smoke coverage for recovery, migration, locking, concurrent writes, overnight schedules, and pending policy merges
- Hardened Guardian IPC, protected policy synchronization, ProgramData access, and credential transitions
- Completed v0.16 rhythm foundation with opt-in, local-only foreground app awareness and separate policy counters
- Completed v0.16.1 motion language with short, accessible, non-blocking transitions and micro-interactions
- Completed v0.17 installer lifecycle with an optional desktop shortcut, MSI-managed Guardian, protected uninstall, verified update packages, rollback, and downgrade prevention
- Completed v0.18 hardening: public builds contain no test unlock path; one-time recovery codes can reset the PIN after Windows administrator approval
- Guardian IPC verifies the signed installed client and uses nonce/HMAC/replay protection with persistent throttling and local security audit
- Monotonic clock checks distinguish reboot/time-zone changes from forward/rollback manipulation and retain an administrator recovery path
- Application Identity 2.0 combines path, trusted publisher, original filename, product metadata, optional SHA-256, package family, launcher, and child-process relationships
- Guardian health checks, safe repair, and privacy-safe diagnostic export are complete
- Real Windows tests confirmed automatic recovery after terminating the protected session and crashing the Guardian service
- Code signing for the final public MSI and the reboot, sleep/hibernate, and multi-monitor validation matrix remain open

See the [current roadmap](ROADMAP.md) for the remaining v1.0 validation and post-release goals.

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

Development-only test gates are not intended for public releases. A complete separation between test and public builds is included in the roadmap.

## Development approach

**AI-assisted development · Human-directed product.**

Product vision, direction, UX decisions, and hands-on testing are led by **Rel0adediso**. Architecture, implementation, and test development are carried out collaboratively with **OpenAI Codex**.

Rather than claiming “100% AI made,” Otium is documented accurately as a human-directed, AI-assisted product.

---

<div align="center">

**Otium** · *Her şeyin bir zamanı var.*

</div>
