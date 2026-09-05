using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public sealed record RhythmStreakSummary(
    RhythmGoalKind Goal,
    int CurrentStreak,
    int BestStreak,
    int Protectors,
    int SuccessfulDays,
    RhythmDayOutcome TodayOutcome,
    int? ReachedMilestone);

public sealed record RhythmDayResult(
    DateOnly Day,
    RhythmGoalKind? Goal,
    RhythmDayOutcome Outcome,
    long Progress,
    long Target,
    FocusRhythmTargetKind? FocusTargetKind);

public static class RhythmStreakAnalyzer
{
    private static readonly int[] Milestones = [3, 7, 14, 30, 50, 100];
    public const int MinimumCountedFocusSessionMinutes = 5;

    public static RhythmStreakSummary Analyze(ControlSettings settings, UsageLedger ledger, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);

        RhythmGoalKind goal = ledger.LocalDay == today
            ? ledger.RhythmGoal ?? ResolveGoal(settings)
            : ResolveGoal(settings);
        Dictionary<DateOnly, DailyUsageRecord> records = ledger.History
            .Where(item => item.LocalDay <= today)
            .GroupBy(item => item.LocalDay)
            .ToDictionary(group => group.Key, group => group.Last());
        if (ledger.LocalDay == today)
        {
            records[today] = new DailyUsageRecord
            {
                LocalDay = today,
                UsedSeconds = ledger.UsedSeconds,
                BonusMinutes = ledger.BonusMinutes,
                LimitReachedCount = ledger.LimitReachedCount,
                AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
                SummaryReviewed = ledger.SummaryReviewed,
                FocusSessionCount = ledger.FocusSessionCount,
                FocusCompletedSeconds = ledger.FocusCompletedSeconds,
                RhythmExcused = ledger.RhythmExcused,
                RhythmGoal = ledger.RhythmGoal,
                RhythmFocusTargetKind = ledger.RhythmFocusTargetKind,
                RhythmGoalTarget = ledger.RhythmGoalTarget,
                RhythmDailyLimitMinutes = ledger.RhythmDailyLimitMinutes,
                RhythmApprovedMinutes = ledger.RhythmApprovedMinutes,
                RhythmPlannedRest = ledger.RhythmPlannedRest,
                RhythmMeasurementAvailable = ledger.RhythmMeasurementAvailable
            };
        }

        RhythmCheckpoint state = CopyCheckpoint(ledger.RhythmCheckpoint);
        DateOnly first = state.ProcessedThroughDay?.AddDays(1)
            ?? records.Keys.DefaultIfEmpty(today).Min();
        int current = state.CurrentStreak;
        int best = state.BestStreak;
        int protectors = state.Protectors;
        int successfulDays = state.SuccessfulDays;
        RhythmDayOutcome todayOutcome = RhythmDayOutcome.Pending;

        for (DateOnly day = first; day <= today; day = day.AddDays(1))
        {
            records.TryGetValue(day, out DailyUsageRecord? record);
            RhythmDayOutcome outcome = day == today
                ? EvaluateCurrentDay(settings, goal, record)
                : record?.RhythmOutcome ?? RhythmDayOutcome.Unobserved;
            ApplyOutcome(outcome, ref current, ref best, ref protectors, ref successfulDays, out outcome);
            if (day == today) todayOutcome = outcome;
        }

        int? milestone = Milestones.Where(value => current >= value).Cast<int?>().LastOrDefault();
        return new RhythmStreakSummary(goal, current, best, protectors, successfulDays, todayOutcome, milestone);
    }

    public static IReadOnlyList<RhythmDayResult> BuildRecentDays(
        ControlSettings settings,
        UsageLedger ledger,
        DateOnly today,
        int count = 7)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);
        count = Math.Clamp(count, 1, 31);
        DateOnly firstVisible = today.AddDays(-(count - 1));
        Dictionary<DateOnly, DailyUsageRecord> records = ledger.History
            .Where(item => item.LocalDay <= today)
            .GroupBy(item => item.LocalDay)
            .ToDictionary(group => group.Key, group => group.Last());
        if (ledger.LocalDay == today)
        {
            records[today] = CreateCurrentRecord(ledger, today);
        }

        RhythmCheckpoint state = CopyCheckpoint(ledger.RhythmCheckpoint);
        DateOnly firstReplay = state.ProcessedThroughDay?.AddDays(1)
            ?? records.Keys.DefaultIfEmpty(firstVisible).Min();
        firstReplay = firstReplay > firstVisible ? firstVisible : firstReplay;
        int current = state.CurrentStreak;
        int best = state.BestStreak;
        int protectors = state.Protectors;
        int successfulDays = state.SuccessfulDays;
        List<RhythmDayResult> results = [];
        for (DateOnly day = firstReplay; day <= today; day = day.AddDays(1))
        {
            records.TryGetValue(day, out DailyUsageRecord? record);
            RhythmGoalKind? goal = record?.RhythmGoal;
            RhythmDayOutcome outcome = day == today
                ? EvaluateCurrentDay(settings, goal ?? ResolveGoal(settings), record)
                : record?.RhythmOutcome ?? RhythmDayOutcome.Unobserved;
            ApplyOutcome(outcome, ref current, ref best, ref protectors, ref successfulDays, out outcome);
            if (day >= firstVisible)
            {
                (long progress, long target, FocusRhythmTargetKind? kind) = GetProgress(settings, record, goal ?? ResolveGoal(settings));
                results.Add(new RhythmDayResult(day, goal, outcome, progress, target, kind));
            }
        }
        return results;
    }

    public static void CaptureCurrentGoal(ControlSettings settings, UsageLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);
        if (ledger.RhythmGoal is not null) return;

        ledger.RhythmGoal = ResolveGoal(settings);
        ledger.RhythmFocusTargetKind = ledger.RhythmGoal == RhythmGoalKind.CompleteFocus
            ? settings.FocusRhythmTargetKind
            : null;
        ledger.RhythmGoalTarget = ledger.RhythmGoal switch
        {
            RhythmGoalKind.CompleteFocus => settings.FocusRhythmTargetValue,
            RhythmGoalKind.ReviewSummary => 1,
            _ => 0
        };
        DaySchedule? schedule = settings.Schedule.FirstOrDefault(item => item.Day == ledger.LocalDay.DayOfWeek);
        ledger.RhythmPlannedRest = ledger.RhythmGoal == RhythmGoalKind.KeepBalance && schedule is { IsEnabled: false };
        ledger.RhythmDailyLimitMinutes = ledger.RhythmGoal == RhythmGoalKind.KeepBalance
            ? schedule is { IsEnabled: true } ? schedule.DailyLimitMinutes : settings.DefaultDailyLimitMinutes
            : null;
        ledger.RhythmMeasurementAvailable = settings.Mode != UsageMode.Insights || settings.AwarenessTrackingEnabled;
    }

    public static void FinalizeDay(DailyUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.RhythmOutcome is not null) return;
        record.RhythmOutcome = EvaluateFinalizedDay(record);
    }

    public static void AdvanceCheckpoint(RhythmCheckpoint checkpoint, IEnumerable<DailyUsageRecord> records)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        foreach (DailyUsageRecord record in records.OrderBy(item => item.LocalDay))
        {
            if (checkpoint.ProcessedThroughDay is { } processed && record.LocalDay <= processed) continue;
            RhythmDayOutcome outcome = record.RhythmOutcome ?? RhythmDayOutcome.Unobserved;
            int current = checkpoint.CurrentStreak;
            int best = checkpoint.BestStreak;
            int protectors = checkpoint.Protectors;
            int successfulDays = checkpoint.SuccessfulDays;
            ApplyOutcome(outcome, ref current, ref best, ref protectors, ref successfulDays, out _);
            checkpoint.CurrentStreak = current;
            checkpoint.BestStreak = best;
            checkpoint.Protectors = protectors;
            checkpoint.SuccessfulDays = successfulDays;
            checkpoint.ProcessedThroughDay = record.LocalDay;
        }
    }

    private static RhythmGoalKind ResolveGoal(ControlSettings settings) => settings.Mode switch
    {
        UsageMode.Insights => RhythmGoalKind.ReviewSummary,
        UsageMode.Personal when settings.PersonalProtectionLevel == PersonalProtectionLevel.Flexible => RhythmGoalKind.CompleteFocus,
        _ => RhythmGoalKind.KeepBalance
    };

    private static RhythmCheckpoint CopyCheckpoint(RhythmCheckpoint? checkpoint) => checkpoint is null
        ? new RhythmCheckpoint()
        : new RhythmCheckpoint
        {
            ProcessedThroughDay = checkpoint.ProcessedThroughDay,
            CurrentStreak = checkpoint.CurrentStreak,
            BestStreak = checkpoint.BestStreak,
            Protectors = checkpoint.Protectors,
            SuccessfulDays = checkpoint.SuccessfulDays
        };

    private static void ApplyOutcome(
        RhythmDayOutcome requested,
        ref int current,
        ref int best,
        ref int protectors,
        ref int successfulDays,
        out RhythmDayOutcome applied)
    {
        applied = requested;
        switch (requested)
        {
            case RhythmDayOutcome.Success:
                current++;
                successfulDays++;
                best = Math.Max(best, current);
                if (successfulDays % 7 == 0) protectors = Math.Min(2, protectors + 1);
                break;
            case RhythmDayOutcome.Missed when protectors > 0:
                protectors--;
                applied = RhythmDayOutcome.Protected;
                break;
            case RhythmDayOutcome.Missed:
                current = 0;
                break;
        }
    }

    private static RhythmDayOutcome EvaluateFinalizedDay(DailyUsageRecord record)
    {
        if (record.RhythmExcused) return RhythmDayOutcome.Excused;
        if (record.RhythmGoal is null || !record.RhythmMeasurementAvailable) return RhythmDayOutcome.Unobserved;
        if (record.RhythmPlannedRest) return RhythmDayOutcome.Rest;

        return record.RhythmGoal switch
        {
            RhythmGoalKind.ReviewSummary => record.SummaryReviewed
                ? RhythmDayOutcome.Success
                : record.AwarenessUsedSeconds <= 0 ? RhythmDayOutcome.Rest : RhythmDayOutcome.Missed,
            RhythmGoalKind.CompleteFocus when record.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes =>
                record.FocusCompletedSeconds >= Math.Max(1, record.RhythmGoalTarget) * 60L
                    ? RhythmDayOutcome.Success
                    : record.UsedSeconds <= 0 ? RhythmDayOutcome.Rest : RhythmDayOutcome.Missed,
            RhythmGoalKind.CompleteFocus => record.FocusSessionCount >= Math.Max(1, record.RhythmGoalTarget) &&
                record.FocusCompletedSeconds >= Math.Max(1, record.RhythmGoalTarget) * MinimumCountedFocusSessionMinutes * 60L
                ? RhythmDayOutcome.Success
                : record.UsedSeconds <= 0 ? RhythmDayOutcome.Rest : RhythmDayOutcome.Missed,
            RhythmGoalKind.KeepBalance when record.LimitReachedCount > 0 => RhythmDayOutcome.Missed,
            RhythmGoalKind.KeepBalance when record.UsedSeconds <= 0 => RhythmDayOutcome.Success,
            RhythmGoalKind.KeepBalance when record.RhythmDailyLimitMinutes is { } limit =>
                record.UsedSeconds <= (limit + record.BonusMinutes + record.RhythmApprovedMinutes) * 60L
                    ? RhythmDayOutcome.Success
                    : RhythmDayOutcome.Missed,
            _ => RhythmDayOutcome.Unobserved
        };
    }

    private static RhythmDayOutcome EvaluateCurrentDay(
        ControlSettings settings,
        RhythmGoalKind goal,
        DailyUsageRecord? record)
    {
        if (record?.RhythmExcused == true) return RhythmDayOutcome.Excused;
        if (record is { RhythmGoal: not null, RhythmMeasurementAvailable: false })
        {
            return RhythmDayOutcome.Unobserved;
        }
        if (goal == RhythmGoalKind.KeepBalance &&
            (record?.RhythmGoal is not null
                ? record.RhythmPlannedRest
                : settings.Schedule.FirstOrDefault(item => item.Day == record?.LocalDay.DayOfWeek) is { IsEnabled: false }))
        {
            return RhythmDayOutcome.Rest;
        }

        return goal switch
        {
            RhythmGoalKind.ReviewSummary => record?.SummaryReviewed == true
                ? RhythmDayOutcome.Success
                : RhythmDayOutcome.Pending,
            RhythmGoalKind.CompleteFocus when record?.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes =>
                record.FocusCompletedSeconds >= Math.Max(1, record.RhythmGoalTarget) * 60L
                    ? RhythmDayOutcome.Success
                    : RhythmDayOutcome.Pending,
            RhythmGoalKind.CompleteFocus => record is not null &&
                record.FocusSessionCount >= Math.Max(1, record.RhythmGoalTarget) &&
                record.FocusCompletedSeconds >= Math.Max(1, record.RhythmGoalTarget) * MinimumCountedFocusSessionMinutes * 60L
                ? RhythmDayOutcome.Success
                : RhythmDayOutcome.Pending,
            RhythmGoalKind.KeepBalance => RhythmDayOutcome.Pending,
            _ => RhythmDayOutcome.Pending
        };
    }

    private static DailyUsageRecord CreateCurrentRecord(UsageLedger ledger, DateOnly today) => new()
    {
        LocalDay = today,
        UsedSeconds = ledger.UsedSeconds,
        BonusMinutes = ledger.BonusMinutes,
        LimitReachedCount = ledger.LimitReachedCount,
        AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
        SummaryReviewed = ledger.SummaryReviewed,
        FocusSessionCount = ledger.FocusSessionCount,
        FocusCompletedSeconds = ledger.FocusCompletedSeconds,
        RhythmExcused = ledger.RhythmExcused,
        RhythmGoal = ledger.RhythmGoal,
        RhythmFocusTargetKind = ledger.RhythmFocusTargetKind,
        RhythmGoalTarget = ledger.RhythmGoalTarget,
        RhythmDailyLimitMinutes = ledger.RhythmDailyLimitMinutes,
        RhythmApprovedMinutes = ledger.RhythmApprovedMinutes,
        RhythmPlannedRest = ledger.RhythmPlannedRest,
        RhythmMeasurementAvailable = ledger.RhythmMeasurementAvailable
    };

    private static (long Progress, long Target, FocusRhythmTargetKind? Kind) GetProgress(
        ControlSettings settings,
        DailyUsageRecord? record,
        RhythmGoalKind goal) => goal switch
        {
            RhythmGoalKind.ReviewSummary => (record?.SummaryReviewed == true ? 1 : 0, 1, null),
            RhythmGoalKind.CompleteFocus when record?.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes =>
                (record.FocusCompletedSeconds / 60, Math.Max(1, record.RhythmGoalTarget), FocusRhythmTargetKind.Minutes),
            RhythmGoalKind.CompleteFocus =>
                (record?.FocusSessionCount ?? 0, Math.Max(1, record?.RhythmGoalTarget ?? settings.FocusRhythmTargetValue), FocusRhythmTargetKind.Sessions),
            RhythmGoalKind.KeepBalance =>
                ((record?.UsedSeconds ?? 0) / 60, Math.Max(0, (record?.RhythmDailyLimitMinutes ?? settings.DefaultDailyLimitMinutes) + (record?.BonusMinutes ?? 0) + (record?.RhythmApprovedMinutes ?? 0)), null),
            _ => (0, 0, null)
        };
}
