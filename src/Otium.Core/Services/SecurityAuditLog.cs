using System.Text.Json;

namespace Otium.Core.Services;

public sealed record SecurityAuditEntry(DateTimeOffset OccurredAtUtc, string Event, string Outcome);

public sealed class SecurityAuditLog(string? filePath = null)
{
    private const int MaximumEntries = 500;
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string FilePath { get; } = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Otium", "security-audit.jsonl");

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
            List<string> lines = File.Exists(FilePath)
                ? (await File.ReadAllLinesAsync(FilePath, cancellationToken)).TakeLast(MaximumEntries - 1).ToList()
                : [];
            lines.Add(JsonSerializer.Serialize(new SecurityAuditEntry(DateTimeOffset.UtcNow, eventName, outcome), JsonOptions));
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

    private static bool IsSafeToken(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

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
