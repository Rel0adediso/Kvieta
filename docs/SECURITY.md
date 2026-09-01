# Kvieta security boundaries

## Protected scenario

Protected mode is designed to resist common bypasses on a standard Windows account managed by a separate administrator account. Guardian keeps the authoritative protected policy, restores the session surface, and communicates with the app over authenticated IPC.

Windows service recovery and Guardian supervision handle Task Manager-like termination of the service or protected session. If Guardian is unhealthy at Protected startup, Kvieta does not continue unprotected: it requests repair or exits safely.

## Not guaranteed

- There is no absolute protection against someone with Windows administrator rights, physical disk access, or offline operating-system access.
- Safe Mode, alternate boot media, firmware/boot changes, and kernel-level tooling are out of scope.
- Kvieta is not a replacement for enterprise EDR, AppLocker, or WDAC.
- Unsigned Development output is not a release package. The current Public Guardian path expects an installed client under Program Files with an Authenticode signer pinned by the installer. A separate unsigned community identity model is open V1 work and will not reuse the Development bypass.

## Stored data

Schedules, rules, and usage history stay on the device. Diagnostic exports exclude PINs, recovery codes, document content, and window titles. The security audit records limited fields such as event kind, result, and time.

## Recovery model

PIN reset requires a one-time recovery code and Windows administrator approval. The last-known-good settings snapshot can be restored, and installer repair verifies application and Guardian files. Recovery tools do not silently remove daily limits or application rules.

Do not place sensitive data in a public issue when reporting a vulnerability. Until the project owner publishes a private reporting channel, share only the minimum information required to reproduce the issue.
