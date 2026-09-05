using Kvieta.Core.Models;
namespace Kvieta.Core.Services;

public sealed class SessionEngine
{
    private readonly ControlSettings _settings;
    private bool _testOverrideActive;
    private long _testOverrideLimitSeconds;

    public SessionEngine(ControlSettings settings, UsageLedger ledger, DateTimeOffset now)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        Refresh(now);

        // A prototype restart never resumes an active desktop silently.
        if (Ledger.State == SessionState.Active)
        {
            Ledger.State = SessionState.Ready;
        }
    }

    public UsageLedger Ledger { get; }

    public ClockChangeKind ObserveClock(DateTimeOffset now, TimeSpan systemUptime, string? bootId = null)
    {
        ClockChangeKind change = ClockIntegrityMonitor.Observe(Ledger, now, systemUptime, bootId);
        Refresh(now);
        return change;
    }

    public SessionSnapshot GetSnapshot(DateTimeOffset now)
    {
        Refresh(now);
        ScheduleStatus schedule = ScheduleEvaluator.Evaluate(_settings, now);
        long limitSeconds = GetLimitSeconds(now);
        long remainingSeconds = Math.Max(0, limitSeconds - Ledger.UsedSeconds);

        string reason = Ledger.State switch
        {
            SessionState.Active => Localize("Oturum aktif. Süre işliyor.", "Session active. Time is running."),
            SessionState.Paused => Localize("Mola modu aktif. Süre durduruldu.", "Break active. Time is paused."),
            SessionState.TimeExpired => Localize("Bugünkü kullanım süresi tamamlandı.", "Today's usage time is complete."),
            SessionState.OutsideSchedule when Ledger.ClockRollbackUntilUtc is not null =>
                Localize("Sistem saati geriye alındı. Önce doğru zamanı geri yükle.", "The system clock was moved back. Restore the correct time first."),
            SessionState.OutsideSchedule => schedule.Reason,
            _ => Localize("Oturum başlatılmaya hazır.", "Session is ready to start.")
        };

        return new SessionSnapshot(
            Ledger.State,
            Ledger.UsedSeconds,
            limitSeconds,
            remainingSeconds,
            reason,
            schedule.AllowedUntil);
    }

    private string Localize(string turkish, string english) =>
        _settings.Language == LanguagePreference.English ? english : turkish;

    public bool StartOrResume(DateTimeOffset now)
    {
        Refresh(now);
        if (Ledger.State is not (SessionState.Ready or SessionState.Paused))
        {
            return false;
        }

        ScheduleStatus schedule = ScheduleEvaluator.Evaluate(_settings, now);
        if (_settings.Mode != UsageMode.Insights &&
            (!schedule.IsAllowed || Ledger.UsedSeconds >= GetLimitSeconds(now)))
        {
            Refresh(now);
            return false;
        }

        Ledger.State = SessionState.Active;
        Touch(now);
        return true;
    }

    public bool Pause(DateTimeOffset now)
    {
        Refresh(now);
        if (Ledger.State != SessionState.Active)
        {
            return false;
        }

        Ledger.State = SessionState.Paused;
        Ledger.BreakCount++;
        AddEvent(UsageEventKind.BreakStarted, now);
        Touch(now);
        return true;
    }

    public void EndSession(DateTimeOffset now)
    {
        Refresh(now);
        if (Ledger.State is SessionState.Active or SessionState.Paused)
        {
            Ledger.State = SessionState.Ready;
            Touch(now);
        }
    }

    public long Accrue(TimeSpan elapsed, DateTimeOffset now)
    {
        long seconds = Math.Max(0, (long)Math.Floor(elapsed.TotalSeconds));
        if (seconds == 0 || elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        DateTimeOffset cursor = now.AddSeconds(-seconds);
        Refresh(cursor);
        long remaining = seconds;
        long accrued = 0;
        while (remaining > 0 && DateOnly.FromDateTime(cursor.DateTime) != DateOnly.FromDateTime(now.DateTime))
        {
            DateTimeOffset nextMidnight = new(cursor.Date.AddDays(1), cursor.Offset);
            long beforeMidnight = Math.Min(
                remaining,
                Math.Max(1, (long)Math.Ceiling((nextMidnight - cursor).TotalSeconds)));
            accrued += AccrueCurrentDay(beforeMidnight, nextMidnight.AddTicks(-1));
            remaining -= beforeMidnight;
            bool shouldContinue = Ledger.State == SessionState.Active;
            Refresh(nextMidnight);
            if (shouldContinue && Ledger.State == SessionState.Ready)
            {
                StartOrResume(nextMidnight);
            }
            cursor = nextMidnight;
        }

        Refresh(now);
        if (remaining > 0)
        {
            accrued += AccrueCurrentDay(remaining, now);
        }
        return accrued;
    }

    private long AccrueCurrentDay(long seconds, DateTimeOffset now)
    {
        if (Ledger.State != SessionState.Active || seconds <= 0)
        {
            return 0;
        }

        if (_settings.Mode == UsageMode.Insights)
        {
            long previous = Ledger.UsedSeconds;
            Ledger.UsedSeconds = Math.Min(24 * 60 * 60, Ledger.UsedSeconds + seconds);
            Ledger.State = SessionState.Active;
            Touch(now);
            return Ledger.UsedSeconds - previous;
        }

        long limitSeconds = GetLimitSeconds(now);
        long previousUsedSeconds = Ledger.UsedSeconds;
        Ledger.UsedSeconds = Math.Min(limitSeconds, Ledger.UsedSeconds + seconds);
        long accruedSeconds = Ledger.UsedSeconds - previousUsedSeconds;
        if (_testOverrideActive && Ledger.UsedSeconds >= limitSeconds)
        {
            _testOverrideActive = false;
            Refresh(now);
            return accruedSeconds;
        }

        bool reachedLimit = Ledger.UsedSeconds >= limitSeconds;
        Ledger.State = reachedLimit ? SessionState.TimeExpired : SessionState.Active;
        if (reachedLimit)
        {
            Ledger.LimitReachedCount++;
            AddEvent(UsageEventKind.LimitReached, now);
        }
        Touch(now);
        return accruedSeconds;
    }

    public void ForceStartForTesting(DateTimeOffset now)
    {
        Ledger.ClockRollbackUntilUtc = null;
        Ledger.LastUpdatedUtc = now.ToUniversalTime();
        Refresh(now);
        _testOverrideLimitSeconds = Math.Max(GetNormalLimitSeconds(now), Ledger.UsedSeconds) + 60 * 60;
        _testOverrideActive = true;
        Ledger.State = SessionState.Active;
        Touch(now);
    }

    public void AddBonusMinutes(int minutes, DateTimeOffset now)
    {
        if (minutes <= 0)
        {
            return;
        }

        Refresh(now);
        Ledger.BonusMinutes = Math.Min(1440, Ledger.BonusMinutes + minutes);
        Ledger.ExtraTimeGrantCount++;
        AddEvent(UsageEventKind.ExtraTimeGranted, now, minutes);
        if (Ledger.State == SessionState.TimeExpired && ScheduleEvaluator.Evaluate(_settings, now).IsAllowed)
        {
            Ledger.State = SessionState.Ready;
        }

        Touch(now);
    }

    private void Refresh(DateTimeOffset now)
    {
        DateTimeOffset utcNow = now.ToUniversalTime();
        if (Ledger.ClockAnomalyRequiresRecovery)
        {
            Ledger.State = SessionState.OutsideSchedule;
            return;
        }
        if (Ledger.LastUpdatedUtc != DateTimeOffset.MinValue &&
            Ledger.LastUpdatedUtc > utcNow.AddMinutes(5))
        {
            Ledger.ClockRollbackUntilUtc = Ledger.LastUpdatedUtc;
            Ledger.State = SessionState.OutsideSchedule;
            return;
        }

        if (Ledger.ClockRollbackUntilUtc is { } rollbackUntil && rollbackUntil > utcNow)
        {
            Ledger.State = SessionState.OutsideSchedule;
            return;
        }

        Ledger.ClockRollbackUntilUtc = null;
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        if (Ledger.LocalDay != today)
        {
            ArchiveCurrentDay(today);
            Ledger.LocalDay = today;
            Ledger.UsedSeconds = 0;
            Ledger.BonusMinutes = 0;
            Ledger.AppUsedSeconds.Clear();
            Ledger.AwarenessUsedSeconds = 0;
            Ledger.ForegroundAppUsedSeconds.Clear();
            Ledger.AwarenessHourlyUsedSeconds.Clear();
            Ledger.BreakCount = 0;
            Ledger.LimitReachedCount = 0;
            Ledger.ExtraTimeGrantCount = 0;
            Ledger.SummaryReviewed = false;
            Ledger.FocusSessionCount = 0;
            Ledger.FocusCompletedSeconds = 0;
            Ledger.RhythmExcused = false;
            Ledger.RhythmGoal = null;
            Ledger.RhythmFocusTargetKind = null;
            Ledger.RhythmGoalTarget = 0;
            Ledger.RhythmDailyLimitMinutes = null;
            Ledger.RhythmApprovedMinutes = 0;
            Ledger.RhythmPlannedRest = false;
            Ledger.RhythmMeasurementAvailable = false;
            Ledger.State = SessionState.Ready;
            Touch(now);
        }

        RhythmStreakAnalyzer.CaptureCurrentGoal(_settings, Ledger);

        if (_testOverrideActive)
        {
            Ledger.State = SessionState.Active;
            Touch(now);
            return;
        }

        if (_settings.Mode == UsageMode.Insights)
        {
            if (Ledger.State is SessionState.OutsideSchedule or SessionState.TimeExpired)
            {
                Ledger.State = SessionState.Ready;
            }

            Touch(now);
            return;
        }

        ScheduleStatus schedule = ScheduleEvaluator.Evaluate(_settings, now);
        if (schedule.IsTemporaryAllowanceActive)
        {
            DaySchedule? regularSchedule = _settings.Schedule.FirstOrDefault(item => item.Day == today.DayOfWeek);
            int regularLimit = regularSchedule is { IsEnabled: true } ? regularSchedule.DailyLimitMinutes : 0;
            Ledger.RhythmApprovedMinutes = Math.Max(
                Ledger.RhythmApprovedMinutes,
                Math.Max(0, schedule.DailyLimitMinutes - regularLimit));
        }
        if (!schedule.IsAllowed)
        {
            Ledger.State = SessionState.OutsideSchedule;
            Touch(now);
            return;
        }

        if (Ledger.UsedSeconds >= GetLimitSeconds(now))
        {
            Ledger.State = SessionState.TimeExpired;
            Touch(now);
            return;
        }

        if (Ledger.State is SessionState.OutsideSchedule or SessionState.TimeExpired)
        {
            Ledger.State = SessionState.Ready;
            Touch(now);
        }
    }

    private long GetLimitSeconds(DateTimeOffset now)
    {
        return _testOverrideActive ? _testOverrideLimitSeconds : GetNormalLimitSeconds(now);
    }

    private long GetNormalLimitSeconds(DateTimeOffset now)
    {
        if (_settings.Mode == UsageMode.Insights)
        {
            return 24 * 60 * 60;
        }

        int scheduledMinutes = ScheduleEvaluator.Evaluate(_settings, now).DailyLimitMinutes;
        return Math.Clamp(scheduledMinutes + Ledger.BonusMinutes, 0, 1440) * 60L;
    }

    private void Touch(DateTimeOffset now)
    {
        Ledger.LastUpdatedUtc = now.ToUniversalTime();
    }

    private void ArchiveCurrentDay(DateOnly retentionReferenceDay)
    {
        Dictionary<Guid, string> names = _settings.AppRules.ToDictionary(rule => rule.Id, rule => rule.Name);
        DailyUsageRecord record = new()
        {
            LocalDay = Ledger.LocalDay,
            UsedSeconds = Ledger.UsedSeconds,
            BonusMinutes = Ledger.BonusMinutes,
            BreakCount = Ledger.BreakCount,
            LimitReachedCount = Ledger.LimitReachedCount,
            ExtraTimeGrantCount = Ledger.ExtraTimeGrantCount,
            SummaryReviewed = Ledger.SummaryReviewed,
            FocusSessionCount = Ledger.FocusSessionCount,
            FocusCompletedSeconds = Ledger.FocusCompletedSeconds,
            RhythmExcused = Ledger.RhythmExcused,
            RhythmGoal = Ledger.RhythmGoal,
            RhythmFocusTargetKind = Ledger.RhythmFocusTargetKind,
            RhythmGoalTarget = Ledger.RhythmGoalTarget,
            RhythmDailyLimitMinutes = Ledger.RhythmDailyLimitMinutes,
            RhythmApprovedMinutes = Ledger.RhythmApprovedMinutes,
            RhythmPlannedRest = Ledger.RhythmPlannedRest,
            RhythmMeasurementAvailable = Ledger.RhythmMeasurementAvailable,
            AwarenessUsedSeconds = Ledger.AwarenessUsedSeconds,
            AwarenessHourlyUsedSeconds = new Dictionary<int, long>(Ledger.AwarenessHourlyUsedSeconds),
            Applications = Ledger.AppUsedSeconds
                .Where(item => item.Value > 0)
                .Select(item => new AppUsageRecord
                {
                    RuleId = item.Key,
                    Name = names.GetValueOrDefault(item.Key, Localize("Uygulama", "Application")),
                    UsedSeconds = item.Value
                })
                .OrderByDescending(item => item.UsedSeconds)
                .ToList(),
            ForegroundApplications = Ledger.ForegroundAppUsedSeconds
                .Where(item => item.Value > 0)
                .Select(item => new AwarenessAppUsageRecord
                {
                    ApplicationId = item.Key,
                    Name = Path.GetFileNameWithoutExtension(item.Key),
                    UsedSeconds = item.Value
                })
                .OrderByDescending(item => item.UsedSeconds)
                .ToList()
        };
        RhythmStreakAnalyzer.FinalizeDay(record);

        Ledger.History.RemoveAll(item => item.LocalDay == record.LocalDay);
        Ledger.History.Add(record);
        DateOnly retentionCutoff = retentionReferenceDay.AddDays(-(Math.Clamp(_settings.UsageRetentionDays, 30, 180) - 1));
        if (Ledger.RetainedFromDay is null || retentionCutoff > Ledger.RetainedFromDay)
        {
            Ledger.RetainedFromDay = retentionCutoff;
        }

        DateOnly activeCutoff = Ledger.RetainedFromDay.Value;
        List<DailyUsageRecord> removedRhythmDays = Ledger.History
            .Where(item => item.LocalDay < activeCutoff)
            .OrderBy(item => item.LocalDay)
            .ToList();
        RhythmStreakAnalyzer.AdvanceCheckpoint(Ledger.RhythmCheckpoint, removedRhythmDays);
        Ledger.History = Ledger.History
            .Where(item => item.LocalDay >= activeCutoff)
            .OrderBy(item => item.LocalDay)
            .ToList();
        if (Ledger.History.Count > 180)
        {
            int removeCount = Ledger.History.Count - 180;
            RhythmStreakAnalyzer.AdvanceCheckpoint(Ledger.RhythmCheckpoint, Ledger.History.Take(removeCount));
            Ledger.History.RemoveRange(0, removeCount);
        }
        Ledger.RecentEvents = Ledger.RecentEvents
            .Where(item => DateOnly.FromDateTime(item.OccurredAtUtc.ToLocalTime().DateTime) >= activeCutoff)
            .ToList();
    }

    private void AddEvent(UsageEventKind kind, DateTimeOffset now, int value = 0)
    {
        Ledger.RecentEvents.Add(new UsageEventRecord
        {
            OccurredAtUtc = now.ToUniversalTime(),
            Kind = kind,
            Value = value
        });
        if (Ledger.RecentEvents.Count > 200)
        {
            Ledger.RecentEvents.RemoveRange(0, Ledger.RecentEvents.Count - 200);
        }
    }
}
