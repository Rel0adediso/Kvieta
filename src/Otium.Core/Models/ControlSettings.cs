namespace Otium.Core.Models;

public enum LimitReachedAction
{
    ShowBlockScreen,
    LockWindows,
    SignOut
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum LanguagePreference
{
    Turkish,
    English
}

public enum ControlMode
{
    Protected,
    Personal
}

public sealed class ControlSettings
{
    public int SchemaVersion { get; set; } = 2;
    public bool SetupCompleted { get; set; }
    public ControlMode Mode { get; set; } = ControlMode.Protected;
    public string DeviceName { get; set; } = "Bu Bilgisayar";
    public int DefaultDailyLimitMinutes { get; set; } = 180;
    public LimitReachedAction LimitAction { get; set; } = LimitReachedAction.SignOut;
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public LanguagePreference Language { get; set; } = LanguagePreference.Turkish;
    public bool StartWithWindows { get; set; }
    public int PersonalChangeDelayMinutes { get; set; } = 60;
    public PendingPolicyChange? PendingChange { get; set; }
    public AdminCredential AdminPin { get; set; } = new();
    public List<int> WarningMinutes { get; set; } = [15, 5, 1];
    public List<DaySchedule> Schedule { get; set; } = CreateDefaultSchedule();
    public List<TemporaryAllowance> TemporaryAllowances { get; set; } = [];
    public List<AppRule> AppRules { get; set; } = [];

    public static List<DaySchedule> CreateDefaultSchedule()
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => new DaySchedule
            {
                Day = day,
                IsEnabled = true,
                AllowedFrom = day is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? new TimeOnly(10, 0)
                    : new TimeOnly(9, 0),
                AllowedUntil = day is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? new TimeOnly(23, 0)
                    : new TimeOnly(21, 0),
                DailyLimitMinutes = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? 300 : 180
            })
            .OrderBy(item => item.Day == DayOfWeek.Sunday ? 7 : (int)item.Day)
            .ToList();
    }
}

public sealed class TemporaryAllowance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    public TimeOnly AllowedFrom { get; set; } = new(18, 0);
    public TimeOnly AllowedUntil { get; set; } = new(21, 0);
    public int BonusMinutes { get; set; } = 60;
    public string Note { get; set; } = string.Empty;
}

public sealed class PendingPolicyChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ApplyAfterUtc { get; set; }
    public ControlSettings TargetSettings { get; set; } = new();
}
