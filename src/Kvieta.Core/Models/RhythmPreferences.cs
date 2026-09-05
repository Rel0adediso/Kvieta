namespace Kvieta.Core.Models;

public enum RhythmSuggestionPreference
{
    Visible,
    RemindLater,
    Hidden
}

public sealed class RhythmPreferences
{
    public int SchemaVersion { get; set; } = 2;
    public RhythmSuggestionPreference SuggestionPreference { get; set; }
    public DateTimeOffset? RemindAfterUtc { get; set; }
    public int LastCelebratedStreakMilestone { get; set; }

    public bool ShouldShowSuggestion(DateTimeOffset now) => SuggestionPreference switch
    {
        RhythmSuggestionPreference.Hidden => false,
        RhythmSuggestionPreference.RemindLater => RemindAfterUtc is null || now.ToUniversalTime() >= RemindAfterUtc.Value,
        _ => true
    };
}
