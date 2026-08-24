using System.Diagnostics;
using System.IO;
using Otium.Core.Models;

namespace Otium.App.Services;

public sealed record ApplicationSuggestion(string Name, string ExecutablePath, long UsedSeconds);

public static class ApplicationSuggestionService
{
    private static readonly HashSet<string> IgnoredWindowsProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost.exe",
        "dwm.exe",
        "explorer.exe",
        "SearchHost.exe",
        "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe",
        "SystemSettings.exe",
        "Taskmgr.exe",
        "TextInputHost.exe"
    };

    public static IReadOnlyList<ApplicationSuggestion> GetSuggestions(
        UsageLedger? ledger,
        IEnumerable<string> excludedPaths,
        int maximum = 12)
    {
        HashSet<string> excluded = excludedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> usage = BuildUsageLookup(ledger);
        Dictionary<string, ApplicationSuggestion> suggestions = new(StringComparer.OrdinalIgnoreCase);

        if (ledger is not null)
        {
            IEnumerable<string> recordedPaths = ledger.ForegroundAppUsedSeconds.Keys
                .Concat(ledger.History.SelectMany(day => day.ForegroundApplications.Select(app => app.ApplicationId)));
            foreach (string recordedPath in recordedPaths.Where(File.Exists))
            {
                AddSuggestion(recordedPath, usage, excluded, suggestions);
            }
        }

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == 0)
                    {
                        continue;
                    }

                    string? executablePath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
                    {
                        AddSuggestion(executablePath, usage, excluded, suggestions);
                    }
                }
                catch
                {
                    // Protected Windows processes do not expose their executable path.
                }
            }
        }

        return suggestions.Values
            .OrderByDescending(item => item.UsedSeconds)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Max(1, maximum))
            .ToList();
    }

    private static Dictionary<string, long> BuildUsageLookup(UsageLedger? ledger)
    {
        Dictionary<string, long> result = new(StringComparer.OrdinalIgnoreCase);
        if (ledger is null)
        {
            return result;
        }

        foreach ((string id, long seconds) in ledger.ForegroundAppUsedSeconds)
        {
            AddUsage(result, id, seconds);
        }

        DateOnly cutoff = DateOnly.FromDateTime(DateTime.Today).AddDays(-6);
        foreach (AwarenessAppUsageRecord application in ledger.History
                     .Where(day => day.LocalDay >= cutoff)
                     .SelectMany(day => day.ForegroundApplications))
        {
            AddUsage(result, application.ApplicationId, application.UsedSeconds);
        }

        return result;
    }

    private static void AddUsage(Dictionary<string, long> usage, string id, long seconds)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        usage[id] = usage.GetValueOrDefault(id) + Math.Max(0, seconds);
        string fileName = Path.GetFileName(id);
        if (!string.Equals(fileName, id, StringComparison.OrdinalIgnoreCase))
        {
            usage[fileName] = usage.GetValueOrDefault(fileName) + Math.Max(0, seconds);
        }
    }

    private static void AddSuggestion(
        string executablePath,
        IReadOnlyDictionary<string, long> usage,
        IReadOnlySet<string> excluded,
        IDictionary<string, ApplicationSuggestion> suggestions)
    {
        string fullPath = NormalizePath(executablePath);
        if (excluded.Contains(fullPath) ||
            string.Equals(Path.GetFileName(fullPath), "Otium.exe", StringComparison.OrdinalIgnoreCase) ||
            IgnoredWindowsProcesses.Contains(Path.GetFileName(fullPath)) ||
            suggestions.ContainsKey(fullPath))
        {
            return;
        }

        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(fullPath);
            string name = string.IsNullOrWhiteSpace(version.ProductName)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : version.ProductName.Trim();
            long usedSeconds = Math.Max(
                usage.GetValueOrDefault(fullPath),
                usage.GetValueOrDefault(Path.GetFileName(fullPath)));
            suggestions[fullPath] = new ApplicationSuggestion(name, fullPath, usedSeconds);
        }
        catch
        {
            // Skip files that disappear or become inaccessible while suggestions are built.
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
    }
}
