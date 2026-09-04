using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class SettingsPolicyComparer
{
    public static bool HasRelaxation(ControlSettings current, ControlSettings desired)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(desired);

        if (desired.PersonalChangeDelayMinutes < current.PersonalChangeDelayMinutes)
        {
            return true;
        }

        if (current.StartWithWindows && !desired.StartWithWindows)
        {
            return true;
        }

        if (current.StrictPersonalMode && !desired.StrictPersonalMode)
        {
            return true;
        }

        if ((int)desired.PersonalProtectionLevel < (int)current.PersonalProtectionLevel)
        {
            return true;
        }

        Dictionary<DayOfWeek, DaySchedule> currentDays = current.Schedule.ToDictionary(day => day.Day);
        foreach (DaySchedule desiredDay in desired.Schedule)
        {
            if (!currentDays.TryGetValue(desiredDay.Day, out DaySchedule? currentDay))
            {
                if (desiredDay.IsEnabled)
                {
                    return true;
                }

                continue;
            }

            if (!currentDay.IsEnabled && desiredDay.IsEnabled)
            {
                return true;
            }

            if (!desiredDay.IsEnabled)
            {
                continue;
            }

            if (desiredDay.DailyLimitMinutes > currentDay.DailyLimitMinutes)
            {
                return true;
            }

            if (currentDay.IsEnabled && AddsAllowedMinutes(currentDay, desiredDay))
            {
                return true;
            }
        }

        Dictionary<Guid, TemporaryAllowance> currentAllowances = current.TemporaryAllowances
            .ToDictionary(item => item.Id);
        foreach (TemporaryAllowance desiredAllowance in desired.TemporaryAllowances)
        {
            if (!currentAllowances.TryGetValue(desiredAllowance.Id, out TemporaryAllowance? existing) ||
                desiredAllowance.Date != existing.Date ||
                AddsAllowedMinutes(existing, desiredAllowance) ||
                desiredAllowance.BonusMinutes > existing.BonusMinutes)
            {
                return true;
            }
        }

        Dictionary<string, AppRule> currentRules = current.AppRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ExecutablePath))
            .ToDictionary(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AppRule> desiredRules = desired.AppRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ExecutablePath))
            .ToDictionary(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase);

        foreach ((string path, AppRule currentRule) in currentRules)
        {
            if (!desiredRules.TryGetValue(path, out AppRule? desiredRule))
            {
                if (currentRule.Mode != AppRuleMode.Unlimited)
                {
                    return true;
                }

                continue;
            }

            if (currentRule.Mode != desiredRule.Mode &&
                currentRule.Mode != AppRuleMode.Unlimited &&
                desiredRule.Mode != AppRuleMode.Blocked)
            {
                return true;
            }

            if (currentRule.Mode == AppRuleMode.Limited &&
                desiredRule.Mode == AppRuleMode.Limited &&
                desiredRule.DailyLimitMinutes > currentRule.DailyLimitMinutes)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AddsAllowedMinutes(DaySchedule current, DaySchedule desired)
    {
        for (int minute = 0; minute < 24 * 60; minute++)
        {
            TimeOnly time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
            if (Contains(desired, time) && !Contains(current, time))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(DaySchedule schedule, TimeOnly time)
    {
        if (!schedule.IsEnabled)
        {
            return false;
        }

        if (schedule.AllowedFrom == schedule.AllowedUntil)
        {
            return true;
        }

        return schedule.AllowedFrom < schedule.AllowedUntil
            ? time >= schedule.AllowedFrom && time < schedule.AllowedUntil
            : time >= schedule.AllowedFrom || time < schedule.AllowedUntil;
    }

    private static bool AddsAllowedMinutes(TemporaryAllowance current, TemporaryAllowance desired)
    {
        for (int minute = 0; minute < 24 * 60; minute++)
        {
            TimeOnly time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
            if (Contains(desired.AllowedFrom, desired.AllowedUntil, time) &&
                !Contains(current.AllowedFrom, current.AllowedUntil, time))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(TimeOnly from, TimeOnly until, TimeOnly time)
    {
        if (from == until)
        {
            return true;
        }

        return from < until ? time >= from && time < until : time >= from || time < until;
    }
}
