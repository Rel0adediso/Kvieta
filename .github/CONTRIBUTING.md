# Contributing to Kvieta

Kvieta targets Windows with .NET 10 and WPF. Keep security-sensitive behavior fail-closed and preserve Turkish/English resource parity.

Before submitting a change, run:

```powershell
dotnet format Kvieta.slnx --verify-no-changes
dotnet build Kvieta.slnx -c Debug
dotnet build Kvieta.slnx -c Release
dotnet run --project tests/Kvieta.Core.SmokeTests/Kvieta.Core.SmokeTests.csproj -c Debug --no-build
dotnet run --project tests/Kvieta.Core.SmokeTests/Kvieta.Core.SmokeTests.csproj -c Release --no-build
./scripts/verify-documentation.ps1
```

Changes to Guardian, protected policy, PIN/recovery, installer, or update verification should include a regression test. Real Windows lifecycle checks can use `scripts/test-guardian-recovery.ps1`; its service-crash option requires elevation and intentionally disrupts the Guardian service briefly.

Do not commit build output, local settings, usage data, recovery codes, signing certificates, or diagnostic reports. Unsigned local test packages must use `scripts/build-test-installer.ps1` and must never be presented as public releases.

Contributions are accepted under the repository's [MIT License](../LICENSE). Keep Turkish and English user-facing documentation equivalent in meaning, and update both sides in the same change.
