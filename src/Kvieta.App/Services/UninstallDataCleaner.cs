using System.IO;
using Microsoft.Win32;

namespace Kvieta.App.Services;

public sealed record UninstallCleanupResult(bool Succeeded, IReadOnlyList<string> FailedPaths);

public static class UninstallDataCleaner
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "Kvieta";

    public static UninstallCleanupResult Clean(string localDataPath, string userSid, bool removeLocalData)
    {
        List<string> failedPaths = [];
        TryRemoveStartupRegistration(userSid, failedPaths);

        if (removeLocalData)
        {
            TryDeleteKvietaDirectory(
                localDataPath,
                path => IsSafeLocalDataDirectory(path, userSid),
                failedPaths);
            TryDeleteKvietaDirectory(
                ProtectionServiceManager.ProtectionDataDirectory,
                IsSafeProtectionDataDirectory,
                failedPaths);
        }

        return new UninstallCleanupResult(failedPaths.Count == 0, failedPaths);
    }

    public static bool IsSafeLocalDataDirectory(string path)
    {
        try
        {
            DirectoryInfo directory = new(Path.GetFullPath(path));
            return string.Equals(directory.Name, "Kvieta", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(directory.Parent?.Name, "Local", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(directory.Parent?.Parent?.Name, "AppData", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSafeLocalDataDirectory(string path, string userSid)
    {
        if (!IsSafeLocalDataDirectory(path) || string.IsNullOrWhiteSpace(userSid))
        {
            return false;
        }

        try
        {
            using RegistryKey? profileKey = Registry.LocalMachine.OpenSubKey(
                $@"Software\Microsoft\Windows NT\CurrentVersion\ProfileList\{userSid}");
            string? profilePath = profileKey?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                return false;
            }

            string expected = Path.GetFullPath(Path.Combine(
                Environment.ExpandEnvironmentVariables(profilePath),
                "AppData",
                "Local",
                "Kvieta"));
            return string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSafeProtectionDataDirectory(string path)
    {
        try
        {
            string expected = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Kvieta"));
            return string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteKvietaDirectory(
        string path,
        Func<string, bool> safetyCheck,
        ICollection<string> failedPaths)
    {
        if (!safetyCheck(path))
        {
            failedPaths.Add(path);
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            failedPaths.Add(path);
        }
    }

    private static void TryRemoveStartupRegistration(string userSid, ICollection<string> failedPaths)
    {
        if (string.IsNullOrWhiteSpace(userSid) || !userSid.StartsWith("S-1-", StringComparison.Ordinal))
        {
            failedPaths.Add("Windows başlangıç kaydı");
            return;
        }

        try
        {
            using RegistryKey? key = Registry.Users.OpenSubKey($@"{userSid}\{RunKeyPath}", writable: true);
            key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
        }
        catch
        {
            failedPaths.Add("Windows başlangıç kaydı");
        }
    }
}
