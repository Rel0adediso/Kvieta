using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Otium.Core.Models;

namespace Otium.App.Services;

public sealed class ApplicationRuleEnforcer : IDisposable
{
    private double _uncommittedSeconds;
    private readonly object _observationGate = new();
    private readonly Dictionary<int, ProcessObservation> _observations = [];
    private IReadOnlyList<AppRule> _activeRules = [];
    private ControlSettings? _activeSettings;
    private readonly ManagementEventWatcher? _processStartWatcher;

    public ApplicationRuleEnforcer()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _processStartWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT ProcessID, ParentProcessID FROM Win32_ProcessStartTrace"));
            _processStartWatcher.EventArrived += ProcessStarted;
            _processStartWatcher.Start();
        }
        catch
        {
            _processStartWatcher?.Dispose();
            _processStartWatcher = null;
        }
    }

    public bool Enforce(ControlSettings settings, UsageLedger ledger, TimeSpan elapsed)
    {
        lock (_observationGate)
        {
            bool settingsChanged = !ReferenceEquals(_activeSettings, settings);
            if (settingsChanged)
            {
                _activeSettings = settings;
                _activeRules = settings.AppRules.ToArray();
                foreach (int processId in _observations.Keys.ToArray())
                {
                    _observations[processId] = _observations[processId] with { RuleId = null };
                }
            }
            if (settingsChanged) ResolveObservedRules(recheckDirectRules: true);
            PruneObservations();
        }
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
                        TryGetPackageFamilyName(process),
                        TryGetStartTimeUtcTicks(process)));
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
                if (rule is null)
                {
                    lock (_observationGate)
                    {
                        if (_observations.TryGetValue(process.Process.Id, out ProcessObservation? observation) &&
                            process.StartTimeUtcTicks != 0 &&
                            observation.StartTimeUtcTicks == process.StartTimeUtcTicks &&
                            observation.RuleId is { } ruleId)
                        {
                            rule = settings.AppRules.LastOrDefault(candidate => candidate.Id == ruleId);
                        }
                    }
                }
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

            lock (_observationGate)
            {
                foreach (ProcessSnapshot process in processes)
                {
                    Guid? ruleId = matched.TryGetValue(process.Process.Id, out AppRule? rule) ? rule.Id : null;
                    _observations[process.Process.Id] = new ProcessObservation(
                        process.Process.Id,
                        process.ParentProcessId,
                        process.Path,
                        process.PackageFamilyName,
                        process.StartTimeUtcTicks,
                        DateTimeOffset.UtcNow,
                        ruleId);
                }
            }

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

    public void Dispose()
    {
        if (_processStartWatcher is null) return;
        try { _processStartWatcher.Stop(); }
        catch { }
        _processStartWatcher.EventArrived -= ProcessStarted;
        _processStartWatcher.Dispose();
    }

    private void ProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            int processId = checked((int)(uint)e.NewEvent["ProcessID"]);
            int parentProcessId = checked((int)(uint)e.NewEvent["ParentProcessID"]);
            using Process process = Process.GetProcessById(processId);
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path)) return;
            string? packageFamilyName = TryGetPackageFamilyName(process);
            long startTime = TryGetStartTimeUtcTicks(process);
            lock (_observationGate)
            {
                AppRule? directRule = _activeRules.LastOrDefault(candidate =>
                    ApplicationIdentityService.MatchesRule(candidate, path, packageFamilyName));
                _observations[processId] = new ProcessObservation(
                    processId,
                    parentProcessId,
                    path,
                    packageFamilyName,
                    startTime,
                    DateTimeOffset.UtcNow,
                    directRule?.Id);
                ResolveObservedRules(recheckDirectRules: false);
            }
        }
        catch
        {
            // Snapshot enforcement remains available when a short-lived process cannot be inspected.
        }
    }

    private void ResolveObservedRules(bool recheckDirectRules)
    {
        if (recheckDirectRules)
        {
            foreach (int processId in _observations.Keys.ToArray())
            {
                ProcessObservation observation = _observations[processId];
                AppRule? directRule = _activeRules.LastOrDefault(candidate =>
                    ApplicationIdentityService.MatchesRule(candidate, observation.Path, observation.PackageFamilyName));
                if (directRule is not null) _observations[processId] = observation with { RuleId = directRule.Id };
            }
        }

        bool changed;
        do
        {
            changed = false;
            foreach (int processId in _observations.Keys.ToArray())
            {
                ProcessObservation child = _observations[processId];
                if (child.RuleId is not null || child.ParentProcessId is not { } parentId ||
                    !_observations.TryGetValue(parentId, out ProcessObservation? parent) ||
                    parent.RuleId is not { } parentRuleId ||
                    parent.ObservedAtUtc > child.ObservedAtUtc ||
                    child.ObservedAtUtc - parent.ObservedAtUtc > TimeSpan.FromMinutes(10)) continue;
                AppRule? rule = _activeRules.LastOrDefault(candidate => candidate.Id == parentRuleId);
                if (rule?.IncludeChildProcesses != true) continue;
                _observations[processId] = child with { RuleId = parentRuleId };
                changed = true;
            }
        } while (changed);
    }

    private void PruneObservations()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (int processId in _observations.Where(pair => pair.Value.ObservedAtUtc < cutoff).Select(pair => pair.Key).ToArray())
        {
            _observations.Remove(processId);
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

    private static long TryGetStartTimeUtcTicks(Process process)
    {
        try { return process.StartTime.ToUniversalTime().Ticks; }
        catch { return 0; }
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
        string? PackageFamilyName,
        long StartTimeUtcTicks);

    private sealed record ProcessObservation(
        int ProcessId,
        int? ParentProcessId,
        string Path,
        string? PackageFamilyName,
        long StartTimeUtcTicks,
        DateTimeOffset ObservedAtUtc,
        Guid? RuleId);
}
