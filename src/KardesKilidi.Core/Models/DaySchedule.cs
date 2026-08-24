namespace KardesKilidi.Core.Models;

public sealed class DaySchedule
{
    public DayOfWeek Day { get; set; }
    public bool IsEnabled { get; set; } = true;
    public TimeOnly AllowedFrom { get; set; } = new(9, 0);
    public TimeOnly AllowedUntil { get; set; } = new(21, 0);
    public int DailyLimitMinutes { get; set; } = 180;
}
