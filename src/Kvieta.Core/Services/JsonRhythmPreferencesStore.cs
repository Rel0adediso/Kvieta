using System.Text.Json;
using System.Text.Json.Serialization;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public sealed class JsonRhythmPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly ResilientJsonFile<RhythmPreferences> _file;

    public JsonRhythmPreferencesStore(string? filePath = null)
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = filePath ?? Path.Combine(localData, "Kvieta", "rhythm-preferences.json");
        _file = new ResilientJsonFile<RhythmPreferences>(
            FilePath,
            JsonOptions,
            () => new RhythmPreferences(),
            preferences =>
            {
                bool changed = preferences.SchemaVersion != 2;
                preferences.SchemaVersion = 2;
                return new MigrationResult<RhythmPreferences>(preferences, changed);
            });
    }

    public string FilePath { get; }

    public async Task<RhythmPreferences> LoadAsync(CancellationToken cancellationToken = default) =>
        File.Exists(FilePath) ? await _file.LoadAsync(cancellationToken) : new RhythmPreferences();

    public Task SaveAsync(
        RhythmSuggestionPreference preference,
        DateTimeOffset? remindAfterUtc = null,
        CancellationToken cancellationToken = default) =>
        _file.UpdateAsync(current =>
        {
            current.SuggestionPreference = preference;
            current.RemindAfterUtc = remindAfterUtc?.ToUniversalTime();
            return current;
        }, cancellationToken);

    public Task MarkMilestoneCelebratedAsync(int milestone, CancellationToken cancellationToken = default) =>
        _file.UpdateAsync(current =>
        {
            current.LastCelebratedStreakMilestone = Math.Max(current.LastCelebratedStreakMilestone, milestone);
            return current;
        }, cancellationToken);
}
