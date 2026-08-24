using System.Text.Json;
using System.Text.Json.Serialization;
using KardesKilidi.Core.Models;

namespace KardesKilidi.Core.Services;

public sealed class JsonUsageStore
{
    private readonly IReadOnlyList<string> _legacyFilePaths;
    private readonly ResilientJsonFile<UsageLedger> _file;
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
        _file = CreateFile(FilePath);
        _legacyFilePaths = filePath is null
            ? [Path.Combine(localData, "Denge", "usage.json"), Path.Combine(localData, "KardesKilidi", "usage.json")]
            : [];
    }

    public string FilePath { get; }
    public string BackupPath => _file.BackupPath;
    public bool LastLoadRecoveredFromBackup => _file.LastLoadRecoveredFromBackup;
    public bool LastLoadMigrated => _file.LastLoadMigrated;

    public async Task<UsageLedger> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? sourcePath = File.Exists(FilePath)
            ? FilePath
            : _legacyFilePaths.FirstOrDefault(File.Exists);

        if (sourcePath is null)
        {
            return new UsageLedger();
        }

        ResilientJsonFile<UsageLedger> sourceFile = string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase)
            ? _file
            : CreateFile(sourcePath);
        UsageLedger ledger = await sourceFile.LoadAsync(cancellationToken);

        if (!string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync(ledger, cancellationToken);
        }

        return ledger;
    }

    public async Task SaveAsync(UsageLedger ledger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        await _file.UpdateAsync(current => Merge(current, ledger), cancellationToken);
    }

    private static UsageLedger Merge(UsageLedger current, UsageLedger incoming)
    {
        if (current.LocalDay > incoming.LocalDay)
        {
            AddCurrentDayToHistory(current, incoming);
            MergeHistoricalData(current, incoming);
            return current;
        }

        if (incoming.LocalDay > current.LocalDay)
        {
            AddCurrentDayToHistory(incoming, current);
            MergeHistoricalData(incoming, current);
            return incoming;
        }

        UsageLedger newest = incoming.LastUpdatedUtc >= current.LastUpdatedUtc ? incoming : current;
        UsageLedger other = ReferenceEquals(newest, incoming) ? current : incoming;
        newest.SchemaVersion = 2;
        newest.UsedSeconds = Math.Max(newest.UsedSeconds, other.UsedSeconds);
        newest.BonusMinutes = Math.Max(newest.BonusMinutes, other.BonusMinutes);
        newest.BreakCount = Math.Max(newest.BreakCount, other.BreakCount);
        newest.LimitReachedCount = Math.Max(newest.LimitReachedCount, other.LimitReachedCount);
        newest.ExtraTimeGrantCount = Math.Max(newest.ExtraTimeGrantCount, other.ExtraTimeGrantCount);
        newest.LastUpdatedUtc = newest.LastUpdatedUtc >= other.LastUpdatedUtc ? newest.LastUpdatedUtc : other.LastUpdatedUtc;
        if (other.ClockRollbackUntilUtc is { } otherRollback &&
            (newest.ClockRollbackUntilUtc is null || otherRollback > newest.ClockRollbackUntilUtc))
        {
            newest.ClockRollbackUntilUtc = otherRollback;
        }

        foreach ((Guid ruleId, long seconds) in other.AppUsedSeconds)
        {
            newest.AppUsedSeconds[ruleId] = Math.Max(newest.AppUsedSeconds.GetValueOrDefault(ruleId), seconds);
        }

        MergeHistoricalData(newest, other);
        return newest;
    }

    private static void MergeHistoricalData(UsageLedger target, UsageLedger source)
    {
        Dictionary<DateOnly, DailyUsageRecord> history = target.History
            .Concat(source.History)
            .GroupBy(item => item.LocalDay)
            .ToDictionary(group => group.Key, group => MergeDay(group));
        target.History = history.Values
            .OrderByDescending(item => item.LocalDay)
            .Take(90)
            .OrderBy(item => item.LocalDay)
            .ToList();

        target.RecentEvents = target.RecentEvents
            .Concat(source.RecentEvents)
            .GroupBy(item => (item.OccurredAtUtc, item.Kind, item.Value))
            .Select(group => group.First())
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(200)
            .OrderBy(item => item.OccurredAtUtc)
            .ToList();
    }

    private static void AddCurrentDayToHistory(UsageLedger target, UsageLedger source)
    {
        if (source.UsedSeconds <= 0 && source.AppUsedSeconds.Count == 0 && source.BreakCount == 0 &&
            source.LimitReachedCount == 0 && source.ExtraTimeGrantCount == 0)
        {
            return;
        }

        target.History.Add(new DailyUsageRecord
        {
            LocalDay = source.LocalDay,
            UsedSeconds = source.UsedSeconds,
            BonusMinutes = source.BonusMinutes,
            BreakCount = source.BreakCount,
            LimitReachedCount = source.LimitReachedCount,
            ExtraTimeGrantCount = source.ExtraTimeGrantCount,
            Applications = source.AppUsedSeconds.Select(item => new AppUsageRecord
            {
                RuleId = item.Key,
                UsedSeconds = item.Value
            }).ToList()
        });
    }

    private static DailyUsageRecord MergeDay(IEnumerable<DailyUsageRecord> records)
    {
        List<DailyUsageRecord> values = records.ToList();
        DailyUsageRecord first = values[0];
        return new DailyUsageRecord
        {
            LocalDay = first.LocalDay,
            UsedSeconds = values.Max(item => item.UsedSeconds),
            BonusMinutes = values.Max(item => item.BonusMinutes),
            BreakCount = values.Max(item => item.BreakCount),
            LimitReachedCount = values.Max(item => item.LimitReachedCount),
            ExtraTimeGrantCount = values.Max(item => item.ExtraTimeGrantCount),
            Applications = values
                .SelectMany(item => item.Applications)
                .GroupBy(item => item.RuleId)
                .Select(group => new AppUsageRecord
                {
                    RuleId = group.Key,
                    Name = group.Select(item => item.Name).LastOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
                    UsedSeconds = group.Max(item => item.UsedSeconds)
                })
                .OrderByDescending(item => item.UsedSeconds)
                .ToList()
        };
    }

    private static ResilientJsonFile<UsageLedger> CreateFile(string path) => new(
        path,
        JsonOptions,
        static () => new UsageLedger(),
        static ledger =>
        {
            if (ledger.SchemaVersion > 2)
            {
                throw new InvalidDataException($"Desteklenmeyen kullanım şeması: {ledger.SchemaVersion}");
            }

            bool changed = ledger.SchemaVersion < 2;
            ledger.SchemaVersion = 2;
            ledger.AppUsedSeconds ??= [];
            ledger.History ??= [];
            ledger.RecentEvents ??= [];
            return new MigrationResult<UsageLedger>(ledger, changed);
        });
}
