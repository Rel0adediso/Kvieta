using System.Globalization;
using Otium.Core.Models;
using Otium.App.Services;

namespace Otium.App.ViewModels;

public sealed class DayScheduleRow : ObservableObject
{
    private bool _isEnabled;
    private string _allowedFrom;
    private string _allowedUntil;
    private int _dailyLimitMinutes;

    public DayScheduleRow(DaySchedule schedule)
    {
        Day = schedule.Day;
        _isEnabled = schedule.IsEnabled;
        _allowedFrom = schedule.AllowedFrom.ToString("HH:mm", CultureInfo.InvariantCulture);
        _allowedUntil = schedule.AllowedUntil.ToString("HH:mm", CultureInfo.InvariantCulture);
        _dailyLimitMinutes = schedule.DailyLimitMinutes;
    }

    public DayOfWeek Day { get; }

    public string DayName => Day switch
    {
        DayOfWeek.Monday => LocalizationService.Get("Monday"),
        DayOfWeek.Tuesday => LocalizationService.Get("Tuesday"),
        DayOfWeek.Wednesday => LocalizationService.Get("Wednesday"),
        DayOfWeek.Thursday => LocalizationService.Get("Thursday"),
        DayOfWeek.Friday => LocalizationService.Get("Friday"),
        DayOfWeek.Saturday => LocalizationService.Get("Saturday"),
        DayOfWeek.Sunday => LocalizationService.Get("Sunday"),
        _ => Day.ToString()
    };

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string AllowedFrom
    {
        get => _allowedFrom;
        set => SetProperty(ref _allowedFrom, value);
    }

    public string AllowedUntil
    {
        get => _allowedUntil;
        set => SetProperty(ref _allowedUntil, value);
    }

    public int DailyLimitMinutes
    {
        get => _dailyLimitMinutes;
        set => SetProperty(ref _dailyLimitMinutes, Math.Clamp(value, 0, 1440));
    }

    public bool TryBuild(out DaySchedule schedule)
    {
        bool fromValid = TimeOnly.TryParseExact(AllowedFrom, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly from);
        bool untilValid = TimeOnly.TryParseExact(AllowedUntil, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly until);

        schedule = new DaySchedule
        {
            Day = Day,
            IsEnabled = IsEnabled,
            AllowedFrom = from,
            AllowedUntil = until,
            DailyLimitMinutes = DailyLimitMinutes
        };

        return fromValid && untilValid;
    }
}
