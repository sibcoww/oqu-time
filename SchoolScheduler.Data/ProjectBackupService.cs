using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SchoolScheduler.Data;

public sealed record ProjectBackupManifest(int FormatVersion, DateTimeOffset CreatedAtUtc,
    string Application, string DatabaseEntry);

public sealed class ProjectBackupService(IDbContextFactory<AppDbContext> factory) : IProjectBackupService
{
    public const int CurrentFormatVersion = 1;
    public const string ManifestEntryName = "manifest.json";
    public const string DatabaseEntryName = "project.db";

    public async Task CreateAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        var fullBackupPath = Path.GetFullPath(backupPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullBackupPath)!);
        var workDirectory = CreateWorkDirectory();
        try
        {
            var snapshotPath = Path.Combine(workDirectory, DatabaseEntryName);
            await using (var db = await factory.CreateDbContextAsync(cancellationToken))
            {
                var sourcePath = Path.GetFullPath(db.Database.GetDbConnection().DataSource);
                if (string.Equals(sourcePath, fullBackupPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Файл резервной копии не может совпадать с рабочей базой данных.");
                await db.Database.OpenConnectionAsync(cancellationToken);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "VACUUM INTO $snapshot";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$snapshot";
                parameter.Value = snapshotPath;
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var manifest = new ProjectBackupManifest(CurrentFormatVersion, DateTimeOffset.UtcNow,
                "SchoolScheduler", DatabaseEntryName);
            await File.WriteAllTextAsync(Path.Combine(workDirectory, ManifestEntryName),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            var temporaryBackup = fullBackupPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                ZipFile.CreateFromDirectory(workDirectory, temporaryBackup, CompressionLevel.Optimal, false);
                File.Move(temporaryBackup, fullBackupPath, true);
            }
            finally { if (File.Exists(temporaryBackup)) File.Delete(temporaryBackup); }
        }
        finally { Directory.Delete(workDirectory, true); }
    }

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        var fullBackupPath = Path.GetFullPath(backupPath);
        if (!File.Exists(fullBackupPath)) throw new FileNotFoundException("Файл резервной копии не найден.", fullBackupPath);
        var workDirectory = CreateWorkDirectory();
        try
        {
            string extractedDatabase;
            using (var archive = ZipFile.OpenRead(fullBackupPath))
            {
                var manifestEntry = archive.GetEntry(ManifestEntryName)
                    ?? throw new InvalidDataException("В резервной копии отсутствует манифест.");
                ProjectBackupManifest? manifest;
                await using (var stream = manifestEntry.Open())
                    manifest = await JsonSerializer.DeserializeAsync<ProjectBackupManifest>(stream, cancellationToken: cancellationToken);
                if (manifest is null || manifest.Application != "SchoolScheduler")
                    throw new InvalidDataException("Файл не является резервной копией SchoolScheduler.");
                if (manifest.FormatVersion != CurrentFormatVersion)
                    throw new InvalidDataException($"Версия резервной копии {manifest.FormatVersion} не поддерживается.");
                var databaseEntry = archive.GetEntry(manifest.DatabaseEntry)
                    ?? throw new InvalidDataException("В резервной копии отсутствует база данных проекта.");
                extractedDatabase = Path.Combine(workDirectory, DatabaseEntryName);
                await using var source = databaseEntry.Open();
                await using var target = File.Create(extractedDatabase);
                await source.CopyToAsync(target, cancellationToken);
            }

            await ValidateDatabaseAsync(extractedDatabase, cancellationToken);
            string targetPath;
            await using (var db = await factory.CreateDbContextAsync(cancellationToken))
                targetPath = Path.GetFullPath(db.Database.GetDbConnection().DataSource);
            if (string.Equals(targetPath, fullBackupPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Нельзя восстановить проект поверх файла резервной копии.");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            SqliteConnection.ClearAllPools();
            DeleteSidecar(targetPath + "-wal");
            DeleteSidecar(targetPath + "-shm");
            if (File.Exists(targetPath)) File.Replace(extractedDatabase, targetPath, null);
            else File.Move(extractedDatabase, targetPath);
        }
        finally { Directory.Delete(workDirectory, true); }
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check";
        if (!string.Equals(await integrity.ExecuteScalarAsync(cancellationToken) as string, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("База данных в резервной копии повреждена.");
        await using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Schools'";
        if (Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidDataException("В резервной копии отсутствует структура проекта SchoolScheduler.");
    }

    private static string CreateWorkDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SchoolScheduler-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteSidecar(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
