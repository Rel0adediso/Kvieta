namespace Otium.Core.Models;

public enum AppRuleMode
{
    Blocked,
    Limited,
    Unlimited
}

public sealed class AppRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public AppRuleMode Mode { get; set; } = AppRuleMode.Blocked;
    public int DailyLimitMinutes { get; set; } = 60;
}
