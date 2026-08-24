using Otium.Core.Models;
namespace Otium.Core.Services;

public sealed record ScheduleStatus(
    bool IsAllowed,
    int DailyLimitMinutes,
    string Reason,
    DateTimeOffset? AllowedUntil);

public static class ScheduleEvaluator
{
    public static ScheduleStatus Evaluate(ControlSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        DateOnly yesterday = today.AddDays(-1);
        DaySchedule? todaysSchedule = settings.Schedule.FirstOrDefault(item => item.Day == now.DayOfWeek);
        DaySchedule? yesterdaysSchedule = settings.Schedule.FirstOrDefault(item => item.Day == now.AddDays(-1).DayOfWeek);

        (DaySchedule Schedule, DateOnly AnchorDate)? activeRegularWindow =
            FindActiveRegularWindow(now, today, todaysSchedule) ??
            FindActiveRegularWindow(now, yesterday, yesterdaysSchedule);

        List<(TemporaryAllowance Allowance, DateOnly AnchorDate)> activeAllowances = settings.TemporaryAllowances
            .Where(item => item.Date == today || item.Date == yesterday)
            .Select(item => (Allowance: item, AnchorDate: item.Date))
            .Where(item => IsInsideWindow(
                now,
                item.AnchorDate,
                item.Allowance.AllowedFrom,
                item.Allowance.AllowedUntil))
            .ToList();

        int baseLimit = activeRegularWindow?.Schedule.DailyLimitMinutes ??
            (todaysSchedule is { IsEnabled: true } ? todaysSchedule.DailyLimitMinutes : 0);
        int allowanceMinutes = settings.TemporaryAllowances
            .Where(item => item.Date == today)
            .Sum(item => item.BonusMinutes) +
            activeAllowances
                .Where(item => item.AnchorDate == yesterday)
                .Sum(item => item.Allowance.BonusMinutes);
        int dailyLimit = Math.Clamp(baseLimit + allowanceMinutes, 0, 1440);

        if (activeRegularWindow is null && activeAllowances.Count == 0)
        {
            string reason = todaysSchedule is { IsEnabled: true }
                ? $"{Localize(settings.Language, "İzin verilen saatler", "Allowed hours")}: {todaysSchedule.AllowedFrom:HH:mm}–{todaysSchedule.AllowedUntil:HH:mm}"
                : Localize(settings.Language, "Bu gün için kullanım kapalı.", "Usage is disabled for this day.");
            return new ScheduleStatus(false, dailyLimit, reason, null);
        }

        List<DateTimeOffset> endings = activeAllowances
            .Select(item => BuildAllowedUntil(
                now,
                item.AnchorDate,
                item.Allowance.AllowedFrom,
                item.Allowance.AllowedUntil))
            .ToList();
        if (activeRegularWindow is { } regularWindow)
        {
            endings.Add(BuildAllowedUntil(
                now,
                regularWindow.AnchorDate,
                regularWindow.Schedule.AllowedFrom,
                regularWindow.Schedule.AllowedUntil));
        }

        return new ScheduleStatus(
            true,
            dailyLimit,
            activeAllowances.Count > 0
                ? Localize(settings.Language, "Geçici izin etkin.", "Temporary allowance is active.")
                : Localize(settings.Language, "Kullanıma izin veriliyor.", "Usage is allowed."),
            endings.Max());
    }

    private static string Localize(LanguagePreference language, string turkish, string english) =>
        language == LanguagePreference.English ? english : turkish;

    private static (DaySchedule Schedule, DateOnly AnchorDate)? FindActiveRegularWindow(
        DateTimeOffset now,
        DateOnly anchorDate,
        DaySchedule? schedule)
    {
        return schedule is { IsEnabled: true } &&
            IsInsideWindow(now, anchorDate, schedule.AllowedFrom, schedule.AllowedUntil)
                ? (schedule, anchorDate)
                : null;
    }

    private static bool IsInsideWindow(
        DateTimeOffset now,
        DateOnly anchorDate,
        TimeOnly from,
        TimeOnly until)
    {
        DateTime start = anchorDate.ToDateTime(from);
        DateTime end = anchorDate.ToDateTime(until);
        if (until <= from)
        {
            end = end.AddDays(1);
        }

        return now.LocalDateTime >= start && now.LocalDateTime < end;
    }

    private static DateTimeOffset BuildAllowedUntil(
        DateTimeOffset now,
        DateOnly anchorDate,
        TimeOnly from,
        TimeOnly until)
    {
        DateTime candidate = anchorDate.ToDateTime(until);
        if (until <= from)
        {
            candidate = candidate.AddDays(1);
        }

        return new DateTimeOffset(candidate, now.Offset);
    }
}
