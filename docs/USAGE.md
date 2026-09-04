# Kvieta usage and recovery guide

> Current status: **Kvieta Alpha 2.1**. Community packages are unsigned previews intended for validation; they are not final public releases.

## Installation and update

1. Use only a package from this repository's GitHub Releases page or one built from your own source checkout.
2. Open `Kvieta-Setup-<version>.exe` and select English or Turkish.
3. On a clean install, the wizard asks for the usage mode, protection level, device name, daily limit, Windows startup, and desktop shortcut preferences.
4. If Kvieta is already installed, setup opens the **Update/Repair** screen directly. A newer package upgrades, the same version offers repair, and an older package is blocked from downgrading the installation.
5. Administrator approval is requested only when Windows Installer needs it. New first-use settings are not saved if installation fails.

Kvieta is installed under `C:\Program Files\Kvieta` by default. User settings and history are stored under `%LOCALAPPDATA%\Kvieta`; protected policy and Guardian state are stored under `%ProgramData%\Kvieta`. Update and repair are designed to preserve these areas.

## First-use modes

- **Insights:** Measures configured application usage on the device without restrictions.
- **Personal:** Applies schedules and limits as a personal routine. Flexible remains user-controlled; Balanced maintains a session surface during an active time window.
- **Family:** Intended for a family member's standard Windows account managed through a separate administrator account. An administrator PIN and Guardian protect policy.

In Personal mode, **Quick focus** starts a 25, 50, or 90-minute session from Today or the tray menu. The focus target never extends the daily limit or the allowed schedule.

In Family mode, an administrator can grant extra time with the existing PIN-protected action while a session is active or paused; waiting for the daily limit to expire is not required.

Review the weekly schedule, daily limit, and application rules, then select **Save**. For protected use, store the administrator PIN and one-time recovery codes securely and separately.

## Application behaviors

- **Always blocked:** Terminates the identified application and related child processes when found running.
- **Timed:** Counts daily use and closes the application when its configured limit is reached.
- **Unlimited:** Does not block; when local measurement is enabled, usage can still appear for awareness.
- **Remove:** Removes only the Kvieta rule; it does not uninstall the program from Windows.

## System Health and Recovery Center

The system health section under Settings reports application, installer, Guardian, and local-data status separately.

- **Confirm clock:** Clears a clock warning after Windows administrator approval; it does not change the system clock.
- **Restore settings:** Restores the last validated settings snapshot without deleting usage history.
- **Repair installation:** Revalidates the application and Guardian installation without deleting schedules, PIN state, or history.
- **Diagnostic report:** Exports JSON that excludes PINs, recovery codes, window titles, and content.

If Guardian is missing or unhealthy, protected use does not silently continue without protection. Approve repair as an administrator; if the problem remains, attach the diagnostic report to a bug report.

## Uninstall

Use **Settings > Uninstall Kvieta** inside the app, or select Kvieta from Windows **Installed apps**. Windows Installer may request administrator approval.

Local settings and history are intended to survive uninstall, reinstall, and upgrade. Export anything you need before removing local data manually; data cleanup remains a separate, deliberate action during alpha.

## Known limitations

- Protected use targets a standard Windows account managed by a separate administrator; it does not promise absolute protection against a Windows administrator or physical disk access.
- Multi-monitor, mixed-DPI, sleep/hibernate, and the complete installer lifecycle still await final V1 matrix validation.
- The alpha test installer is unsigned and may trigger a Windows SmartScreen warning.

See [SECURITY.md](SECURITY.md) for the security boundary and [Support](../.github/SUPPORT.md) for help and reporting paths.
