# Otium usage and recovery guide

> Current status: `v1.0.0-alpha`. Test packages are unsigned and intended only for local development and validation; they are not final public releases.

## Installation and update

1. Use only a package from this repository's GitHub Releases page or one built from your own source checkout.
2. Open `Otium-Setup-<version>.exe` and select English or Turkish.
3. On a clean install, the wizard asks for the usage mode, protection level, device name, daily limit, Windows startup, and desktop shortcut preferences.
4. If Otium is already installed, setup opens the **Update/Repair** screen directly. A newer package upgrades, the same version offers repair, and an older package is blocked from downgrading the installation.
5. Administrator approval is requested only when Windows Installer needs it. New first-use settings are not saved if installation fails.

Otium is installed under `C:\Program Files\Otium` by default. User settings and history are stored under `%LOCALAPPDATA%\Otium`; protected policy and Guardian state are stored under `%ProgramData%\Otium`. Update and repair are designed to preserve these areas.

## First-use modes

- **Tracking only:** Measures configured application usage on the device without restrictions.
- **For myself:** Applies schedules and limits as a personal routine. Flexible remains user-controlled; Balanced maintains a session surface during an active time window.
- **For someone I manage:** Intended for a standard Windows account managed through a separate administrator account. An administrator PIN and Guardian protect policy.

Review the weekly schedule, daily limit, and application rules, then select **Save**. For protected use, store the administrator PIN and one-time recovery codes securely and separately.

## Application behaviors

- **Always blocked:** Terminates the identified application and related child processes when found running.
- **Timed:** Counts daily use and closes the application when its configured limit is reached.
- **Unlimited:** Does not block; when local measurement is enabled, usage can still appear for awareness.
- **Remove:** Removes only the Otium rule; it does not uninstall the program from Windows.

## System Health and Recovery Center

The system health section under Settings reports application, installer, Guardian, and local-data status separately.

- **Confirm clock:** Clears a clock warning after Windows administrator approval; it does not change the system clock.
- **Restore settings:** Restores the last validated settings snapshot without deleting usage history.
- **Repair installation:** Revalidates the application and Guardian installation without deleting schedules, PIN state, or history.
- **Diagnostic report:** Exports JSON that excludes PINs, recovery codes, window titles, and content.

If Guardian is missing or unhealthy, protected use does not silently continue without protection. Approve repair as an administrator; if the problem remains, attach the diagnostic report to a bug report.

## Uninstall

Use **Settings > Uninstall Otium** inside the app, or select Otium from Windows **Installed apps**. Windows Installer may request administrator approval.

Local settings and history are intended to survive uninstall, reinstall, and upgrade. Export anything you need before removing local data manually; data cleanup remains a separate, deliberate action during alpha.

## Known limitations

- Protected use targets a standard Windows account managed by a separate administrator; it does not promise absolute protection against a Windows administrator or physical disk access.
- Multi-monitor, mixed-DPI, sleep/hibernate, and the complete installer lifecycle still await final V1 matrix validation.
- The alpha test installer is unsigned and may trigger a Windows SmartScreen warning.

See [SECURITY.md](SECURITY.md) for the security boundary and [Support](../.github/SUPPORT.md) for help and reporting paths.
