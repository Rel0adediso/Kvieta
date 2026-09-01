using System.Text.Json;

namespace Kvieta.Core.Services;

public sealed record SecurityAuditEntry(DateTimeOffset OccurredAtUtc, string Event, string Outcome);

public sealed class SecurityAuditLog(string? filePath = null)
{
    private const int MaximumEntries = 500;
    private const int MaximumTokenLength = 96;
    private const int MaximumFileBytes = 256 * 1024;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string FilePath { get; } = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kvieta", "security-audit.jsonl");

    public async Task AppendAsync(string eventName, string outcome, CancellationToken cancellationToken = default)
    {
        if (!IsSafeToken(eventName) || !IsSafeToken(outcome))
        {
            throw new ArgumentException("Audit fields may contain only letters, digits, dots, dashes, and underscores.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            await using FileStream fileLock = await AcquireFileLockAsync(cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<SecurityAuditEntry> entries = LoadRetainedEntries(now, MaximumEntries - 1);
            entries.Add(new SecurityAuditEntry(now, eventName, outcome));
            List<string> lines = FitToFileLimit(entries
                .Select(entry => JsonSerializer.Serialize(entry, JsonOptions))
                .ToList());
            string temporary = FilePath + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllLinesAsync(temporary, lines, cancellationToken);
                File.Move(temporary, FilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<SecurityAuditEntry>> ReadRecentAsync(
        int maximumEntries = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SecurityAuditEntry> entries = LoadRetainedEntries(DateTimeOffset.UtcNow, maximumEntries);
        return Task.FromResult(entries);
    }

    private static bool IsSafeToken(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumTokenLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private List<SecurityAuditEntry> LoadRetainedEntries(DateTimeOffset now, int maximumEntries)
    {
        if (!File.Exists(FilePath)) return [];

        DateTimeOffset oldestAllowed = now - RetentionPeriod;
        DateTimeOffset newestAllowed = now.AddMinutes(5);
        Queue<SecurityAuditEntry> retained = new(maximumEntries);
        foreach (string line in File.ReadLines(FilePath))
        {
            SecurityAuditEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<SecurityAuditEntry>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is null || entry.OccurredAtUtc < oldestAllowed || entry.OccurredAtUtc > newestAllowed ||
                !IsSafeToken(entry.Event) || !IsSafeToken(entry.Outcome))
            {
                continue;
            }

            if (retained.Count == maximumEntries) retained.Dequeue();
            retained.Enqueue(entry);
        }

        return retained.ToList();
    }

    private static List<string> FitToFileLimit(List<string> lines)
    {
        int byteCount = 0;
        List<string> retained = [];
        for (int index = lines.Count - 1; index >= 0; index--)
        {
            int lineBytes = System.Text.Encoding.UTF8.GetByteCount(lines[index]) + Environment.NewLine.Length;
            if (byteCount + lineBytes > MaximumFileBytes) break;
            retained.Add(lines[index]);
            byteCount += lineBytes;
        }

        retained.Reverse();
        return retained;
    }

    private async Task<FileStream> AcquireFileLockAsync(CancellationToken cancellationToken)
    {
        string lockPath = FilePath + ".lock";
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < LockTimeout)
            {
                await Task.Delay(40, cancellationToken);
            }
        }
    }
}
