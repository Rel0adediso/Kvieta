using Otium.Core.Models;
using System.Globalization;

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

        DaySchedule? schedule = settings.Schedule.FirstOrDefault(item => item.Day == now.DayOfWeek);
        TimeOnly current = TimeOnly.FromDateTime(now.LocalDateTime);
        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        List<TemporaryAllowance> todaysAllowances = settings.TemporaryAllowances
            .Where(item => item.Date == today)
            .ToList();
        List<TemporaryAllowance> activeAllowances = todaysAllowances
            .Where(item => IsInsideWindow(current, item.AllowedFrom, item.AllowedUntil))
            .ToList();
        bool insideRegularWindow = schedule is { IsEnabled: true } &&
            IsInsideWindow(current, schedule.AllowedFrom, schedule.AllowedUntil);
        int dailyLimit = Math.Clamp(
            (schedule is { IsEnabled: true } ? schedule.DailyLimitMinutes : 0) +
            todaysAllowances.Sum(item => item.BonusMinutes),
            0,
            1440);

        if (!insideRegularWindow && activeAllowances.Count == 0)
        {
            string reason = schedule is { IsEnabled: true }
                ? $"{Localize("İzin verilen saatler", "Allowed hours")}: {schedule.AllowedFrom:HH\\:mm}–{schedule.AllowedUntil:HH\\:mm}"
                : Localize("Bu gün için kullanım kapalı.", "Usage is disabled for this day.");
            return new ScheduleStatus(
                false,
                dailyLimit,
                reason,
                null);
        }

        List<DateTimeOffset> endings = activeAllowances
            .Select(item => BuildAllowedUntil(now, item.AllowedUntil))
            .ToList();
        if (insideRegularWindow && schedule is not null)
        {
            endings.Add(BuildAllowedUntil(now, schedule.AllowedUntil));
        }

        return new ScheduleStatus(
            true,
            dailyLimit,
            activeAllowances.Count > 0
                ? Localize("Geçici izin etkin.", "Temporary allowance is active.")
                : Localize("Kullanıma izin veriliyor.", "Usage is allowed."),
            endings.Max());
    }

    private static string Localize(string turkish, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? english : turkish;

    private static bool IsInsideWindow(TimeOnly current, TimeOnly from, TimeOnly until)
    {
        if (from == until)
        {
            return true;
        }

        return from < until
            ? current >= from && current < until
            : current >= from || current < until;
    }

    private static DateTimeOffset BuildAllowedUntil(DateTimeOffset now, TimeOnly until)
    {
        DateTime localDate = now.LocalDateTime.Date;
        DateTime candidate = localDate.Add(until.ToTimeSpan());
        if (candidate <= now.LocalDateTime)
        {
            candidate = candidate.AddDays(1);
        }

        return new DateTimeOffset(candidate, now.Offset);
    }
}
