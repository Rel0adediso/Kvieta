using System.Text.Json;
using System.Text.Json.Serialization;
using Otium.Core.Models;

namespace Otium.Core.Services;

public sealed class JsonSettingsStore
{
    private static readonly string LegacyPreOtiumDirectoryName = "Kardes" + "Kilidi";
    private readonly IReadOnlyList<string> _legacyFilePaths;
    private readonly ResilientJsonFile<ControlSettings> _file;
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
        _file = CreateFile(FilePath);
        _legacyFilePaths = filePath is null
            ? [Path.Combine(localData, "Denge", "settings.json"), Path.Combine(localData, LegacyPreOtiumDirectoryName, "settings.json")]
            : [];
    }

    public string FilePath { get; }
    public string BackupPath => _file.BackupPath;
    public bool LastLoadRecoveredFromBackup => _file.LastLoadRecoveredFromBackup;
    public bool LastLoadMigrated => _file.LastLoadMigrated;

    public async Task<ControlSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? sourcePath = File.Exists(FilePath)
            ? FilePath
            : _legacyFilePaths.FirstOrDefault(File.Exists);

        if (sourcePath is null)
        {
            return new ControlSettings();
        }

        ResilientJsonFile<ControlSettings> sourceFile = string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase)
            ? _file
            : CreateFile(sourcePath);
        ControlSettings settings = await sourceFile.LoadAsync(cancellationToken);

        if (settings.PendingChange is { } pending && pending.ApplyAfterUtc <= DateTimeOffset.UtcNow)
        {
            settings = pending.TargetSettings;
            settings.PendingChange = null;
            settings.SchemaVersion = 2;
            settings.SetupCompleted = true;
            await SaveAsync(settings, cancellationToken);
        }

        if (!string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync(settings, cancellationToken);
        }

        return settings;
    }

    public async Task SaveAsync(ControlSettings settings, CancellationToken cancellationToken = default)
    {
        await _file.SaveAsync(settings, cancellationToken);
    }

    private static ResilientJsonFile<ControlSettings> CreateFile(string path) => new(
        path,
        JsonOptions,
        static () => new ControlSettings(),
        static settings =>
        {
            if (settings.SchemaVersion > 2)
            {
                throw new InvalidDataException($"Desteklenmeyen ayar şeması: {settings.SchemaVersion}");
            }

            bool changed = settings.SchemaVersion < 2;
            if (changed)
            {
                settings.SchemaVersion = 2;
                settings.SetupCompleted = true;
                settings.Mode = ControlMode.Protected;
            }

            settings.WarningMinutes ??= [15, 5, 1];
            settings.Schedule ??= ControlSettings.CreateDefaultSchedule();
            settings.TemporaryAllowances ??= [];
            settings.AppRules ??= [];
            settings.AdminPin ??= new AdminCredential();
            return new MigrationResult<ControlSettings>(settings, changed);
        });
}
