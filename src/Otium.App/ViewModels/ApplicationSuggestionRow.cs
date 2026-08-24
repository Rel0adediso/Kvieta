using System.IO;
using System.Windows.Media;
using Otium.App.Services;

namespace Otium.App.ViewModels;

public sealed class ApplicationSuggestionRow
{
    public ApplicationSuggestionRow(ApplicationSuggestion suggestion)
    {
        Name = suggestion.Name;
        ExecutablePath = suggestion.ExecutablePath;
        UsedSeconds = suggestion.UsedSeconds;
        Icon = ApplicationIconProvider.GetIcon(ExecutablePath);
        FallbackBrush = ApplicationIconProvider.GetFallbackBrush(Name);
    }

    public string Name { get; }
    public string ExecutablePath { get; }
    public long UsedSeconds { get; }
    public ImageSource? Icon { get; }
    public System.Windows.Media.Brush FallbackBrush { get; }
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

    public string UsageText => UsedSeconds <= 0
        ? LocalizationService.Get("CurrentlyOpen")
        : string.Format(
            LocalizationService.Get("UsedInLastSevenDays"),
            UsedSeconds >= 3600
                ? $"{UsedSeconds / 3600d:0.#} {LocalizationService.Get("HourShort")}"
                : $"{Math.Max(1, UsedSeconds / 60)} {LocalizationService.Get("MinuteShort")}");
}
