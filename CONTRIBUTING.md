# Contributing to Otium

Otium targets Windows with .NET 10 and WPF. Keep security-sensitive behavior fail-closed and preserve Turkish/English resource parity.

Before submitting a change, run:

```powershell
dotnet format Otium.slnx --verify-no-changes
dotnet build Otium.slnx -c Debug
dotnet build Otium.slnx -c Release
dotnet run --project tests/Otium.Core.SmokeTests/Otium.Core.SmokeTests.csproj -c Debug --no-build
dotnet run --project tests/Otium.Core.SmokeTests/Otium.Core.SmokeTests.csproj -c Release --no-build
```

Changes to Guardian, protected policy, PIN/recovery, installer, or update verification should include a regression test. Real Windows lifecycle checks can use `scripts/test-guardian-recovery.ps1`; its service-crash option requires elevation and intentionally disrupts the Guardian service briefly.

Do not commit build output, local settings, usage data, recovery codes, signing certificates, or diagnostic reports. Public installer builds require an Authenticode code-signing certificate and must use `scripts/build-installer.ps1`.

The repository does not yet declare an open-source license. Contributions cannot be accepted as an open-source release until the project owner selects and adds one.
