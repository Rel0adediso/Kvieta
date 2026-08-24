using System.Diagnostics;
using System.Runtime.InteropServices;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public sealed class ForegroundApplicationTracker
{
    private double _uncommittedSeconds;

    public bool Sample(UsageLedger ledger, TimeSpan elapsed)
    {
        double sampledSeconds = Math.Clamp(elapsed.TotalSeconds, 0, 5);
        _uncommittedSeconds += sampledSeconds;
        long wholeSeconds = (long)Math.Floor(_uncommittedSeconds);
        if (wholeSeconds <= 0)
        {
            return false;
        }

        _uncommittedSeconds -= wholeSeconds;
        string? applicationId = TryGetForegroundApplicationId();
        return applicationId is not null &&
            AwarenessUsageCounter.Accrue(ledger, applicationId, TimeSpan.FromSeconds(wholeSeconds), DateTimeOffset.Now);
    }

    private static string? TryGetForegroundApplicationId()
    {
        nint window = GetForegroundWindow();
        if (window == 0)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0 || processId == Environment.ProcessId)
        {
            return null;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName ?? $"{process.ProcessName}.exe";
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
