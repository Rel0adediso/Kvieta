using System.Text.Json;
using System.Text.Json.Serialization;
using Otium.Core.Models;

namespace Otium.Core.Services;

public sealed class JsonUsageStore
{
    private static readonly string LegacyPreOtiumDirectoryName = "Kardes" + "Kilidi";
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
            ? [Path.Combine(localData, "Denge", "usage.json"), Path.Combine(localData, LegacyPreOtiumDirectoryName, "usage.json")]
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

    public async Task ReplaceAsync(UsageLedger ledger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        await _file.SaveAsync(ledger, cancellationToken);
    }

    public Task<UsageLedger> ClearAsync(CancellationToken cancellationToken = default) =>
        _file.UpdateAsync(current => new UsageLedger
        {
            SchemaVersion = 5,
            DataGeneration = checked(current.DataGeneration + 1),
            RetainedFromDay = current.RetainedFromDay,
            LocalDay = DateOnly.FromDateTime(DateTime.Today),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

    public async Task TrimHistoryAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        int safeDays = retentionDays is 30 or 90 or 180 ? retentionDays : 90;
        DateOnly cutoff = DateOnly.FromDateTime(DateTime.Today).AddDays(-(safeDays - 1));
        await _file.UpdateAsync(ledger =>
        {
            ledger.RetainedFromDay = LaterOf(ledger.RetainedFromDay, cutoff);
            ApplyRetentionCutoff(ledger);
            return ledger;
        }, cancellationToken);
    }

    private static UsageLedger Merge(UsageLedger current, UsageLedger incoming)
    {
        if (current.DataGeneration != incoming.DataGeneration)
        {
            return current.DataGeneration > incoming.DataGeneration ? current : incoming;
        }

        DateOnly? retainedFromDay = LaterOf(current.RetainedFromDay, incoming.RetainedFromDay);
        if (current.LocalDay > incoming.LocalDay)
        {
            AddCurrentDayToHistory(current, incoming);
            MergeHistoricalData(current, incoming);
            current.RetainedFromDay = retainedFromDay;
            ApplyRetentionCutoff(current);
            return current;
        }

        if (incoming.LocalDay > current.LocalDay)
        {
            AddCurrentDayToHistory(incoming, current);
            MergeHistoricalData(incoming, current);
            incoming.RetainedFromDay = retainedFromDay;
            ApplyRetentionCutoff(incoming);
            return incoming;
        }

        UsageLedger newest = incoming.LastUpdatedUtc >= current.LastUpdatedUtc ? incoming : current;
        UsageLedger other = ReferenceEquals(newest, incoming) ? current : incoming;
        newest.SchemaVersion = 5;
        newest.RetainedFromDay = retainedFromDay;
        newest.UsedSeconds = Math.Max(newest.UsedSeconds, other.UsedSeconds);
        newest.BonusMinutes = Math.Max(newest.BonusMinutes, other.BonusMinutes);
        newest.BreakCount = Math.Max(newest.BreakCount, other.BreakCount);
        newest.LimitReachedCount = Math.Max(newest.LimitReachedCount, other.LimitReachedCount);
        newest.ExtraTimeGrantCount = Math.Max(newest.ExtraTimeGrantCount, other.ExtraTimeGrantCount);
        newest.AwarenessUsedSeconds = Math.Max(newest.AwarenessUsedSeconds, other.AwarenessUsedSeconds);
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

        foreach ((string applicationId, long seconds) in other.ForegroundAppUsedSeconds)
        {
            newest.ForegroundAppUsedSeconds[applicationId] = Math.Max(newest.ForegroundAppUsedSeconds.GetValueOrDefault(applicationId), seconds);
        }

        foreach ((int hour, long seconds) in other.AwarenessHourlyUsedSeconds)
        {
            newest.AwarenessHourlyUsedSeconds[hour] = Math.Max(newest.AwarenessHourlyUsedSeconds.GetValueOrDefault(hour), seconds);
        }

        MergeHistoricalData(newest, other);
        ApplyRetentionCutoff(newest);
        return newest;
    }

    private static DateOnly? LaterOf(DateOnly? left, DateOnly? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return left.Value >= right.Value ? left : right;
    }

    private static void ApplyRetentionCutoff(UsageLedger ledger)
    {
        if (ledger.RetainedFromDay is not { } cutoff)
        {
            return;
        }

        ledger.History = ledger.History.Where(day => day.LocalDay >= cutoff).ToList();
        ledger.RecentEvents = ledger.RecentEvents
            .Where(item => DateOnly.FromDateTime(item.OccurredAtUtc.ToLocalTime().DateTime) >= cutoff)
            .ToList();
    }

    private static void MergeHistoricalData(UsageLedger target, UsageLedger source)
    {
        Dictionary<DateOnly, DailyUsageRecord> history = target.History
            .Concat(source.History)
            .GroupBy(item => item.LocalDay)
            .ToDictionary(group => group.Key, group => MergeDay(group));
        target.History = history.Values
            .OrderByDescending(item => item.LocalDay)
            .Take(180)
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
        if (source.UsedSeconds <= 0 && source.AppUsedSeconds.Count == 0 && source.AwarenessUsedSeconds <= 0 && source.BreakCount == 0 &&
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
            AwarenessUsedSeconds = source.AwarenessUsedSeconds,
            AwarenessHourlyUsedSeconds = new Dictionary<int, long>(source.AwarenessHourlyUsedSeconds),
            Applications = source.AppUsedSeconds.Select(item => new AppUsageRecord
            {
                RuleId = item.Key,
                UsedSeconds = item.Value
            }).ToList(),
            ForegroundApplications = source.ForegroundAppUsedSeconds.Select(item => new AwarenessAppUsageRecord
            {
                ApplicationId = item.Key,
                Name = Path.GetFileNameWithoutExtension(item.Key),
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
            AwarenessUsedSeconds = values.Max(item => item.AwarenessUsedSeconds),
            AwarenessHourlyUsedSeconds = values
                .SelectMany(item => item.AwarenessHourlyUsedSeconds)
                .GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.Max(item => item.Value)),
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
                .ToList(),
            ForegroundApplications = values
                .SelectMany(item => item.ForegroundApplications)
                .GroupBy(item => item.ApplicationId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AwarenessAppUsageRecord
                {
                    ApplicationId = group.Key,
                    Name = group.Select(item => item.Name).LastOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? Path.GetFileNameWithoutExtension(group.Key),
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
            if (ledger.SchemaVersion > 5)
            {
                throw new InvalidDataException($"Desteklenmeyen kullanım şeması: {ledger.SchemaVersion}");
            }

            bool changed = ledger.SchemaVersion < 5;
            ledger.SchemaVersion = 5;
            ledger.AppUsedSeconds ??= [];
            ledger.ForegroundAppUsedSeconds ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            ledger.AwarenessHourlyUsedSeconds ??= [];
            ledger.History ??= [];
            foreach (DailyUsageRecord day in ledger.History)
            {
                day.Applications ??= [];
                day.ForegroundApplications ??= [];
                day.AwarenessHourlyUsedSeconds ??= [];
            }
            ledger.RecentEvents ??= [];
            return new MigrationResult<UsageLedger>(ledger, changed);
        });
}
