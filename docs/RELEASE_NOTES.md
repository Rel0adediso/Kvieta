# Kvieta release notes

## Kvieta Alpha 3 — Current community preview

Kvieta Alpha 3 brings the pre-V1 product experience into one coherent,
purpose-led flow. It keeps the numeric MSI version at `1.0.0` so existing Alpha
installations can be serviced in place. The public package label is `Alpha-3`
and the GitHub tag is `kvieta-alpha-3`.

### Highlights

- Added five editable setup templates for usage awareness, focus, gaming,
  winding down, and family routines; setup now previews the first Rhythm goal.
- Reworked Today around the user's next useful action: top applications, a
  comparison with yesterday, the current or next plan, custom Quick Focus, and
  repeat-last-focus.
- Added direct rule creation from measured application cards for daily limits,
  plan-only access, focus blocking, unrestricted use, and permanent blocking.
- Added calm 15, 5, and 1-minute wrap-up prompts with work-saved, controlled
  break, permitted extra-time, and direct tomorrow-planning actions.

### Rhythm Streak and weekly review

- Added a local Rhythm Streak with one mode-appropriate daily goal, rest days,
  best-streak tracking, milestones, and up to two earned Rhythm Protectors.
- Made Protector use visible and kept approved temporary allowances, extra time,
  Guardian failures, and recovery actions from unfairly breaking the streak.
- Expanded the weekly review with completed focus time and the most increased
  and decreased application trends.
- Added persistent apply, remind-tomorrow, and hide choices for local
  suggestions.
- Replaced plain-text sharing with an on-device 1200×630 image card that excludes
  application names and is never uploaded automatically.

### Behavior, security, and migration

- Extra time can be requested before the daily allowance reaches zero, while
  Family and protected flows retain their existing administrator checks.
- Session warnings now appear on the active session surface; warning actions
  cannot bypass permission checks and tomorrow planning opens the Plan page.
- Application identity and enforcement cover the new rule behaviors without
  storing full executable paths in usage history.
- Existing settings and protected policy remain compatible. Usage history is
  migrated locally to schema 7 to preserve focus completion and Rhythm fairness
  markers; separate focus and Rhythm preference files contain convenience state
  only.

### Validation and known limitations

- Formatting verification, Debug and Release builds, Debug and Release smoke
  tests, bilingual documentation checks, public-build bypass inspection, package
  metadata checks, release-manifest verification, and SHA-256 generation are
  required for this package.
- This is an intentionally unsigned community prerelease. Windows SmartScreen
  may display an unknown-publisher warning.
- Broad physical-device coverage for clean install and Alpha upgrade, Guardian
  lifecycle, multi-monitor/DPI, sleep/hibernate, repair/uninstall, and extended
  real-world use remains open before final `v1.0.0`.

## Kvieta Alpha 2.1 — Previous community preview

Kvieta Alpha 2.1 is a usability and reliability update built from the Alpha 2
field-feedback branch. It keeps the numeric MSI version at `1.0.0` so existing
Alpha installations can be serviced in place. The public package label is
`Alpha-2.1` and the GitHub tag is `kvieta-alpha-2.1`.

### Setup and lifecycle

- Added mode-aware weekly planning directly to setup. Tracking-only users no
  longer choose a daily limit, while managed modes can finish their initial plan
  before the first launch.
- Rounded and aligned setup inputs and refined the bilingual onboarding layout.
- Replaced the immediate in-app uninstall action with a confirmation window,
  optional local-data cleanup, progress feedback, and a clear completion result.

### Dashboard and interaction polish

- Increased the useful space for application usage cards and restored their
  visual hierarchy.
- Improved the seven-day chart's label contrast, spacing, progress visibility,
  and selected-day state.
- Refined administrator PIN, bonus-time, session, tray, and manager-device
  approval surfaces across light and dark themes.

### Reliability and validation

- Hardened trusted-device enrollment and transfer validation, Guardian client
  identity handling, and schedule evaluation edge cases.
- Updated the local companion experience and its generated distribution bundle.
- Expanded smoke coverage for scheduling, managed-device verification, and the
  new lifecycle behavior.
- The package remains an intentionally unsigned community prerelease; Windows
  SmartScreen may display an unknown-publisher warning.

## Kvieta Alpha 2 — Previous community preview

Kvieta Alpha 2 turns the Alpha 1 field fixes into a single recommended preview.
It preserves existing settings, usage history, recovery material, and protected
policy when installed over an earlier Alpha package.

The unsigned package was built from clean source commit `0444bd2`. Setup EXE
SHA-256: `44444d2c07add93b5b44e374b4de5a426e7b8759efced119c5252485edf0dd2b`.
The package label is `Alpha-2`; the future GitHub release uses
`kvieta-alpha-2` because the pre-rename Otium history already contains an
`alpha-2` tag.

### Scheduling and recovery

- Replaced the four separate schedule selectors with a compact time picker whose
  hour and minute columns scroll inside one popup.
- Refined the Recovery Center layout, version presentation, and repair paths.
- Recovery codes remain stable when reopened and can be copied or downloaded
  again; opening the window no longer silently invalidates the previous set.
- Clock protection can be cleared directly after Windows time is corrected and
  now recovers automatically when trusted wall time catches up.

### Protected session reliability

- Administrator and Control Center time no longer consumes the managed user's
  daily allowance; accounting resumes only after returning to the session surface.
- Kept administrator PIN and bonus-time dialogs above the protected session
  surface and retained the Guardian identity correction from Alpha 1 hotfixes.
- Improved Guardian health reporting and recovery behavior observed during
  physical-device installation and upgrade testing.

### Presentation and packaging

- Continued the crisp vector Kvieta identity across the application, setup,
  Windows executable, and documentation.
- Passed Release build, formatting, smoke, package metadata, embedded MSI,
  manifest, and SHA-256 verification gates.
- Remains an intentionally unsigned community prerelease, so Windows SmartScreen
  may display an unknown-publisher warning.

## Kvieta Alpha 1 Hotfix 2 — Visible administrator dialogs

This hotfix keeps administrator PIN and bonus-time dialogs visibly above the
protected full-screen session surface.

The unsigned package was built from clean source commit `56c2462` and published
as the `alpha-1-hotfix-2` GitHub prerelease. Setup EXE SHA-256:
`0c9a974072929e47369efdd951bdc42341a836814aa447f48d8299fbf70e5f72`.

- Session-owned WPF dialogs now inherit the topmost z-order required by the
  protected surface instead of relying on ownership alone.
- Administrator PIN dialogs explicitly activate and focus the PIN field after
  loading while preserving modal-dialog focus recovery protections.
- The change covers administrator exit and extra-time approval without weakening
  the full-screen session surface or exposing the desktop.

## Kvieta Alpha 1 Hotfix 1 — Guardian PIN authorization

This hotfix corrects a public-package identity check that could make every valid
administrator PIN appear incorrect in Protected mode.

The unsigned package was built from clean source commit `4ed3210` and published
as the `alpha-1-hotfix-1` GitHub prerelease. Setup EXE SHA-256:
`e13297d9713acdcae13ec67473749aebb03b864c9f27544a3d5cfb3ac0b8f13b`.

- Public builds use a human-readable product version such as
  `Alpha-1+<source-commit>`, while Windows Installer registers the numeric version
  `1.0.0` and the executable carries numeric file version `1.0.0.0`.
- Guardian previously attempted to parse the human-readable product version as a
  numeric `Version`. Parsing failed, so the otherwise valid, installer-managed,
  SHA-256-matched client was rejected before PIN verification.
- Guardian now reads the numeric file version and normalizes three- and four-part
  Windows versions before comparing them.
- Regression coverage now exercises real Kvieta assembly metadata and the
  `1.0.0` / `1.0.0.0` equivalence used by community-package authorization.

Existing Alpha 1 installations require this hotfix package; waiting, repairing
the original package, or resetting the PIN cannot correct the affected binary.

## Kvieta Alpha 1 — First Kvieta-branded community preview

Kvieta Alpha 1 is the first community preview under the Kvieta identity. The
unsigned package was built from clean source commit `9e68876` and published as
the `alpha-1` GitHub prerelease.

### Kvieta identity

- Renamed the solution, projects, assemblies, namespaces, services, installer,
  storage locations, protocol identifiers, documentation, and repository identity.
- Added the asymmetric Kvieta mark across the application, setup flow, tray,
  executable, MSI registration, and project documentation.
- Replaced scaled bitmap branding in WPF surfaces with vector templates and added
  a multi-resolution Windows icon for crisp rendering at common system sizes.

### Product baseline

- Carries forward the three usage modes, schedules, limits, breaks, focus sessions,
  app rules, local insights, trusted-phone recovery, and Guardian protection.
- Includes the setup, update, repair, policy handoff, administrator-exit, session,
  recovery, and power-action reliability work completed during the earlier preview cycle.
- Remains local-first and account-free; trusted-phone approval runs over the local
  network and never sends the administrator PIN or recovery codes to the phone.

### Release gate

- Local Debug/Release builds, smoke tests, documentation validation, public-build
  bypass checks, and package verification are required before publication.
- The community installer is intentionally unsigned. The published Setup EXE
  SHA-256 is `a29169986307434d819e1d7dbb11c30d768058548ce1ef1a166978b93fc87871`;
  the MSI SHA-256 is `29334604fec9f6f577bb830951446c7533bf6e7cb5493fa799ac075e18838fdb`.
- The pre-rename Otium Alpha 2 entry below is retained as historical development
  context from before the Kvieta-branded release line.

## Otium Alpha 2 — Pre-rename historical preview

Otium Alpha 2 was the second community prerelease before the Kvieta rename. Its
historical public name deliberately
does not use `v1.0.0-alpha.2`; `alpha-2` is used only where a URL, Git tag, or
filename-safe identifier is required. The internal MSI product version remains
`1.0.0` so Windows Installer can service existing Alpha installations.

### Trusted phone and PIN recovery

- Added optional trusted-phone enrollment during setup and from the Control Center.
- Added a mobile companion page with QR enrollment, short-lived challenges,
  comparison codes, signed approvals, replay prevention, expiry, and one active
  trusted device at a time.
- Added trusted-phone authorization for administrator PIN reset while keeping the
  new PIN and recovery codes on the Windows device.
- Added QR-based transfer to replace the trusted phone and explicit device revocation.
- Kept recovery codes as an offline fallback and clarified every PIN prompt and
  “forgot PIN” path.
- Normalized browser user-agent names into a friendly device label such as
  **Android phone** instead of exposing raw platform strings.

### Setup, update, repair, and removal

- Reworked first-run PIN handling, confirmation, visibility controls, optional
  phone enrollment, and protected-mode handoff.
- Added safer handling for existing Alpha installations, including update/repair,
  settings reconfiguration, stale registration recovery, and in-app removal.
- Hardened elevated MSI staging, Guardian policy transfer, rollback, lock cleanup,
  and error reporting so failed setup no longer leaves ambiguous state.
- Added package checks for Setup metadata, embedded MSI identity, Guardian service
  registration, public-build configuration, manifest hashes, and release labels.

### Guardian and administrator lifecycle

- Rebuilt administrator exit as a verified transition instead of closing the
  session surface first and letting Guardian immediately reopen it.
- Prevented duplicate Control Centers, duplicate session surfaces, and competing
  management transitions.
- Added Guardian start/repair recovery from the Control Center and clearer health
  reporting when installation repair is required.
- Fixed protected-policy lock ownership, credential synchronization, recovery-code
  consumption, and service handoff failures observed during physical-machine tests.
- Prevented automatic plan/session enforcement from taking over while a management
  window or setup flow is active.

### Session and control experience

- Restored application-rule management and stabilized the protected session after
  install, administrator exit, cancellation, Windows lock, and restart scenarios.
- Made trusted-phone enrollment optional without reporting a cancelled enrollment
  as successful.
- Improved PIN dialogs, recovery screens, compact-window layouts, error messages,
  progress states, icons, and the Kvieta light/dark visual language.
- Fixed session-surface power actions and confirmation-dialog focus handling.

### Local-first boundary

- The Alpha 2 companion flow is served by the Windows device and stores no Kvieta
  usage data in a cloud account.
- Enrollment and approval payloads are signed, short-lived, origin-checked, and
  rate-limited; PINs and recovery codes are never sent to the phone.
- Internet relay support remains a post-v1 roadmap item after the planned product
  rename. Alpha 2 phone enrollment therefore requires reachability to the Windows
  device on the local network.

### Release gate

- Hands-on setup, Guardian, trusted-phone, administrator-exit, session, repair,
  uninstall, and power-action regressions have been exercised on Windows devices.
- The final Alpha 2 community package was rebuilt from clean release commit
  `ca2181c` and passed the complete build, format, smoke, documentation,
  public-bypass, package metadata, manifest, and SHA-256 gates.
- Alpha 2 remains an intentionally unsigned prerelease; Windows SmartScreen may
  display an unknown-publisher warning.

## v1.0.0-alpha.1 — First community prerelease

`v1.0.0-alpha.1` is the first public test package for real-device feedback. It is an unsigned Windows community prerelease, not the final V1 release. It consolidates installer, recovery, diagnostics, usage-mode, personal-protection, Guardian, and security work completed after v0.16.1.

The final `v1.0.0` tag will only be created after the remaining Windows escape-path, public community package integrity, installer lifecycle, and real-device test requirements have been completed.

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

### Security hardening completed for Alpha.1

- Removed the offline administrator-PIN verifier from user-readable protected policy copies. Public policy files now contain a non-verifying marker while the real verifier remains restricted to Guardian and administrator storage.
- Routed protected-mode PIN checks through authenticated Guardian IPC with persistent throttling, replay protection, and migration of older policy files.
- Serialized PIN-dialog verification so parallel clicks or Enter presses cannot submit multiple attempts concurrently.
- Removed the installer elevation time-of-check/time-of-use gap. The elevated setup process now extracts and verifies its embedded MSI inside an ACL-restricted ProgramData staging directory before invoking Windows Installer.
- Added process-start observation and verified parent ancestry for launcher and child-process application rules.
- Preserved protected credential redaction when settings or pending policy targets are copied back to the user profile.

### Alpha.1 behavior and reliability fixes

- Removed the **Sign out** limit action after a real-use test exposed a repeated Windows sign-out loop. Existing settings using it migrate to the safer **Windows lock** action.
- Changed usage-mode selection so Protected mode and Guardian policy changes are staged in the interface and applied only after **Save** succeeds.
- Prevented duplicate PIN submissions while Guardian verification is in progress.
- Replaced frequent system-theme registry polling with Windows preference-change notifications.
- Consolidated Guardian IPC request handling and reduced repeated application-rule matching work.
- Improved Recovery Center sizing and scrolling on constrained displays.
- Confirmed the basic protected surface and secondary-display shields on a physical two-monitor setup.

### Community package

- Added an unsigned `community` package kind that uses the Release configuration and excludes every development/test bypass.
- Kept community artifacts technically separate from Debug test installers.
- Produces a self-contained Setup EXE, standalone MSI, SHA-256 files, and a schema-v2 release manifest tied to the exact source commit.
- Verifies setup/MSI metadata, embedded package integrity, release configuration, artifact sizes, hashes, and the absence of public-build test unlock markers.
- Windows SmartScreen may show an unknown-publisher warning because Alpha.1 is intentionally unsigned.

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
- Expanded the Recovery Center into a System Health view with separate application, installer, Guardian, and local-data status.
- Added one-click privacy-safe diagnostic export from the health surface.

### Windows lifecycle

- Added a serialized lock, unlock, suspend, and resume state policy.
- Atomically pauses active timing before suspend and resumes only when the system was not locked.
- Preserves the user-controlled Break state after Win+L and rebuilds the protected display topology after resume.
- Records bounded, content-free lifecycle audit events for diagnostics.

### Open-source project foundation

- Added the MIT License, a support policy, and matching English/Turkish usage guides.
- Corrected stale RC, two-mode, and planned Application Identity claims in both READMEs.
- Documented the deliberate unsigned community-release direction without treating Development bypasses as release security.
- Added a CI documentation gate for bilingual status, modes, install/update, uninstall, support, and license coverage.

### Removed or deferred

- Removed unstable one-click application suggestions from this release.
- Moved reliable live suggestion refresh to the post-v1 roadmap.
- Kept AppLocker and WDAC integration as a future optional protection level because availability depends on Windows edition and policy.

### Known Alpha.1 limitations and final V1 blockers

- Balanced session-surface recovery has been hardened and passed the initial single-monitor manual test; Explorer restart, virtual desktops, and the wider repeatable Windows matrix remain open.
- The unsigned community packaging path is ready, but the published Alpha.1 artifact must be built from and matched to its release commit.
- Clean install, Protected Guardian enrollment, expiry behavior, repair, and uninstall are the first post-publication Alpha.1 field tests on a separate Windows device. Upgrade and rollback remain part of the broader final V1 matrix.
- Reboot, Win+L, sleep, hibernation, Explorer restart, user switching, Remote Desktop, multiple monitors, and standard-user scenarios remain in the final Windows matrix.

### Validation completed

- Debug and Release builds complete with zero warnings and zero errors.
- Core smoke tests pass in both configurations.
- Settings migration, delayed relaxation, Guardian credentials, protected-policy synchronization, and manual Flexible sessions have automated regression coverage.
- A real-process startup diagnostic confirmed that the Tracking-only startup crash no longer occurs.
- Protected PIN redaction, legacy Sign-out migration, and save-gated Protected mode transitions have automated regression coverage.
- The unsigned Release community package passed metadata, embedded MSI, manifest, SHA-256, and public-build bypass verification.

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
