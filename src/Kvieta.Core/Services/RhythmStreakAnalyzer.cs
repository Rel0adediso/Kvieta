using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public enum RhythmGoalKind
{
    ReviewSummary,
    CompleteFocus,
    KeepBalance
}

public enum RhythmDayOutcome
{
    Pending,
    Success,
    Rest,
    Excused,
    Protected,
    Missed
}

public sealed record RhythmStreakSummary(
    RhythmGoalKind Goal,
    int CurrentStreak,
    int BestStreak,
    int Protectors,
    int SuccessfulDays,
    RhythmDayOutcome TodayOutcome,
    int? ReachedMilestone);

public static class RhythmStreakAnalyzer
{
    private static readonly int[] Milestones = [3, 7, 14, 30, 50, 100];

    public static RhythmStreakSummary Analyze(ControlSettings settings, UsageLedger ledger, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);

        RhythmGoalKind goal = settings.Mode switch
        {
            UsageMode.Insights => RhythmGoalKind.ReviewSummary,
            UsageMode.Personal when settings.PersonalProtectionLevel == PersonalProtectionLevel.Flexible => RhythmGoalKind.CompleteFocus,
            _ => RhythmGoalKind.KeepBalance
        };
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
                AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
                SummaryReviewed = ledger.SummaryReviewed,
                FocusSessionCount = ledger.FocusSessionCount,
                FocusCompletedSeconds = ledger.FocusCompletedSeconds,
                RhythmExcused = ledger.RhythmExcused
            };
        }

        DateOnly first = records.Keys.DefaultIfEmpty(today).Min();
        if (first < today.AddDays(-179)) first = today.AddDays(-179);
        int current = 0;
        int best = 0;
        int protectors = 0;
        int successfulDays = 0;
        RhythmDayOutcome todayOutcome = RhythmDayOutcome.Pending;

        for (DateOnly day = first; day <= today; day = day.AddDays(1))
        {
            records.TryGetValue(day, out DailyUsageRecord? record);
            RhythmDayOutcome outcome = EvaluateDay(settings, goal, day, today, record);
            switch (outcome)
            {
                case RhythmDayOutcome.Success:
                    current++;
                    successfulDays++;
                    best = Math.Max(best, current);
                    if (successfulDays % 7 == 0) protectors = Math.Min(2, protectors + 1);
                    break;
                case RhythmDayOutcome.Missed when protectors > 0:
                    protectors--;
                    outcome = RhythmDayOutcome.Protected;
                    break;
                case RhythmDayOutcome.Missed:
                    current = 0;
                    break;
            }
            if (day == today) todayOutcome = outcome;
        }

        int? milestone = Milestones.Where(value => current >= value).Cast<int?>().LastOrDefault();
        return new RhythmStreakSummary(goal, current, best, protectors, successfulDays, todayOutcome, milestone);
    }

    private static RhythmDayOutcome EvaluateDay(
        ControlSettings settings,
        RhythmGoalKind goal,
        DateOnly day,
        DateOnly today,
        DailyUsageRecord? record)
    {
        DaySchedule? schedule = settings.Schedule.FirstOrDefault(item => item.Day == day.DayOfWeek);
        if (record?.RhythmExcused == true) return RhythmDayOutcome.Excused;
        if (schedule is { IsEnabled: false }) return RhythmDayOutcome.Rest;

        return goal switch
        {
            RhythmGoalKind.ReviewSummary => record?.SummaryReviewed == true
                ? RhythmDayOutcome.Success
                : record is null || record.AwarenessUsedSeconds <= 0 ? RhythmDayOutcome.Rest : RhythmDayOutcome.Missed,
            RhythmGoalKind.CompleteFocus => record is { FocusSessionCount: > 0 }
                ? RhythmDayOutcome.Success
                : record is null || record.UsedSeconds <= 0 ? RhythmDayOutcome.Rest : RhythmDayOutcome.Missed,
            RhythmGoalKind.KeepBalance when day == today => RhythmDayOutcome.Pending,
            RhythmGoalKind.KeepBalance when record is null || record.UsedSeconds <= 0 => RhythmDayOutcome.Success,
            RhythmGoalKind.KeepBalance => record.UsedSeconds <=
                ((schedule is { IsEnabled: true } ? schedule.DailyLimitMinutes : settings.DefaultDailyLimitMinutes) + record.BonusMinutes) * 60L
                    ? RhythmDayOutcome.Success
                    : RhythmDayOutcome.Missed,
            _ => RhythmDayOutcome.Pending
        };
    }
}
