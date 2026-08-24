namespace Otium.Core.Models;

public enum SessionState
{
    Ready,
    Active,
    Paused,
    TimeExpired,
    OutsideSchedule
}

public sealed class UsageLedger
{
    public int SchemaVersion { get; set; } = 2;
    public DateOnly LocalDay { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public long UsedSeconds { get; set; }
    public int BonusMinutes { get; set; }
    public Dictionary<Guid, long> AppUsedSeconds { get; set; } = [];
    public int BreakCount { get; set; }
    public int LimitReachedCount { get; set; }
    public int ExtraTimeGrantCount { get; set; }
    public List<DailyUsageRecord> History { get; set; } = [];
    public List<UsageEventRecord> RecentEvents { get; set; } = [];
    public SessionState State { get; set; } = SessionState.Ready;
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? ClockRollbackUntilUtc { get; set; }
}

public enum UsageEventKind
{
    BreakStarted,
    LimitReached,
    ExtraTimeGranted,
    PolicyChanged
}

public sealed class UsageEventRecord
{
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public UsageEventKind Kind { get; set; }
    public int Value { get; set; }
}

public sealed class DailyUsageRecord
{
    public DateOnly LocalDay { get; set; }
    public long UsedSeconds { get; set; }
    public int BonusMinutes { get; set; }
    public int BreakCount { get; set; }
    public int LimitReachedCount { get; set; }
    public int ExtraTimeGrantCount { get; set; }
    public List<AppUsageRecord> Applications { get; set; } = [];
}

public sealed class AppUsageRecord
{
    public Guid RuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long UsedSeconds { get; set; }
}

public sealed record SessionSnapshot(
    SessionState State,
    long UsedSeconds,
    long LimitSeconds,
    long RemainingSeconds,
    string Reason,
    DateTimeOffset? AllowedUntil);
