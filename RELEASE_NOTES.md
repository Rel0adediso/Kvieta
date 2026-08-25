# Otium release notes

## v1.0.0-rc.1 — Usage modes and Guardian release candidate

`v1.0.0-rc.1` is a public release candidate, not the final v1.0 release. It consolidates the installer, recovery, diagnostics, usage-mode, personal-protection, and Guardian work completed after v0.16.1.

The final `v1.0.0` tag will only be created after the remaining Windows escape-path regression, signed installer, and lifecycle test requirements have been completed.

### Highlights

- Replaced the old technical mode names with three user-facing usage modes: **Tracking only**, **For myself**, and **For someone I manage**.
- Added Flexible, Balanced, and Guarded personal-protection levels.
- Added Guardian-backed personal protection without turning a self-selected PIN into an instant escape mechanism.
- Converted Flexible mode into a user-controlled manual focus session.
- Hardened the Control Center, session surface, tray, and single-instance window lifecycle.

### Flexible personal mode

- Removed the weekly schedule page and allowed-hour enforcement.
- Disabled forced daily-limit enforcement.
- Sessions now start only after an explicit user action.
- Added a stopwatch that starts from `00:00`, pauses during a break, resumes correctly, and resets when the session ends.
- Application rules apply only while a Flexible focus session is active.
- Guardian is never required, while local usage history continues to work.

### Balanced personal mode

- Retains weekly schedules, daily limits, application rules, and delayed relaxation of restrictions.
- Keeps Guardian disabled while preserving the personal change-delay policy.
- Reuses a single background session controller instead of creating new windows during navigation.
- Hides the Control Center from the taskbar before presenting the session surface.

### Guarded personal mode

- Added **Guarded · Guardian** as the strictest personal-protection level.
- Uses an internal cryptographic Guardian credential instead of a user-created administrator PIN.
- Lowering protection is queued using the configured personal change delay.
- Guardian applies an expired relaxation even when the Control Center is closed.
- Opening the Control Center does not silently disable enforcement.
- Windows administrator recovery remains the emergency uninstall and repair path.

### Managed and protected use

- Preserved administrator-PIN verification for **For someone I manage**.
- Fixed protected administrator exit and Control Center transitions.
- Strengthened Guardian enrollment, protected-policy synchronization, recovery, and version compatibility checks.
- Restricted test-only unlock behavior to Development builds.

### Window and startup reliability

- Fixed a Tracking-only startup crash caused by an invisible maximized WPF window.
- Prevented rapid Session Screen clicks from creating duplicate session surfaces.
- Prevented repeated Control Center requests from creating duplicate management windows.
- Added transition guards around session and Control Center navigation.
- Replaced the stale `Awareness` label with `Tracking only`.
- Normalized irrelevant personal-protection fields outside personal mode.

### Recovery, diagnostics, and privacy

- Added Guardian health and version compatibility reporting.
- Added privacy-safe diagnostic export without PINs, recovery secrets, window titles, or document content.
- Added last-known-good settings recovery and installer repair flows.
- Added delayed policy-change details and recovery audit events.
- Kept all usage data local to the device.

### Removed or deferred

- Removed unstable one-click application suggestions from this release.
- Moved reliable live suggestion refresh to the post-v1 roadmap.
- Kept AppLocker and WDAC integration as a future optional protection level because availability depends on Windows edition and policy.

### Known release blockers

- In Balanced mode, the session surface displayed after closing Otium can currently be minimized or sent behind other windows, allowing access to the desktop. It must be fixed and tested against the taskbar, `Alt+Tab`, `Win+D`, minimization, and related desktop-switching paths.
- The explanatory installer wizard and signed public MSI are incomplete.
- Clean install, upgrade, repair, uninstall, and rollback must be rerun with the final signed package.
- Reboot, Win+L, sleep, hibernation, Explorer restart, user switching, Remote Desktop, multiple monitors, and standard-user scenarios remain in the final Windows matrix.

### Validation completed

- Debug and Release builds complete with zero warnings and zero errors.
- Core smoke tests pass in both configurations.
- Settings migration, delayed relaxation, Guardian credentials, protected-policy synchronization, and manual Flexible sessions have automated regression coverage.
- A real-process startup diagnostic confirmed that the Tracking-only startup crash no longer occurs.

## v0.19.0 — Diagnostics and Guardian reliability

- Added Guardian health, service-state, protected-policy, and version compatibility checks.
- Added privacy-safe diagnostics export and security audit coverage.
- Restricted development unlock behavior to Development packages.
- Improved Guardian installation recovery, protected-policy restoration, and crash recovery.

## v0.18.0 — Recovery and security hardening

- Added Recovery Center tools for clock validation, settings restore, and installation repair.
- Added recovery-code-based administrator PIN reset and safer confirmations.
- Extended app identity with publisher trust, original filename, product metadata, SHA-256, package family, and process relationships.
- Improved recovery layout, explanations, temporary allowances, empty states, rule removal, and dashboard readability.

## v0.17.0 — Secure installer lifecycle

- Added Windows Installer support for install, upgrade, repair, and uninstall.
- Added Program Files installation, Start menu integration, and Guardian service lifecycle support.
- Added downgrade prevention and rollback validation.
- Added release-manifest verification for package name, version, architecture, size, SHA-256, and Authenticode signer identity.
- Added automated clean-install, repair, removal, upgrade, and rollback verification.
