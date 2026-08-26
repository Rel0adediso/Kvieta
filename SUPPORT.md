# Otium support

Otium is an early, personal open-source project. Support is best-effort and currently handled through GitHub.

## Bug reports

Use the repository's **Bug report** issue template. Include:

- the exact Otium version, package label, and source commit shown in Settings;
- Windows version and whether the account is standard or administrator;
- selected usage and protection mode;
- clear reproduction steps, expected behavior, and actual behavior;
- monitor count, scaling, and sleep/lock context when relevant;
- a privacy-safe diagnostic report from the System Health and Recovery Center when available.

Never post an administrator PIN, recovery code, private document, window title, signing key, or other secret. Review screenshots and diagnostics before uploading them.

## Feature requests

Use the **Feature request** template and explain the problem before proposing a solution. New features must preserve local-first operation, data minimization, Turkish/English parity, and the documented security boundary.

## Security reports

Do not publish exploit details or secrets in a public issue. Until a private security channel is configured, open a minimal issue that states a security-sensitive problem exists without including reproduction details. The project owner can then arrange a safer follow-up path.

## Alpha expectations

The current `v1.0.0-alpha` packages are for development and validation. Multi-monitor, sleep/hibernate, complete installer lifecycle, and public distribution trust are still open V1 work. See the [roadmap](ROADMAP.md) and [test matrix](docs/V1-TEST-MATRIX.md) for current evidence.
