using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Otium.Core.Services;

namespace Otium.App.Services;

public static class WindowsAdministratorVerificationService
{
    private const string CompanionFirewallRuleName = "Otium Local Companion";
    private static readonly HashSet<string> AllowedAuditEvents = new(StringComparer.Ordinal)
    {
        "recovery.codes.generate",
        "recovery.code.consume",
        "recovery.manager-device.enroll",
        "recovery.manager-device.revoke",
        "recovery.manager-device.transfer",
        "recovery.manager-device.consume",
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

        if (IsAdministrator())
        {
            if (RequiresLocalCompanionFirewall(auditEvent) &&
                !await EnsureLocalCompanionFirewallRuleAsync())
            {
                return false;
            }
            try
            {
                await new SecurityAuditLog().AppendAsync(auditEvent, "windows-admin-authorized");
            }
            catch
            {
                // Non-critical diagnostic
            }
            return true;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

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
        catch (Exception)
        {
            return false;
        }
    }

    private static bool RequiresLocalCompanionFirewall(string auditEvent) =>
        auditEvent is "recovery.manager-device.enroll" or
            "recovery.manager-device.transfer" or
            "recovery.manager-device.consume";

    public static async Task<bool> EnsureLocalCompanionFirewallRuleAsync()
    {
        if (!IsAdministrator()) return false;
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Otium executable path is unavailable.");
        string netsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");

        await RunNetshAsync(netsh,
            "advfirewall", "firewall", "delete", "rule",
            $"name={CompanionFirewallRuleName}", $"program={executable}");
        return await RunNetshAsync(netsh,
            "advfirewall", "firewall", "add", "rule",
            $"name={CompanionFirewallRuleName}", "dir=in", "action=allow",
            $"program={executable}", "protocol=TCP",
            $"localport={LocalNetworkHttpServer.FirstCompanionPort}-{LocalNetworkHttpServer.LastCompanionPort}",
            "profile=private", "remoteip=localsubnet", "enable=yes") == 0;
    }

    private static async Task<int> RunNetshAsync(string netsh, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = netsh,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process? process = Process.Start(startInfo);
        if (process is null) return -1;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
