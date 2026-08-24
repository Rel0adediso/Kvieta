using Otium.App.Services;
using Otium.Core.Models;

namespace Otium.App.ViewModels;

public sealed class UsageHistoryDayRow
{
    public required DateOnly Day { get; init; }
    public long UsedSeconds { get; init; }
    public double RelativePercent { get; init; }
    public int BreakCount { get; init; }
    public int LimitReachedCount { get; init; }
    public int ExtraTimeGrantCount { get; init; }

    public string DayText => Day.ToDateTime(TimeOnly.MinValue).ToString("ddd");
    public string DateText => Day.ToString("dd.MM");
    public string UsedText => UsageHistoryFormatting.FormatDuration(UsedSeconds);
    public string DetailText => $"{LocalizationService.Get("HistoryBreaksShort")} {BreakCount}  ·  {LocalizationService.Get("HistoryLimitsShort")} {LimitReachedCount}";
}

public sealed class AppUsageHistoryRow
{
    public required string Name { get; init; }
    public long UsedSeconds { get; init; }
    public double RelativePercent { get; init; }
    public string UsedText => UsageHistoryFormatting.FormatDuration(UsedSeconds);
}

public sealed class UsageHistoryEventRow
{
    public required UsageEventRecord Event { get; init; }
    public string TimeText => Event.OccurredAtUtc.ToLocalTime().ToString("dd.MM · HH:mm");
    public string Description => Event.Kind switch
    {
        UsageEventKind.BreakStarted => LocalizationService.Get("HistoryEventBreak"),
        UsageEventKind.LimitReached => LocalizationService.Get("HistoryEventLimit"),
        UsageEventKind.ExtraTimeGranted => $"{LocalizationService.Get("HistoryEventExtraTime")} · +{Event.Value} {LocalizationService.Get("MinuteShort")}",
        UsageEventKind.PolicyChanged => LocalizationService.Get("HistoryEventPolicy"),
        _ => string.Empty
    };
}

internal static class UsageHistoryFormatting
{
    public static string FormatDuration(long seconds)
    {
        long minutes = Math.Max(0, seconds) / 60;
        long hours = minutes / 60;
        long remainder = minutes % 60;
        string hour = LocalizationService.CurrentLanguage == LanguagePreference.English ? "hr" : "sa";
        return hours > 0
            ? $"{hours} {hour} {remainder} {LocalizationService.Get("MinuteShort")}"
            : $"{remainder} {LocalizationService.Get("MinuteShort")}";
    }
}
