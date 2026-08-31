using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace Otium.SetupApp;

internal static class ElevatedPackageInstaller
{
    private const string PayloadResourceName = "Otium.Payload.msi";
    internal const int GuardianProvisioningFailedExitCode = 5101;
    internal const int GuardianStartFailedExitCode = 5102;
    internal const int OrphanedGuardianCleanupFailedExitCode = 5103;
    internal const int GuardianCredentialUnavailableExitCode = 5104;

    public static int ResetBrokenProtection()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            {
                return 5;
            }
        }

        try
        {
            StopGuardianForUpgrade();
            StopInstalledOtiumProcesses();
            string protectionDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Otium");
            string[] securityFiles =
            [
                "guardian-enrollment.json",
                "guardian-process.json",
                "protected-settings.json",
                "protected-settings.json.bak",
                "protected-settings.json.lock",
                "post-install-control-center.pending",
                "administrative-activity.json",
                "guardian-auth-throttle.json",
                "manager-device.json",
                "manager-recovery-replay.json"
            ];
            foreach (string fileName in securityFiles)
            {
                string path = Path.Combine(protectionDirectory, fileName);
                if (File.Exists(path)) File.Delete(path);
            }

            return securityFiles.Any(fileName => File.Exists(Path.Combine(protectionDirectory, fileName)))
                ? 1
                : 0;
        }
        catch
        {
            return 1;
        }
    }

    public static async Task<int> RunAsync(
        bool desktopShortcut,
        string? guardianPayload,
        bool forceReinstall)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Otium",
            "InstallerTemp");
        string stagingDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        GuardianMachineStateSnapshot guardianSnapshot = GuardianMachineStateSnapshot.Capture();

        try
        {
            HardenDirectory(stagingDirectory);
            string msiPath = Path.Combine(stagingDirectory, "Otium.msi");
            await ExtractAndVerifyPayloadAsync(msiPath);

            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Otium",
                "SetupLogs");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"Otium-setup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            string features = desktopShortcut ? "MainFeature,DesktopShortcutFeature" : "MainFeature";

            // Early alpha builds exposed uninstall only through Otium.exe. If that
            // file was removed manually, Windows Installer could remain registered
            // together with a Guardian service whose executable no longer exists.
            // A later MSI then cannot install its own service. Remove only that
            // demonstrably orphaned service; MajorUpgrade will handle the MSI record.
            if (!TryRemoveOrphanedGuardianService(logPath))
            {
                return OrphanedGuardianCleanupFailedExitCode;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (string argument in new[] { "/i", msiPath, "/qn", "/norestart", $"ADDLOCAL={features}", "/L*v", logPath })
            {
                startInfo.ArgumentList.Add(argument);
            }
            // SetupPlan classifies equal semantic versions as a repair, but each
            // test build can have a new MSI ProductCode. REINSTALL on a ProductCode
            // that is not installed makes Windows Installer preselect no features
            // and can turn the first-time install into REMOVE=ALL.
            if (forceReinstall && IsSameMsiProductInstalled(msiPath))
            {
                // Test builds intentionally share the public product version.
                // Force Windows Installer to replace equal-version binaries so
                // a repair really installs the newly embedded build.
                startInfo.ArgumentList.Add("REINSTALL=ALL");
                startInfo.ArgumentList.Add("REINSTALLMODE=amus");
            }

            bool restartGuardianOnFailure;
            try
            {
                restartGuardianOnFailure = StopGuardianForUpgrade();
            }
            catch
            {
                TryStartGuardian();
                throw;
            }
            int installerExitCode;
            try
            {
                StopInstalledOtiumProcesses();
                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Windows Installer could not be started.");
                await process.WaitForExitAsync();
                installerExitCode = process.ExitCode;
            }
            catch
            {
                guardianSnapshot.Restore();
                if (restartGuardianOnFailure)
                {
                    TryStartGuardian();
                }
                throw;
            }

            if (installerExitCode is not (0 or 1641 or 3010))
            {
                guardianSnapshot.Restore();
                if (restartGuardianOnFailure)
                {
                    TryStartGuardian();
                }
                return installerExitCode;
            }

            if (string.IsNullOrWhiteSpace(guardianPayload))
            {
                return !restartGuardianOnFailure || TryStartGuardian()
                    ? installerExitCode
                    : GuardianStartFailedExitCode;
            }

            int provisioningExitCode;
            try
            {
                provisioningExitCode = await ProvisionGuardianAsync(guardianPayload);
            }
            catch
            {
                guardianSnapshot.Restore();
                if (restartGuardianOnFailure)
                {
                    TryStartGuardian();
                }
                throw;
            }
            if (provisioningExitCode != 0)
            {
                guardianSnapshot.Restore();
                if (restartGuardianOnFailure)
                {
                    TryStartGuardian();
                }

                return provisioningExitCode == 6
                    ? GuardianCredentialUnavailableExitCode
                    : GuardianProvisioningFailedExitCode;
            }

            if (TryStartGuardian())
            {
                return installerExitCode;
            }

            guardianSnapshot.Restore();
            if (restartGuardianOnFailure)
            {
                TryStartGuardian();
            }
            return GuardianStartFailedExitCode;
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static bool StopGuardianForUpgrade()
    {
        try
        {
            using ServiceController controller = new("OtiumGuardian");
            controller.Refresh();
            bool wasRunning = controller.Status != ServiceControllerStatus.Stopped;
            if (!wasRunning)
            {
                return false;
            }

            if (controller.Status != ServiceControllerStatus.StopPending)
            {
                controller.Stop();
            }
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryRemoveOrphanedGuardianService(string logPath)
    {
        string installedExecutable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Otium",
            "Otium.exe");
        if (File.Exists(installedExecutable) || !GuardianServiceExists())
        {
            return true;
        }

        AppendRecoveryLog(logPath,
            "Guardian service is registered but its Otium executable is missing. Removing the orphaned service before MSI installation.");

        try
        {
            using (ServiceController controller = new("OtiumGuardian"))
            {
                controller.Refresh();
                if (controller.Status != ServiceControllerStatus.Stopped)
                {
                    if (controller.Status != ServiceControllerStatus.StopPending)
                    {
                        controller.Stop();
                    }

                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }

            ProcessStartInfo deleteInfo = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            deleteInfo.ArgumentList.Add("delete");
            deleteInfo.ArgumentList.Add("OtiumGuardian");

            using Process deleteProcess = Process.Start(deleteInfo)
                ?? throw new InvalidOperationException("The orphaned Guardian service cleanup could not be started.");
            string standardOutput = deleteProcess.StandardOutput.ReadToEnd();
            string standardError = deleteProcess.StandardError.ReadToEnd();
            deleteProcess.WaitForExit();
            AppendRecoveryLog(logPath,
                $"sc.exe delete exit code: {deleteProcess.ExitCode}. {standardOutput} {standardError}".Trim());

            if (deleteProcess.ExitCode != 0 && GuardianServiceExists())
            {
                return false;
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (GuardianServiceExists() && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }

            return !GuardianServiceExists();
        }
        catch (Exception exception)
        {
            AppendRecoveryLog(logPath, $"Orphaned Guardian cleanup failed: {exception}");
            return false;
        }
    }

    private static bool GuardianServiceExists()
    {
        try
        {
            using ServiceController controller = new("OtiumGuardian");
            controller.Refresh();
            _ = controller.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void AppendRecoveryLog(string logPath, string message)
    {
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Recovery must not fail merely because its diagnostic note could not be written.
        }
    }

    private static void StopInstalledOtiumProcesses()
    {
        string installedExecutable = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Otium",
            "Otium.exe"));
        foreach (Process process in Process.GetProcessesByName("Otium"))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited ||
                        !string.Equals(
                            Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                            installedExecutable,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(8000))
                    {
                        throw new InvalidOperationException(
                            $"Installed Otium process {process.Id} did not stop before the upgrade.");
                    }
                }
                catch (ArgumentException)
                {
                    // The process exited between enumeration and inspection.
                }
            }
        }
    }

    private static bool TryStartGuardian()
    {
        try
        {
            using ServiceController controller = new("OtiumGuardian");
            controller.Refresh();
            if (controller.Status == ServiceControllerStatus.StopPending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                controller.Refresh();
            }

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                controller.Start();
                controller.Refresh();
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

    private static async Task<int> ProvisionGuardianAsync(string guardianPayload)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Otium",
            "Otium.exe");
        if (!File.Exists(executable)) return 1;

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--provision-guardian");
        startInfo.ArgumentList.Add(guardianPayload);
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Guardian provisioning could not be started.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private sealed record GuardianMachineStateSnapshot(
        byte[]? Enrollment,
        byte[]? ProtectedSettings,
        byte[]? PostInstallMarker)
    {
        private static string ProtectionDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Otium");
        private static string EnrollmentPath => Path.Combine(ProtectionDirectory, "guardian-enrollment.json");
        private static string ProtectedSettingsPath => Path.Combine(ProtectionDirectory, "protected-settings.json");
        private static string PostInstallMarkerPath => Path.Combine(ProtectionDirectory, "post-install-control-center.pending");

        public static GuardianMachineStateSnapshot Capture() => new(
            ReadIfPresent(EnrollmentPath),
            ReadIfPresent(ProtectedSettingsPath),
            ReadIfPresent(PostInstallMarkerPath));

        public void Restore()
        {
            RestoreFile(EnrollmentPath, Enrollment);
            RestoreFile(ProtectedSettingsPath, ProtectedSettings);
            RestoreFile(PostInstallMarkerPath, PostInstallMarker);
        }

        private static byte[]? ReadIfPresent(string path) =>
            File.Exists(path) ? File.ReadAllBytes(path) : null;

        private static void RestoreFile(string path, byte[]? content)
        {
            if (content is null)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".rollback";
            try
            {
                File.WriteAllBytes(temporaryPath, content);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        }
    }

    private static async Task ExtractAndVerifyPayloadAsync(string destination)
    {
        await using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new FileNotFoundException("The signed setup executable does not contain its MSI payload.");
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[128 * 1024];
            int read;
            while ((read = await payload.ReadAsync(buffer)) > 0)
            {
                sourceHash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read));
            }
            await output.FlushAsync();
        }

        await using FileStream extracted = File.OpenRead(destination);
        byte[] extractedHash = await SHA256.HashDataAsync(extracted);
        if (!CryptographicOperations.FixedTimeEquals(sourceHash.GetHashAndReset(), extractedHash))
        {
            throw new InvalidDataException("The extracted installer payload failed its integrity check.");
        }
    }

    private static bool IsSameMsiProductInstalled(string msiPath)
    {
        string? productCode = ReadMsiProperty(msiPath, "ProductCode");
        return !string.IsNullOrWhiteSpace(productCode) &&
            MsiQueryProductState(productCode) == InstallState.Default;
    }

    private static string? ReadMsiProperty(string msiPath, string propertyName)
    {
        uint database = 0;
        uint view = 0;
        uint record = 0;
        try
        {
            if (MsiOpenDatabase(msiPath, IntPtr.Zero, out database) != 0)
            {
                return null;
            }

            string query = $"SELECT `Value` FROM `Property` WHERE `Property`='{propertyName}'";
            if (MsiDatabaseOpenView(database, query, out view) != 0 ||
                MsiViewExecute(view, 0) != 0 ||
                MsiViewFetch(view, out record) != 0)
            {
                return null;
            }

            uint capacity = 64;
            StringBuilder value = new((int)capacity);
            return MsiRecordGetString(record, 1, value, ref capacity) == 0
                ? value.ToString()
                : null;
        }
        finally
        {
            if (record != 0) MsiCloseHandle(record);
            if (view != 0) MsiCloseHandle(view);
            if (database != 0) MsiCloseHandle(database);
        }
    }

    private enum InstallState
    {
        Default = 5
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiOpenDatabaseW")]
    private static extern uint MsiOpenDatabase(string databasePath, IntPtr persist, out uint database);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiDatabaseOpenViewW")]
    private static extern uint MsiDatabaseOpenView(uint database, string query, out uint view);

    [DllImport("msi.dll")]
    private static extern uint MsiViewExecute(uint view, uint record);

    [DllImport("msi.dll")]
    private static extern uint MsiViewFetch(uint view, out uint record);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiRecordGetStringW")]
    private static extern uint MsiRecordGetString(
        uint record,
        uint field,
        StringBuilder value,
        ref uint valueLength);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiQueryProductStateW")]
    private static extern InstallState MsiQueryProductState(string productCode);

    [DllImport("msi.dll")]
    private static extern uint MsiCloseHandle(uint handle);

    private static void HardenDirectory(string directory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            directory, "/inheritance:r", "/grant:r", "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-544:(OI)(CI)F"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Installer staging permissions could not be configured.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Installer staging permissions could not be configured.");
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // The MSI is no longer executable from this process; Windows will clean up temporary files later.
        }
    }
}
