using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App.Services;

public enum ProtectionServiceState
{
    NotInstalled,
    Stopped,
    Running
}

public enum ProtectionVersionCompatibility
{
    NotInstalled,
    Compatible,
    Mismatch,
    Unknown
}

public enum ProtectionHealthIssue
{
    ServiceNotInstalled,
    ServiceStopped,
    ExecutableMissing,
    EnrollmentMissing,
    ProtectedPolicyMissing,
    VersionMismatch,
    VersionUnknown,
    StartupNotAutomatic,
    GuardianSessionMissing
}

public sealed record ProtectionHealthReport(
    ProtectionServiceState ServiceState,
    IReadOnlyList<ProtectionHealthIssue> Issues)
{
    public bool IsHealthy => ServiceState == ProtectionServiceState.Running && Issues.Count == 0;
}

public sealed record ProtectionInstallationIdentity(
    bool InstallerManaged,
    string? ReleaseLabel,
    Version? RegisteredVersion,
    Version? InstalledBinaryVersion,
    ProtectionServiceState GuardianState,
    ProtectionVersionCompatibility Compatibility);

public static class ProtectionServiceManager
{
    public const string ServiceName = "KvietaGuardian";
    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Kvieta");
    public static string InstalledExecutablePath => Path.Combine(InstallDirectory, "Kvieta.exe");
    public static string ProtectionDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Kvieta");
    public static string EnrollmentPath => Path.Combine(ProtectionDataDirectory, "guardian-enrollment.json");
    public static string ProcessStatePath => Path.Combine(ProtectionDataDirectory, "guardian-process.json");
    public static string ProtectedSettingsPath => Path.Combine(ProtectionDataDirectory, "protected-settings.json");
    public static string AdministrativeActivityPath => Path.Combine(
        ProtectionDataDirectory,
        "administrative-activity.json");
    public static string PostInstallControlCenterPath => Path.Combine(
        ProtectionDataDirectory,
        "post-install-control-center.pending");
    public static string InstallLogPath => Path.Combine(ProtectionDataDirectory, "guardian-install.log");

    public static bool IsInstallerManaged
    {
        get
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey(@"Software\Kvieta")?.GetValue("InstallerManaged") is int value && value == 1;
            }
            catch
            {
                return false;
            }
        }
    }

    public static string? RegisteredSignerThumbprint =>
        ReadInstallerValue("SignerThumbprint") as string;

    public static string? RegisteredPackageKind =>
        ReadInstallerValue("PackageKind") as string;

    public static string? RegisteredExecutableSha256 =>
        ReadInstallerValue("ExecutableSha256") as string;

    public static ProtectionInstallationIdentity GetInstallationIdentity() => new(
        IsInstallerManaged,
        ReadInstallerValue("InstalledReleaseLabel") as string,
        ReadRegisteredVersion(),
        ReadProductVersion(InstalledExecutablePath),
        GetState(),
        GetVersionCompatibility());

    public static bool IsCommunityClientIdentityValid(
        string? packageKind,
        string? expectedHash,
        string? actualHash,
        Version? registeredVersion,
        Version? clientVersion) =>
        string.Equals(packageKind, "community", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(expectedHash) &&
        !string.IsNullOrWhiteSpace(actualHash) &&
        string.Equals(NormalizeHash(expectedHash), NormalizeHash(actualHash), StringComparison.OrdinalIgnoreCase) &&
        registeredVersion is not null &&
        clientVersion == registeredVersion;

    public static Version? ReadRegisteredVersionForIdentity() => ReadRegisteredVersion();

    public static Version? ReadProductVersionForIdentity(string filePath) => ReadProductVersion(filePath);

    public static string ComputeSha256(string filePath) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));

    private static string NormalizeHash(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

    public static ProtectionVersionCompatibility GetVersionCompatibility()
    {
        if (!File.Exists(InstalledExecutablePath))
        {
            return ProtectionVersionCompatibility.NotInstalled;
        }

        Version? installedBinaryVersion = ReadProductVersion(InstalledExecutablePath);
        Version? currentProcessVersion = ReadProductVersion(Environment.ProcessPath);
        Version? registeredVersion = ReadRegisteredVersion();
        return EvaluateVersionCompatibility(currentProcessVersion, installedBinaryVersion, registeredVersion);
    }

    public static ProtectionVersionCompatibility EvaluateVersionCompatibility(
        Version? currentProcessVersion,
        Version? installedBinaryVersion,
        Version? registeredVersion)
    {
        if (installedBinaryVersion is null || currentProcessVersion is null)
        {
            return ProtectionVersionCompatibility.Unknown;
        }

        return installedBinaryVersion != currentProcessVersion ||
            registeredVersion is not null && installedBinaryVersion != registeredVersion
                ? ProtectionVersionCompatibility.Mismatch
                : ProtectionVersionCompatibility.Compatible;
    }

    public static bool RequiresPinForUninstall(ControlSettings settings) =>
        settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured;

    public static bool RequiresProductRepair(ProtectionHealthReport health) =>
        RequiresProductRepair(health, IsInstallerManaged);

    public static bool RequiresProductRepair(
        ProtectionHealthReport health,
        bool installerManaged) =>
        installerManaged &&
        (health.ServiceState == ProtectionServiceState.NotInstalled ||
         health.Issues.Contains(ProtectionHealthIssue.ExecutableMissing) ||
         health.Issues.Contains(ProtectionHealthIssue.VersionMismatch) ||
         health.Issues.Contains(ProtectionHealthIssue.VersionUnknown) ||
         health.Issues.Contains(ProtectionHealthIssue.StartupNotAutomatic));

    public static bool LaunchProductUninstall()
    {
        if (!IsInstallerManaged || ReadInstallerValue("ProductCode") is not string productCode ||
            !Guid.TryParse(productCode.Trim('{', '}'), out _))
        {
            return false;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
            Arguments = $"/x {productCode} /passive /norestart",
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            return Process.Start(startInfo) is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static async Task<bool> RunProductRepairAsync(bool requestElevation = true)
    {
        if (!IsInstallerManaged || ReadInstallerValue("ProductCode") is not string productCode ||
            !Guid.TryParse(productCode.Trim('{', '}'), out _))
        {
            return false;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
            Arguments = $"/fa {productCode} /passive /norestart",
            UseShellExecute = requestElevation,
            Verb = requestElevation ? "runas" : string.Empty,
            CreateNoWindow = !requestElevation,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null) return false;
            await process.WaitForExitAsync();
            if (process.ExitCode is not (0 or 3010))
            {
                return false;
            }

            return LoadEnrollment() is null || EnsureServiceRunning();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static ProtectionServiceState GetState()
    {
        try
        {
            using ServiceController controller = new(ServiceName);
            _ = controller.Status;
            return controller.Status == ServiceControllerStatus.Running &&
                File.Exists(EnrollmentPath) &&
                File.Exists(ProtectedSettingsPath)
                ? ProtectionServiceState.Running
                : ProtectionServiceState.Stopped;
        }
        catch (InvalidOperationException)
        {
            return ProtectionServiceState.NotInstalled;
        }
    }

    public static ProtectionHealthReport GetHealthReport()
    {
        List<ProtectionHealthIssue> issues = [];
        ProtectionServiceState state = GetState();
        if (state == ProtectionServiceState.NotInstalled)
        {
            issues.Add(ProtectionHealthIssue.ServiceNotInstalled);
            return new ProtectionHealthReport(state, issues);
        }

        if (state != ProtectionServiceState.Running)
        {
            issues.Add(ProtectionHealthIssue.ServiceStopped);
        }
        if (!File.Exists(InstalledExecutablePath))
        {
            issues.Add(ProtectionHealthIssue.ExecutableMissing);
        }
        if (!File.Exists(EnrollmentPath))
        {
            issues.Add(ProtectionHealthIssue.EnrollmentMissing);
        }
        if (!File.Exists(ProtectedSettingsPath))
        {
            issues.Add(ProtectionHealthIssue.ProtectedPolicyMissing);
        }

        ProtectionVersionCompatibility compatibility = GetVersionCompatibility();
        if (compatibility == ProtectionVersionCompatibility.Mismatch)
        {
            issues.Add(ProtectionHealthIssue.VersionMismatch);
        }
        else if (compatibility == ProtectionVersionCompatibility.Unknown)
        {
            issues.Add(ProtectionHealthIssue.VersionUnknown);
        }

        try
        {
            using ServiceController controller = new(ServiceName);
            if (controller.StartType != ServiceStartMode.Automatic)
            {
                issues.Add(ProtectionHealthIssue.StartupNotAutomatic);
            }
        }
        catch
        {
            if (!issues.Contains(ProtectionHealthIssue.ServiceNotInstalled))
            {
                issues.Add(ProtectionHealthIssue.ServiceStopped);
            }
        }

        if (state == ProtectionServiceState.Running && !HasLiveGuardianSession())
        {
            issues.Add(ProtectionHealthIssue.GuardianSessionMissing);
        }

        return new ProtectionHealthReport(state, issues.Distinct().ToList());
    }

    private static bool HasLiveGuardianSession()
    {
        try
        {
            if (!File.Exists(ProcessStatePath))
            {
                return false;
            }

            GuardianProcessState? state = JsonSerializer.Deserialize<GuardianProcessState>(
                File.ReadAllText(ProcessStatePath));
            if (state is null)
            {
                return false;
            }

            using Process process = Process.GetProcessById(state.ProcessId);
            return !process.HasExited &&
                process.SessionId == state.SessionId &&
                process.StartTime.ToUniversalTime().Ticks == state.StartTimeUtcTicks;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> RunElevatedInstallerAsync(bool install)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Kvieta executable path is unavailable.");
        string payload = string.Empty;
        if (install)
        {
            string userSid = WindowsIdentity.GetCurrent().User?.Value
                ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");
            string settingsPath = new JsonSettingsStore().FilePath;
            payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new GuardianInstallRequest(userSid, settingsPath))));
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            Arguments = install ? $"--install-guardian {payload}" : "--remove-guardian",
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static int ExecuteInstallerCommand(bool install, string? payload = null)
    {
        if (!IsAdministrator())
        {
            return 5;
        }

        return install ? Install(payload) : Remove();
    }

    public static async Task<bool> RunElevatedAuthorizedStopAsync()
    {
        if (GetState() != ProtectionServiceState.Running)
        {
            return true;
        }

        if (IsAdministrator())
        {
            return ExecuteAuthorizedStopCommand() == 0;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            Arguments = "--stop-guardian",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static int ExecuteProvisioningCommand(string? payload)
    {
        if (!IsAdministrator())
        {
            return 5;
        }

        if (!IsInstallerManaged)
        {
            return 4;
        }

        return ProvisionInstallerManagedGuardian(payload);
    }

    public static int ExecuteAuthorizedStopCommand()
    {
        if (!IsAdministrator())
        {
            return 5;
        }

        try
        {
            StopServiceIfPresent();
            StopTrackedGuardianSessionIfPresent();
            TryDelete(ProcessStatePath);
            return GetState() == ProtectionServiceState.Running ? 1 : 0;
        }
        catch
        {
            return 1;
        }
    }

    public static GuardianEnrollment? LoadEnrollment()
    {
        try
        {
            return File.Exists(EnrollmentPath)
                ? JsonSerializer.Deserialize<GuardianEnrollment>(File.ReadAllText(EnrollmentPath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void MigrateCredentialStorage()
    {
        GuardianEnrollment? enrollment = LoadEnrollment();
        if (enrollment is null || string.IsNullOrWhiteSpace(enrollment.UserSid)) return;

        HardenProtectionDataAcl(enrollment.UserSid);
        if (File.Exists(ProtectedSettingsPath))
        {
            ControlSettings settings = Task.Run(
                    () => new JsonSettingsStore(ProtectedSettingsPath, readOnly: true).LoadAsync())
                .GetAwaiter()
                .GetResult();
            File.WriteAllBytes(ProtectedSettingsPath, ProtectionPolicyChannel.CreateProtectedPolicyBytes(settings));
            HardenProtectedPolicyAcl(enrollment.UserSid);
            if (!string.IsNullOrWhiteSpace(enrollment.SettingsPath))
            {
                File.Copy(ProtectedSettingsPath, enrollment.SettingsPath, overwrite: true);
            }
        }
        HardenSensitiveFileAcl(EnrollmentPath);
        string throttlePath = Path.Combine(ProtectionDataDirectory, "guardian-auth-throttle.json");
        if (File.Exists(throttlePath)) HardenSensitiveFileAcl(throttlePath);
    }

    public static void DeactivateGuardianPolicy()
    {
        StopTrackedGuardianSessionIfPresent();
        TryDelete(ProcessStatePath);
        TryDelete(PostInstallControlCenterPath);
        TryDelete(AdministrativeActivityPath);
        TryDelete(ProtectedSettingsPath);
        TryDelete(EnrollmentPath);
    }

    private static int Install(string? payload)
    {
        if (IsInstallerManaged)
        {
            int provisionResult = ProvisionInstallerManagedGuardian(payload);
            return provisionResult == 0 && EnsureServiceRunning()
                ? 0
                : provisionResult == 0 ? 1 : provisionResult;
        }

        try
        {
            TraceInstallStep("start");
            if (string.IsNullOrWhiteSpace(payload))
            {
                TraceInstallStep("failure.payload-missing");
                return 2;
            }

            GuardianInstallRequest request = JsonSerializer.Deserialize<GuardianInstallRequest>(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload)))
                ?? throw new InvalidOperationException("Guardian enrollment request is invalid.");
            TraceInstallStep("payload.valid");
            ControlSettings settings = Task.Run(
                    () => new JsonSettingsStore(request.SettingsPath).LoadAsync())
                .GetAwaiter()
                .GetResult();
            TraceInstallStep("settings.loaded");
            if (!settings.RequiresGuardian || !settings.AdminPin.IsConfigured)
            {
                TraceInstallStep("failure.policy-not-protected");
                return 3;
            }

            bool alreadyInstalled = GetState() != ProtectionServiceState.NotInstalled;
            TraceInstallStep(alreadyInstalled ? "service.existing" : "service.new");
            StopServiceIfPresent();
            StopTrackedGuardianSessionIfPresent();
            TraceInstallStep("session.stopped");
            Directory.CreateDirectory(InstallDirectory);
            Directory.CreateDirectory(ProtectionDataDirectory);
            TraceInstallStep("directories.ready");
            HardenProtectionDataAcl(request.UserSid);
            TraceInstallStep("acl.ready");
            File.WriteAllText(EnrollmentPath, JsonSerializer.Serialize(
                new GuardianEnrollment(request.UserSid, request.SettingsPath, settings.AdminPin)));
            HardenSensitiveFileAcl(EnrollmentPath);
            File.WriteAllBytes(ProtectedSettingsPath, ProtectionPolicyChannel.CreateProtectedPolicyBytes(settings));
            HardenProtectedPolicyAcl(request.UserSid);
            File.Copy(ProtectedSettingsPath, request.SettingsPath, overwrite: true);
            File.WriteAllText(PostInstallControlCenterPath, "waiting");
            HardenReadOnlyFileAcl(PostInstallControlCenterPath, request.UserSid);
            TraceInstallStep("policy.ready");

            string source = Environment.ProcessPath
                ?? throw new InvalidOperationException("Kvieta executable path is unavailable.");
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(InstalledExecutablePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(source, InstalledExecutablePath, overwrite: true);
            }
            TraceInstallStep("binary.ready");

            RunSc(
                alreadyInstalled ? "config" : "create",
                ServiceName,
                "binPath=", $"\"{InstalledExecutablePath}\" --guardian-service",
                "start=", "auto",
                "DisplayName=", "Kvieta Protection");
            RunSc("description", ServiceName, "Kvieta protected-session watchdog");
            RunSc("failure", ServiceName, "reset=", "60", "actions=", "restart/3000/restart/3000/restart/3000");
            TraceInstallStep("service.configured");
            RunSc("start", ServiceName);
            TraceInstallStep("complete");
            return 0;
        }
        catch (Exception exception)
        {
            TraceInstallStep($"failure.{exception.GetType().Name}.{SanitizeInstallLogValue(exception.Message)}");
            return 1;
        }
    }

    private static int ProvisionInstallerManagedGuardian(string? payload)
    {
        GuardianProvisioningSnapshot? snapshot = null;
        try
        {
            TraceInstallStep("provision.start");
            if (string.IsNullOrWhiteSpace(payload))
            {
                TraceInstallStep("provision.failure.payload-missing");
                return 2;
            }

            GuardianInstallRequest request = JsonSerializer.Deserialize<GuardianInstallRequest>(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload)))
                ?? throw new InvalidOperationException("Guardian enrollment request is invalid.");
            ControlSettings settings = Task.Run(
                    () => new JsonSettingsStore(request.SettingsPath).LoadAsync())
                .GetAwaiter()
                .GetResult();
            if (!settings.RequiresGuardian || !settings.AdminPin.IsConfigured)
            {
                TraceInstallStep("provision.failure.policy-not-protected");
                return 3;
            }

            Directory.CreateDirectory(ProtectionDataDirectory);
            snapshot = GuardianProvisioningSnapshot.Capture(request.SettingsPath);
            GuardianEnrollment? existingEnrollment = LoadEnrollment();
            if (settings.AdminPin.IsPublicMarker &&
                (existingEnrollment is null ||
                 !string.Equals(existingEnrollment.UserSid, request.UserSid, StringComparison.OrdinalIgnoreCase) ||
                 existingEnrollment.AdminPin is null ||
                 !existingEnrollment.AdminPin.IsConfigured ||
                 existingEnrollment.AdminPin.IsPublicMarker))
            {
                TraceInstallStep("provision.failure.credential-unavailable");
                return 6;
            }
            GuardianEnrollment enrollment = ResolveEnrollmentForProvisioning(
                request,
                settings.AdminPin,
                existingEnrollment);
            HardenProtectionDataAcl(request.UserSid);
            WriteBytesAtomically(
                EnrollmentPath,
                JsonSerializer.SerializeToUtf8Bytes(enrollment));
            HardenSensitiveFileAcl(EnrollmentPath);
            settings.AdminPin = enrollment.AdminPin;
            WriteBytesAtomically(
                ProtectedSettingsPath,
                ProtectionPolicyChannel.CreateProtectedPolicyBytes(settings));
            HardenProtectedPolicyAcl(request.UserSid);
            WriteBytesAtomically(request.SettingsPath, File.ReadAllBytes(ProtectedSettingsPath));
            WriteBytesAtomically(PostInstallControlCenterPath, Encoding.UTF8.GetBytes("waiting"));
            HardenReadOnlyFileAcl(PostInstallControlCenterPath, request.UserSid);
            TryDelete(AdministrativeActivityPath);
            ProtectionPolicyChannel.ResetAuthenticationThrottleAfterProvisioning();
            TraceInstallStep("provision.complete");
            return 0;
        }
        catch (Exception exception)
        {
            snapshot?.Restore();
            TraceInstallStep($"provision.failure.{exception.GetType().Name}.{SanitizeInstallLogValue(exception.Message)}");
            return 1;
        }
    }

    public static GuardianEnrollment ResolveEnrollmentForProvisioning(
        GuardianInstallRequest request,
        AdminCredential requestedCredential,
        GuardianEnrollment? existingEnrollment)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestedCredential);
        if (!requestedCredential.IsPublicMarker)
        {
            return new GuardianEnrollment(request.UserSid, request.SettingsPath, requestedCredential);
        }

        if (existingEnrollment is null ||
            !string.Equals(existingEnrollment.UserSid, request.UserSid, StringComparison.OrdinalIgnoreCase) ||
            !existingEnrollment.AdminPin.IsConfigured ||
            existingEnrollment.AdminPin.IsPublicMarker)
        {
            throw new InvalidOperationException(
                "The existing Guardian credential is unavailable for protected repair.");
        }

        return existingEnrollment with
        {
            SettingsPath = request.SettingsPath
        };
    }

    private static void WriteBytesAtomically(string path, byte[] content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static bool EnsureServiceRunning()
    {
        try
        {
            using ServiceController controller = new(ServiceName);
            controller.Refresh();
            if (controller.Status == ServiceControllerStatus.StopPending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                controller.Refresh();
            }
            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                controller.Start();
            }
            if (controller.Status != ServiceControllerStatus.Running)
            {
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            }
            controller.Refresh();
            return controller.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private sealed record GuardianProvisioningSnapshot(
        string SettingsPath,
        byte[]? Enrollment,
        byte[]? ProtectedSettings,
        byte[]? UserSettings,
        byte[]? PostInstallMarker)
    {
        public static GuardianProvisioningSnapshot Capture(string settingsPath) => new(
            settingsPath,
            ReadIfPresent(EnrollmentPath),
            ReadIfPresent(ProtectedSettingsPath),
            ReadIfPresent(settingsPath),
            ReadIfPresent(PostInstallControlCenterPath));

        public void Restore()
        {
            RestoreFile(EnrollmentPath, Enrollment);
            RestoreFile(ProtectedSettingsPath, ProtectedSettings);
            RestoreFile(SettingsPath, UserSettings);
            RestoreFile(PostInstallControlCenterPath, PostInstallMarker);
        }

        private static byte[]? ReadIfPresent(string path) =>
            File.Exists(path) ? File.ReadAllBytes(path) : null;

        private static void RestoreFile(string path, byte[]? content)
        {
            if (content is null)
            {
                TryDelete(path);
                return;
            }
            WriteBytesAtomically(path, content);
        }
    }

    private static string SanitizeInstallLogValue(string value) =>
        new(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '.' or '-' or '_' or '(' or ')').ToArray());

    private static void TraceInstallStep(string step)
    {
        try
        {
            Directory.CreateDirectory(ProtectionDataDirectory);
            File.AppendAllText(
                InstallLogPath,
                $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} {step}{Environment.NewLine}");
        }
        catch
        {
            // Installation must not depend on diagnostic logging.
        }
    }

    private static int Remove()
    {
        try
        {
            if (IsInstallerManaged)
            {
                // Windows Installer owns the service registration. Removing protection
                // only clears enrollment; MSI repair and uninstall must remain reliable.
                TryDelete(EnrollmentPath);
                TryDelete(ProcessStatePath);
                TryDelete(PostInstallControlCenterPath);
                TryDelete(ProtectedSettingsPath);
                return 0;
            }

            StopServiceIfPresent();
            RunSc("delete", ServiceName);
            for (int attempt = 0; attempt < 20 && GetState() != ProtectionServiceState.NotInstalled; attempt++)
            {
                Thread.Sleep(250);
            }
            if (GetState() != ProtectionServiceState.NotInstalled)
            {
                return 2;
            }

            TryDelete(EnrollmentPath);
            TryDelete(ProcessStatePath);
            TryDelete(PostInstallControlCenterPath);
            TryDelete(ProtectedSettingsPath);
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static void StopServiceIfPresent()
    {
        ServiceController? controller = null;
        try
        {
            controller = new ServiceController(ServiceName);
            _ = controller.Status;
        }
        catch (InvalidOperationException)
        {
            controller?.Dispose();
            return;
        }

        using (controller)
        {
            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                RunSc("stop", ServiceName);
                try
                {
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    throw new InvalidOperationException("Kvieta protection service did not stop in time.");
                }
            }
        }
    }

    private static void StopTrackedGuardianSessionIfPresent()
    {
        try
        {
            if (!File.Exists(ProcessStatePath))
            {
                return;
            }

            GuardianProcessState? state = JsonSerializer.Deserialize<GuardianProcessState>(
                File.ReadAllText(ProcessStatePath));
            if (state is null)
            {
                return;
            }

            using Process process = Process.GetProcessById(state.ProcessId);
            string? executablePath = process.MainModule?.FileName;
            if (process.HasExited ||
                process.SessionId != state.SessionId ||
                process.StartTime.ToUniversalTime().Ticks != state.StartTimeUtcTicks ||
                !string.Equals(
                    Path.GetFullPath(executablePath ?? string.Empty),
                    Path.GetFullPath(InstalledExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000))
            {
                throw new InvalidOperationException("Guardian session did not stop in time.");
            }
        }
        catch (ArgumentException)
        {
            // The tracked process already exited.
        }
        finally
        {
            TryDelete(ProcessStatePath);
        }
    }

    private static void RunSc(string verb, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(verb);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows service manager could not be started.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Service operation failed ({verb}, {process.ExitCode}).");
        }
    }

    private static void HardenProtectionDataAcl(string userSid)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(ProtectionDataDirectory);
        startInfo.ArgumentList.Add("/inheritance:r");
        startInfo.ArgumentList.Add("/remove:g");
        startInfo.ArgumentList.Add("*S-1-5-32-545");
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-18:(OI)(CI)F");
        startInfo.ArgumentList.Add("*S-1-5-32-544:(OI)(CI)F");
        startInfo.ArgumentList.Add($"*{userSid}:(RX)");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Protection folder permissions could not be configured.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Protection folder permissions could not be configured.");
        }
    }

    public static void HardenSensitiveFileAcl(string path) => RunIcacls(
        path,
        "/inheritance:r",
        "/grant:r",
        "*S-1-5-18:F",
        "*S-1-5-32-544:F");

    public static void HardenReadOnlyFileAcl(string path, string userSid) => RunIcacls(
        path,
        "/inheritance:r",
        "/grant:r",
        "*S-1-5-18:F",
        "*S-1-5-32-544:F",
        $"*{userSid}:R");

    public static void HardenProtectedPolicyAcl(string userSid) => RunIcacls(
        ProtectedSettingsPath,
        "/inheritance:r",
        "/grant:r",
        "*S-1-5-18:F",
        "*S-1-5-32-544:F",
        $"*{userSid}:R");

    private static void RunIcacls(string path, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(path);
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Sensitive file permissions could not be configured.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Sensitive file permissions could not be configured.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Service removal has already succeeded; leftover state is inert.
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static object? ReadInstallerValue(string name)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"Software\Kvieta");
            return key?.GetValue(name);
        }
        catch
        {
            return null;
        }
    }

    private static Version? ReadRegisteredVersion() =>
        ReadInstallerValue("InstalledVersion") is string value && Version.TryParse(value, out Version? version)
            ? version
            : null;

    private static Version? ReadProductVersion(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            string? productVersion = FileVersionInfo.GetVersionInfo(path).ProductVersion?.Split('+')[0];
            return Version.TryParse(productVersion, out Version? version) ? version : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record GuardianInstallRequest(string UserSid, string SettingsPath);
public sealed record GuardianEnrollment(
    string UserSid,
    string SettingsPath,
    AdminCredential AdminPin,
    AdminCredential? PreviousAdminPin = null);

public sealed record GuardianProcessState(int ProcessId, int SessionId, long StartTimeUtcTicks);
