using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Otium.Core.Models;

namespace Otium.App.Services;

public sealed class ApplicationRuleEnforcer
{
    private double _uncommittedSeconds;

    public bool Enforce(ControlSettings settings, UsageLedger ledger, TimeSpan elapsed)
    {
        if (settings.Mode == ControlMode.Awareness || settings.AppRules.Count == 0) return false;

        _uncommittedSeconds += Math.Max(0, elapsed.TotalSeconds);
        long accruedSeconds = (long)Math.Floor(_uncommittedSeconds);
        if (accruedSeconds > 0) _uncommittedSeconds -= accruedSeconds;

        List<ProcessSnapshot> processes = [];
        foreach (Process process in Process.GetProcesses())
        {
            if (process.Id == Environment.ProcessId)
            {
                process.Dispose();
                continue;
            }

            try
            {
                string? path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    processes.Add(new ProcessSnapshot(
                        process,
                        path,
                        TryGetParentProcessId(process),
                        TryGetPackageFamilyName(process)));
                    continue;
                }
            }
            catch { }
            process.Dispose();
        }

        try
        {
            Dictionary<int, AppRule> matched = [];
            foreach (ProcessSnapshot process in processes)
            {
                AppRule? rule = settings.AppRules.LastOrDefault(candidate =>
                    ApplicationIdentityService.MatchesRule(candidate, process.Path, process.PackageFamilyName));
                if (rule is not null) matched[process.Process.Id] = rule;
            }

            bool addedChild;
            do
            {
                addedChild = false;
                foreach (ProcessSnapshot process in processes)
                {
                    if (!matched.ContainsKey(process.Process.Id) && process.ParentProcessId is { } parentId &&
                        matched.TryGetValue(parentId, out AppRule? parentRule) && parentRule.IncludeChildProcesses)
                    {
                        matched[process.Process.Id] = parentRule;
                        addedChild = true;
                    }
                }
            } while (addedChild);

            HashSet<Guid> runningTrackedRules = [];
            foreach (ProcessSnapshot process in processes)
            {
                if (!matched.TryGetValue(process.Process.Id, out AppRule? rule)) continue;
                long usedSeconds = ledger.AppUsedSeconds.GetValueOrDefault(rule.Id);
                bool limitReached = rule.Mode == AppRuleMode.Limited &&
                    usedSeconds >= Math.Max(0, rule.DailyLimitMinutes) * 60L;
                if (rule.Mode == AppRuleMode.Blocked || limitReached) TryTerminate(process.Process);
                else runningTrackedRules.Add(rule.Id);
            }

            if (accruedSeconds <= 0) return false;
            foreach (Guid ruleId in runningTrackedRules)
            {
                ledger.AppUsedSeconds[ruleId] = ledger.AppUsedSeconds.GetValueOrDefault(ruleId) + accruedSeconds;
            }
            return runningTrackedRules.Count > 0;
        }
        finally
        {
            foreach (ProcessSnapshot process in processes) process.Process.Dispose();
        }
    }

    private static int? TryGetParentProcessId(Process process)
    {
        try
        {
            PROCESS_BASIC_INFORMATION information = new();
            int status = NtQueryInformationProcess(
                process.Handle,
                0,
                ref information,
                Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(),
                out _);
            return status == 0 ? checked((int)information.InheritedFromUniqueProcessId) : null;
        }
        catch { return null; }
    }

    private static string? TryGetPackageFamilyName(Process process)
    {
        try
        {
            uint length = 0;
            int result = GetPackageFamilyName(process.Handle, ref length, null);
            if (result != 122 || length == 0) return null;
            StringBuilder value = new(checked((int)length));
            return GetPackageFamilyName(process.Handle, ref length, value) == 0 ? value.ToString() : null;
        }
        catch { return null; }
    }

    private static void TryTerminate(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(
        IntPtr process,
        ref uint packageFamilyNameLength,
        StringBuilder? packageFamilyName);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    private sealed record ProcessSnapshot(
        Process Process,
        string Path,
        int? ParentProcessId,
        string? PackageFamilyName);
}
