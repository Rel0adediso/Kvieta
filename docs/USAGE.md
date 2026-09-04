# Kvieta usage and recovery guide

> Current status: **Kvieta Alpha 3**. Community packages are unsigned previews intended for validation; they are not final public releases.

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

In Personal mode, **Quick focus** starts a 25, 50, or 90-minute session from Today or the tray menu. Today also accepts a custom duration and can repeat the last focus duration, stored only in a separate local preference file. The focus target never extends the daily limit or the allowed schedule.

Today combines current use and remaining time with the three most-used applications, the change from yesterday, and the active or next scheduled window. Empty and first-day states are shown without inventing a comparison.

For a measured application, **Create rule** offers a daily limit, availability only inside the plan, blocking during focus, unrestricted use, or a permanent block without opening a file picker. Because usage history deliberately stores the executable name rather than its full path, an application that has no existing rule must be running while its first rule is created. Changes take effect after **Save** and continue to follow Personal/Family approval rules.

At 15, 5, and 1 minute remaining, the session surface offers a calm wrap-up card: confirm that work is saved, take a controlled break, request more time when the mode permits it, or open Control Center to plan tomorrow.

**Rhythm Streak** rewards one meaningful daily outcome: reviewing the daily summary in Insights, completing a focus session in Flexible Personal, or keeping the daily balance in planned Personal and Family use. Rest days do not break or advance the streak. Every seven successful days earns one of up to two Rhythm Protectors, and milestones appear at 3, 7, 14, 30, 50, and 100 days. Approved temporary allowances and recovery days do not consume a Protector or break the streak. The weekly summary includes focus time and rising/falling application trends. Its optional 1200×630 share card is generated locally and excludes application names. Suggestions can be applied, hidden permanently on the device, or postponed until the next day.

During setup, five editable intent templates provide a useful starting point: See my usage, Focus, Gaming routine, Wind down, and Family routine. A template selects the matching usage mode and, when relevant, fills a weekly schedule that can still be changed before installation.

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
