using System.Text.Json;
using System.Text.Json.Serialization;
using KardesKilidi.Core.Models;

namespace KardesKilidi.Core.Services;

public sealed class JsonSettingsStore
{
    private readonly IReadOnlyList<string> _legacyFilePaths;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonSettingsStore(string? filePath = null)
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = filePath ?? Path.Combine(localData, "Otium", "settings.json");
        _legacyFilePaths = filePath is null
            ? [Path.Combine(localData, "Denge", "settings.json"), Path.Combine(localData, "KardesKilidi", "settings.json")]
            : [];
    }

    public string FilePath { get; }

    public async Task<ControlSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? sourcePath = File.Exists(FilePath)
            ? FilePath
            : _legacyFilePaths.FirstOrDefault(File.Exists);

        if (sourcePath is null)
        {
            return new ControlSettings();
        }

        ControlSettings? settings;
        await using (FileStream stream = File.OpenRead(sourcePath))
        {
            settings = await JsonSerializer.DeserializeAsync<ControlSettings>(
                stream,
                JsonOptions,
                cancellationToken);
        }

        bool migrated = settings is not null && settings.SchemaVersion < 2;
        if (migrated)
        {
            settings!.SchemaVersion = 2;
            settings.SetupCompleted = true;
            settings.Mode = ControlMode.Protected;
        }

        if (settings?.PendingChange is { } pending && pending.ApplyAfterUtc <= DateTimeOffset.UtcNow)
        {
            settings = pending.TargetSettings;
            settings.PendingChange = null;
            settings.SchemaVersion = 2;
            settings.SetupCompleted = true;
            await SaveAsync(settings, cancellationToken);
        }

        if (settings is not null &&
            (!string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase) || migrated))
        {
            await SaveAsync(settings, cancellationToken);
        }

        return settings ?? new ControlSettings();
    }

    public async Task SaveAsync(ControlSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = FilePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, FilePath, true);
    }
}
