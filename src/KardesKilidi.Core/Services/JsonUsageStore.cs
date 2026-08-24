using System.Text.Json;
using System.Text.Json.Serialization;
using KardesKilidi.Core.Models;

namespace KardesKilidi.Core.Services;

public sealed class JsonUsageStore
{
    private readonly IReadOnlyList<string> _legacyFilePaths;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonUsageStore(string? filePath = null)
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = filePath ?? Path.Combine(localData, "Otium", "usage.json");
        _legacyFilePaths = filePath is null
            ? [Path.Combine(localData, "Denge", "usage.json"), Path.Combine(localData, "KardesKilidi", "usage.json")]
            : [];
    }

    public string FilePath { get; }

    public async Task<UsageLedger> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? sourcePath = File.Exists(FilePath)
            ? FilePath
            : _legacyFilePaths.FirstOrDefault(File.Exists);

        if (sourcePath is null)
        {
            return new UsageLedger();
        }

        UsageLedger? ledger;
        await using (FileStream stream = File.OpenRead(sourcePath))
        {
            ledger = await JsonSerializer.DeserializeAsync<UsageLedger>(stream, JsonOptions, cancellationToken);
        }

        if (ledger is not null && !string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync(ledger, cancellationToken);
        }

        if (ledger is not null)
        {
            ledger.SchemaVersion = 2;
            ledger.AppUsedSeconds ??= [];
            ledger.History ??= [];
            ledger.RecentEvents ??= [];
        }

        return ledger ?? new UsageLedger();
    }

    public async Task SaveAsync(UsageLedger ledger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = FilePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, ledger, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, FilePath, true);
    }
}
