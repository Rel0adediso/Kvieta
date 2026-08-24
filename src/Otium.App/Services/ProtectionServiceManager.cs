using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public enum ProtectionServiceState
{
    NotInstalled,
    Stopped,
    Running
}

public static class ProtectionServiceManager
{
    public const string ServiceName = "OtiumGuardian";
    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Otium");
    public static string InstalledExecutablePath => Path.Combine(InstallDirectory, "Otium.exe");
    public static string ProtectionDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Otium");
    public static string EnrollmentPath => Path.Combine(ProtectionDataDirectory, "guardian-enrollment.json");
    public static string ProcessStatePath => Path.Combine(ProtectionDataDirectory, "guardian-process.json");
    public static string ProtectedSettingsPath => Path.Combine(ProtectionDataDirectory, "protected-settings.json");

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

    public static async Task<bool> RunElevatedInstallerAsync(bool install)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Otium executable path is unavailable.");
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

            await process.WaitForExitAsync();
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

    private static int Install(string? payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return 2;
            }

            GuardianInstallRequest request = JsonSerializer.Deserialize<GuardianInstallRequest>(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload)))
                ?? throw new InvalidOperationException("Guardian enrollment request is invalid.");
            ControlSettings settings = new JsonSettingsStore(request.SettingsPath).LoadAsync().GetAwaiter().GetResult();
            if (settings.Mode != ControlMode.Protected || !settings.AdminPin.IsConfigured)
            {
                return 3;
            }

            bool alreadyInstalled = GetState() != ProtectionServiceState.NotInstalled;
            StopServiceIfPresent();
            Directory.CreateDirectory(InstallDirectory);
            Directory.CreateDirectory(ProtectionDataDirectory);
            HardenProtectionDataAcl(request.UserSid);
            File.WriteAllText(EnrollmentPath, JsonSerializer.Serialize(
                new GuardianEnrollment(request.UserSid, request.SettingsPath, settings.AdminPin)));
            File.Copy(request.SettingsPath, ProtectedSettingsPath, overwrite: true);

            string source = Environment.ProcessPath
                ?? throw new InvalidOperationException("Otium executable path is unavailable.");
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(InstalledExecutablePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(source, InstalledExecutablePath, overwrite: true);
            }

            RunSc(
                alreadyInstalled ? "config" : "create",
                ServiceName,
                "binPath=", $"\"{InstalledExecutablePath}\" --guardian-service",
                "start=", "auto",
                "DisplayName=", "Otium Protection");
            RunSc("description", ServiceName, "Otium protected-session watchdog");
            RunSc("failure", ServiceName, "reset=", "60", "actions=", "restart/3000/restart/3000/restart/3000");
            RunSc("start", ServiceName);
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static int Remove()
    {
        try
        {
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
                    throw new InvalidOperationException("Otium protection service did not stop in time.");
                }
            }
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
        startInfo.ArgumentList.Add($"*{userSid}:(OI)(CI)R");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Protection folder permissions could not be configured.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Protection folder permissions could not be configured.");
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
}

public sealed record GuardianInstallRequest(string UserSid, string SettingsPath);
public sealed record GuardianEnrollment(
    string UserSid,
    string SettingsPath,
    AdminCredential AdminPin,
    AdminCredential? PreviousAdminPin = null);
