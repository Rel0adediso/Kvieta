using Kvieta.App.Services;
using Kvieta.Core.Models;
using Kvieta.Core.Services;
using System.IO;
using System.Windows.Media;

namespace Kvieta.App.ViewModels;

public sealed class UsageHistoryDayRow : ObservableObject
{
    private bool _isSelected;
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
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string SummaryText => $"{DayText} · {UsedText} · {DetailText}";
}

public sealed class RhythmDayRow : ObservableObject
{
    private bool _isSelected;
    public required RhythmDayResult Result { get; init; }
    public DateOnly Day => Result.Day;
    public string DayText => Day.ToDateTime(TimeOnly.MinValue).ToString("ddd");
    public string DateText => Day.ToString("dd.MM");
    public string SymbolText => Result.Outcome switch
    {
        RhythmDayOutcome.Success => "✓",
        RhythmDayOutcome.Pending => "…",
        RhythmDayOutcome.Rest => "☾",
        RhythmDayOutcome.Excused => "○",
        RhythmDayOutcome.Protected => "◇",
        RhythmDayOutcome.Missed => "×",
        _ => "—"
    };
    public string StateText => Result.Outcome switch
    {
        RhythmDayOutcome.Success => L("Tamamlandı", "Complete"),
        RhythmDayOutcome.Pending => L("Devam ediyor", "In progress"),
        RhythmDayOutcome.Rest => L("Dinlenme", "Rest"),
        RhythmDayOutcome.Excused => L("Muaf", "Excused"),
        RhythmDayOutcome.Protected => L("Korundu", "Protected"),
        RhythmDayOutcome.Missed => L("Kaçırıldı", "Missed"),
        _ => L("Ölçülmedi", "Unobserved")
    };
    public string GoalText => Result.Goal switch
    {
        RhythmGoalKind.ReviewSummary => L("Günlük özeti değerlendir", "Review the daily summary"),
        RhythmGoalKind.CompleteFocus when Result.FocusTargetKind == FocusRhythmTargetKind.Minutes =>
            L($"{Result.Target} dk odak", $"{Result.Target} min focus"),
        RhythmGoalKind.CompleteFocus => L($"{Result.Target} odak oturumu (en az 5 dk)", $"{Result.Target} focus session(s) (5 min minimum)"),
        RhythmGoalKind.KeepBalance => L("Günlük dengeni koru", "Keep your daily balance"),
        _ => L("Hedef kaydı yok", "No goal record")
    };
    public string ProgressText => Result.Goal switch
    {
        RhythmGoalKind.CompleteFocus when Result.FocusTargetKind == FocusRhythmTargetKind.Minutes => $"{Result.Progress}/{Result.Target} {L("dk", "min")}",
        RhythmGoalKind.CompleteFocus => $"{Result.Progress}/{Result.Target}",
        RhythmGoalKind.ReviewSummary => $"{Result.Progress}/{Result.Target}",
        RhythmGoalKind.KeepBalance => $"{Result.Progress}/{Result.Target} {L("dk", "min")}",
        _ => "—"
    };
    public string DetailText => $"{Day:dd.MM} · {GoalText} · {ProgressText} · {StateText}";
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    private static string L(string tr, string en) => LocalizationService.CurrentLanguage == LanguagePreference.English ? en : tr;
}

public sealed class AppUsageHistoryRow
{
    public int Rank { get; init; }
    public string ApplicationId { get; init; } = string.Empty;
    public required string Name { get; init; }
    public long UsedSeconds { get; init; }
    public double RelativePercent { get; init; }
    public ImageSource? Icon { get; init; }
    public required System.Windows.Media.Brush FallbackBrush { get; init; }
    public DoubleCollection TrendValues { get; init; } = [];
    public string UsedText => UsageHistoryFormatting.FormatDuration(UsedSeconds);
    public string RankText => $"#{Rank}";
    public bool HasIcon => Icon is not null;
    public bool HasFallbackIcon => Icon is null;
    public string Initials
    {
        get
        {
            string cleanName = Path.GetFileNameWithoutExtension(Name).Trim();
            string[] words = cleanName.Split(['.', ' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 1
                ? string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])))
                : cleanName[..Math.Min(2, cleanName.Length)].ToUpperInvariant();
        }
    }
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
