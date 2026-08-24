using System.Diagnostics;
using System.IO;
using Otium.Core.Models;

namespace Otium.App.Services;

public sealed class ApplicationRuleEnforcer
{
    private double _uncommittedSeconds;

    public bool Enforce(ControlSettings settings, UsageLedger ledger, TimeSpan elapsed)
    {
        if (settings.AppRules.Count == 0)
        {
            return false;
        }

        _uncommittedSeconds += Math.Max(0, elapsed.TotalSeconds);
        long accruedSeconds = (long)Math.Floor(_uncommittedSeconds);
        if (accruedSeconds > 0)
        {
            _uncommittedSeconds -= accruedSeconds;
        }

        Dictionary<string, AppRule> rules = settings.AppRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ExecutablePath))
            .GroupBy(rule => NormalizePath(rule.ExecutablePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> runningTrackedRules = [];
        bool changed = false;

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                string? executablePath = TryGetExecutablePath(process);
                if (executablePath is null || !rules.TryGetValue(NormalizePath(executablePath), out AppRule? rule))
                {
                    continue;
                }

                long usedSeconds = ledger.AppUsedSeconds.GetValueOrDefault(rule.Id);
                bool limitReached = rule.Mode == AppRuleMode.Limited &&
                    usedSeconds >= Math.Max(0, rule.DailyLimitMinutes) * 60L;
                if (rule.Mode == AppRuleMode.Blocked || limitReached)
                {
                    TryTerminate(process);
                    continue;
                }

                if (rule.Mode is AppRuleMode.Limited or AppRuleMode.Unlimited)
                {
                    runningTrackedRules.Add(rule.Id);
                }
            }
        }

        if (accruedSeconds <= 0)
        {
            return false;
        }

        foreach (Guid ruleId in runningTrackedRules)
        {
            ledger.AppUsedSeconds[ruleId] = ledger.AppUsedSeconds.GetValueOrDefault(ruleId) + accruedSeconds;
            changed = true;
        }

        return changed;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Protected/system processes may reject termination; the next poll retries.
        }
    }
}
