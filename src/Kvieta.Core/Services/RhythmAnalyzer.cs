using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public sealed record RhythmSummary(
    int BaselineDays,
    bool IsBaselineReady,
    long BaselineDailyAverageSeconds,
    long CurrentWeekSeconds,
    int CurrentObservedDays,
    long PreviousWeekSeconds,
    int PreviousObservedDays,
    double? WeekChangePercent,
    int PlanAlignedDays,
    long ReclaimedSeconds,
    long GoalDailySeconds,
    bool IsGoalEnabled,
    bool IsGoalMet,
    string? RisingApplication,
    string? FallingApplication,
    int? PeakHour,
    long PeakHourSeconds,
    int WeekdayObservedDays,
    long WeekdayDailyAverageSeconds,
    int WeekendObservedDays,
    long WeekendDailyAverageSeconds,
    double? WeekendDifferencePercent);

public static class RhythmAnalyzer
{
    public static RhythmSummary Analyze(ControlSettings settings, UsageLedger ledger, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);

        List<DailyUsageRecord> records = ledger.History
            .Where(record => record.LocalDay <= today)
            .ToList();
        if (ledger.LocalDay == today)
        {
            records.RemoveAll(record => record.LocalDay == today);
            records.Add(new DailyUsageRecord
            {
                LocalDay = today,
                UsedSeconds = ledger.UsedSeconds,
                BonusMinutes = ledger.BonusMinutes,
                AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
                AwarenessHourlyUsedSeconds = new Dictionary<int, long>(ledger.AwarenessHourlyUsedSeconds),
                ForegroundApplications = ledger.ForegroundAppUsedSeconds.Select(item => new AwarenessAppUsageRecord
                {
                    ApplicationId = item.Key,
                    Name = Path.GetFileNameWithoutExtension(item.Key),
                    UsedSeconds = item.Value
                }).ToList()
            });
        }

        records = records
            .GroupBy(record => record.LocalDay)
            .Select(group => group.OrderByDescending(record => record.AwarenessUsedSeconds).First())
            .OrderBy(record => record.LocalDay)
            .ToList();

        List<DailyUsageRecord> observed = records
            .Where(record => record.AwarenessUsedSeconds > 0)
            .ToList();
        List<DailyUsageRecord> baseline = observed.Take(14).ToList();
        int baselineDays = Math.Min(14, baseline.Count);
        bool baselineReady = baselineDays >= 7;
        long baselineAverage = baselineDays == 0 ? 0 : baseline.Sum(record => record.AwarenessUsedSeconds) / baselineDays;

        DateOnly currentFrom = today.AddDays(-6);
        DateOnly previousFrom = today.AddDays(-13);
        DateOnly previousUntil = today.AddDays(-7);
        List<DailyUsageRecord> current = records.Where(record => record.LocalDay >= currentFrom && record.LocalDay <= today).ToList();
        List<DailyUsageRecord> previous = records.Where(record => record.LocalDay >= previousFrom && record.LocalDay <= previousUntil).ToList();
        int currentObservedDays = current.Count(record => record.AwarenessUsedSeconds > 0);
        int previousObservedDays = previous.Count(record => record.AwarenessUsedSeconds > 0);
        long currentSeconds = current.Sum(record => record.AwarenessUsedSeconds);
        long previousSeconds = previous.Sum(record => record.AwarenessUsedSeconds);
        double? changePercent = previousSeconds > 0 && previousObservedDays > 0 && currentObservedDays > 0
            ? ((currentSeconds / (double)currentObservedDays) - (previousSeconds / (double)previousObservedDays)) /
              (previousSeconds / (double)previousObservedDays) * 100
            : null;

        int alignedDays = current.Count(record =>
        {
            if (record.UsedSeconds <= 0)
            {
                return false;
            }

            DaySchedule? schedule = settings.Schedule.FirstOrDefault(day => day.Day == record.LocalDay.DayOfWeek);
            long plannedSeconds = ((schedule is { IsEnabled: true } ? schedule.DailyLimitMinutes : 0) + record.BonusMinutes) * 60L;
            return record.UsedSeconds <= plannedSeconds;
        });

        long reclaimed = baselineReady && currentObservedDays > 0
            ? Math.Max(0, (baselineAverage * currentObservedDays) - currentSeconds)
            : 0;
        bool goalEnabled = settings.WeeklyReductionGoalPercent > 0 && baselineReady;
        long goalDailySeconds = goalEnabled
            ? (long)Math.Round(baselineAverage * (1 - settings.WeeklyReductionGoalPercent / 100d))
            : 0;
        long currentAverage = currentObservedDays == 0 ? 0 : currentSeconds / currentObservedDays;
        KeyValuePair<int, long>? peak = current
            .SelectMany(record => record.AwarenessHourlyUsedSeconds)
            .GroupBy(item => item.Key)
            .Select(group => new KeyValuePair<int, long>(group.Key, group.Sum(item => item.Value)))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .Cast<KeyValuePair<int, long>?>()
            .FirstOrDefault();
        List<DailyUsageRecord> comparisonWindow = records
            .Where(record => record.LocalDay >= today.AddDays(-27) && record.LocalDay <= today && record.AwarenessUsedSeconds > 0)
            .ToList();
        List<DailyUsageRecord> weekdays = comparisonWindow
            .Where(record => record.LocalDay.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .ToList();
        List<DailyUsageRecord> weekends = comparisonWindow
            .Where(record => record.LocalDay.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .ToList();
        long weekdayAverage = weekdays.Count == 0 ? 0 : weekdays.Sum(record => record.AwarenessUsedSeconds) / weekdays.Count;
        long weekendAverage = weekends.Count == 0 ? 0 : weekends.Sum(record => record.AwarenessUsedSeconds) / weekends.Count;
        double? weekendDifference = weekdays.Count >= 2 && weekends.Count >= 1 && weekdayAverage > 0
            ? (weekendAverage - weekdayAverage) / (double)weekdayAverage * 100
            : null;

        return new RhythmSummary(
            baselineDays,
            baselineReady,
            baselineAverage,
            currentSeconds,
            currentObservedDays,
            previousSeconds,
            previousObservedDays,
            changePercent,
            alignedDays,
            reclaimed,
            goalDailySeconds,
            goalEnabled,
            goalEnabled && currentObservedDays > 0 && currentAverage <= goalDailySeconds,
            FindTrendApplication(current, previous, rising: true),
            FindTrendApplication(current, previous, rising: false),
            peak?.Key,
            peak?.Value ?? 0,
            weekdays.Count,
            weekdayAverage,
            weekends.Count,
            weekendAverage,
            weekendDifference);
    }

    private static string? FindTrendApplication(
        IEnumerable<DailyUsageRecord> current,
        IEnumerable<DailyUsageRecord> previous,
        bool rising)
    {
        Dictionary<string, long> currentTotals = SumApplications(current);
        Dictionary<string, long> previousTotals = SumApplications(previous);
        return currentTotals.Keys
            .Concat(previousTotals.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new { Name = name, Delta = currentTotals.GetValueOrDefault(name) - previousTotals.GetValueOrDefault(name) })
            .Where(item => rising ? item.Delta > 0 : item.Delta < 0)
            .OrderByDescending(item => rising ? item.Delta : -item.Delta)
            .Select(item => item.Name)
            .FirstOrDefault();
    }

    private static Dictionary<string, long> SumApplications(IEnumerable<DailyUsageRecord> records) => records
        .SelectMany(record => record.ForegroundApplications)
        .GroupBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Sum(record => record.UsedSeconds), StringComparer.OrdinalIgnoreCase);
}
