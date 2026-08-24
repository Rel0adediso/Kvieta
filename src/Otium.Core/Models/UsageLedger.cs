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
    public int SchemaVersion { get; set; } = 6;
    public long DataGeneration { get; set; }
    public DateOnly? RetainedFromDay { get; set; }
    public DateOnly LocalDay { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public long UsedSeconds { get; set; }
    public int BonusMinutes { get; set; }
    public Dictionary<Guid, long> AppUsedSeconds { get; set; } = [];
    public long AwarenessUsedSeconds { get; set; }
    public Dictionary<string, long> ForegroundAppUsedSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, long> AwarenessHourlyUsedSeconds { get; set; } = [];
    public int BreakCount { get; set; }
    public int LimitReachedCount { get; set; }
    public int ExtraTimeGrantCount { get; set; }
    public List<DailyUsageRecord> History { get; set; } = [];
    public List<UsageEventRecord> RecentEvents { get; set; } = [];
    public SessionState State { get; set; } = SessionState.Ready;
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? ClockRollbackUntilUtc { get; set; }
    public DateTimeOffset LastTrustedUtc { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? EstimatedBootUtc { get; set; }
    public long? LastMonotonicMilliseconds { get; set; }
    public string? LastBootId { get; set; }
    public int? LastUtcOffsetMinutes { get; set; }
    public ClockChangeKind LastClockChange { get; set; }
    public DateTimeOffset? ClockChangeDetectedAtUtc { get; set; }
    public bool ClockAnomalyRequiresRecovery { get; set; }
}

public enum ClockChangeKind
{
    None,
    Reboot,
    TimeZoneChanged,
    ForwardJump,
    Rollback
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
    public long AwarenessUsedSeconds { get; set; }
    public List<AwarenessAppUsageRecord> ForegroundApplications { get; set; } = [];
    public Dictionary<int, long> AwarenessHourlyUsedSeconds { get; set; } = [];
}

public sealed class AppUsageRecord
{
    public Guid RuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long UsedSeconds { get; set; }
}

public sealed class AwarenessAppUsageRecord
{
    public string ApplicationId { get; set; } = string.Empty;
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
