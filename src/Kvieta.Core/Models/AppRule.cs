namespace Kvieta.Core.Models;

public enum AppRuleMode
{
    Blocked,
    Limited,
    ScheduleOnly,
    FocusBlocked,
    Unlimited
}

public sealed class AppRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;
    public string PublisherThumbprint { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public bool RequireSha256 { get; set; }
    public string PackageFamilyName { get; set; } = string.Empty;
    public bool IncludeChildProcesses { get; set; } = true;
    public List<string> LauncherExecutablePaths { get; set; } = [];
    public AppRuleMode Mode { get; set; } = AppRuleMode.Blocked;
    public int DailyLimitMinutes { get; set; } = 60;
}
