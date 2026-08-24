using System.Diagnostics;
using System.Text.Json;

namespace Otium.Core.Services;

internal sealed class ResilientJsonFile<T> where T : class
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(40);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Func<T> _createDefault;
    private readonly Func<T, MigrationResult<T>> _migrate;

    public ResilientJsonFile(
        string filePath,
        JsonSerializerOptions jsonOptions,
        Func<T> createDefault,
        Func<T, MigrationResult<T>> migrate)
    {
        FilePath = filePath;
        _jsonOptions = jsonOptions;
        _createDefault = createDefault;
        _migrate = migrate;
    }

    public string FilePath { get; }
    public string BackupPath => FilePath + ".bak";
    public string LockPath => FilePath + ".lock";
    public bool LastLoadRecoveredFromBackup { get; private set; }
    public bool LastLoadMigrated { get; private set; }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        LastLoadRecoveredFromBackup = false;
        LastLoadMigrated = false;
        await using FileStream fileLock = await AcquireLockAsync(cancellationToken);

        return await LoadCoreAsync(cancellationToken);
    }

    public async Task<T> UpdateAsync(Func<T, T> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await using FileStream fileLock = await AcquireLockAsync(cancellationToken);
        T current = await LoadCoreAsync(cancellationToken);
        T updated = update(current) ?? throw new InvalidDataException("Otium veri güncellemesi geçersiz sonuç üretti.");
        MigrationResult<T> migrated = _migrate(updated);
        await WriteCoreAsync(migrated.Value, updateBackup: true, cancellationToken);
        return migrated.Value;
    }

    private async Task<T> LoadCoreAsync(CancellationToken cancellationToken)
    {
        LastLoadRecoveredFromBackup = false;
        LastLoadMigrated = false;

        if (!File.Exists(FilePath))
        {
            return _createDefault();
        }

        try
        {
            MigrationResult<T> result = await ReadAndMigrateAsync(FilePath, cancellationToken);
            LastLoadMigrated = result.Changed;
            if (result.Changed)
            {
                await WriteCoreAsync(result.Value, updateBackup: true, cancellationToken);
            }

            return result.Value;
        }
        catch (Exception primaryException) when (IsRecoverableReadFailure(primaryException))
        {
            if (!File.Exists(BackupPath))
            {
                throw new InvalidDataException($"Otium veri dosyası okunamadı ve sağlam yedek bulunamadı: {FilePath}", primaryException);
            }

            try
            {
                MigrationResult<T> recovered = await ReadAndMigrateAsync(BackupPath, cancellationToken);
                await WriteCoreAsync(recovered.Value, updateBackup: true, cancellationToken);
                LastLoadRecoveredFromBackup = true;
                LastLoadMigrated = recovered.Changed;
                return recovered.Value;
            }
            catch (Exception backupException) when (IsRecoverableReadFailure(backupException))
            {
                throw new InvalidDataException(
                    $"Otium veri dosyası ve son sağlam yedeği okunamadı: {FilePath}",
                    new AggregateException(primaryException, backupException));
            }
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await using FileStream fileLock = await AcquireLockAsync(cancellationToken);
        MigrationResult<T> migrated = _migrate(value);
        await WriteCoreAsync(migrated.Value, updateBackup: true, cancellationToken);
    }

    private async Task<MigrationResult<T>> ReadAndMigrateAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        T? value = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
        if (value is null)
        {
            throw new InvalidDataException($"Otium veri dosyası boş veya geçersiz: {path}");
        }

        return _migrate(value);
    }

    private async Task WriteCoreAsync(T value, bool updateBackup, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("Veri dosyasının klasörü belirlenemedi.");
        Directory.CreateDirectory(directory);
        string temporaryPath = FilePath + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            await ReadAndMigrateAsync(temporaryPath, cancellationToken);
            if (File.Exists(FilePath))
            {
                File.Replace(temporaryPath, FilePath, BackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, FilePath);
            }

            if (updateBackup)
            {
                await UpdateBackupSnapshotAsync(cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task UpdateBackupSnapshotAsync(CancellationToken cancellationToken)
    {
        string backupTemporaryPath = BackupPath + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            await using (FileStream source = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream target = new(backupTemporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
                target.Flush(flushToDisk: true);
            }

            await ReadAndMigrateAsync(backupTemporaryPath, cancellationToken);
            File.Move(backupTemporaryPath, BackupPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(backupTemporaryPath))
            {
                File.Delete(backupTemporaryPath);
            }
        }
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < LockTimeout)
            {
                await Task.Delay(LockRetryDelay, cancellationToken);
            }
        }
    }

    private static bool IsRecoverableReadFailure(Exception exception) =>
        exception is JsonException or InvalidDataException or IOException;
}

internal readonly record struct MigrationResult<T>(T Value, bool Changed) where T : class;
