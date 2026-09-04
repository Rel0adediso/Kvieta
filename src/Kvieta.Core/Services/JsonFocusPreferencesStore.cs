using System.Text.Json;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public sealed class JsonFocusPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly ResilientJsonFile<FocusPreferences> _file;

    public JsonFocusPreferencesStore(string? filePath = null)
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = filePath ?? Path.Combine(localData, "Kvieta", "focus-preferences.json");
        _file = new ResilientJsonFile<FocusPreferences>(
            FilePath,
            JsonOptions,
            () => new FocusPreferences(),
            preferences =>
            {
                int normalized = Math.Clamp(preferences.LastDurationMinutes, 1, 24 * 60);
                bool changed = preferences.SchemaVersion != 1 || preferences.LastDurationMinutes != normalized;
                preferences.SchemaVersion = 1;
                preferences.LastDurationMinutes = normalized;
                return new MigrationResult<FocusPreferences>(preferences, changed);
            });
    }

    public string FilePath { get; }

    public async Task<FocusPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return new FocusPreferences();
        }

        return await _file.LoadAsync(cancellationToken);
    }

    public Task SaveLastDurationAsync(int durationMinutes, CancellationToken cancellationToken = default) =>
        _file.SaveAsync(new FocusPreferences
        {
            LastDurationMinutes = Math.Clamp(durationMinutes, 1, 24 * 60)
        }, cancellationToken);
}
