using System.Diagnostics;
using System.Security.Principal;

namespace Otium.App.Services;

public static class WindowsAdministratorVerificationService
{
    private static readonly HashSet<string> AllowedAuditEvents = new(StringComparer.Ordinal)
    {
        "recovery.codes.generate",
        "recovery.code.consume",
        "recovery.last-known-good.restore",
        "recovery.installer.repair",
        "recovery.clock-anomaly.clear"
    };

    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsAllowedAuditEvent(string? eventName) =>
        eventName is not null && AllowedAuditEvents.Contains(eventName);

    public static async Task<bool> RequestAsync(string auditEvent)
    {
        if (!IsAllowedAuditEvent(auditEvent))
        {
            throw new ArgumentException("Unsupported recovery audit event.", nameof(auditEvent));
        }

        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Otium executable path is unavailable.");
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            Arguments = $"--windows-admin-verification --audit-event {auditEvent}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
