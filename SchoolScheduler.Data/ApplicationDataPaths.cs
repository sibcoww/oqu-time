namespace SchoolScheduler.Data;

public static class ApplicationDataPaths
{
    public const string ApplicationDirectoryName = "SchoolScheduler";
    public const string DatabaseFileName = "school.db";

    public static string GetDatabasePath(string? localApplicationData = null)
    {
        var root = localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Не удалось определить каталог локальных данных пользователя.");

        return Path.Combine(root, ApplicationDirectoryName, "data", DatabaseFileName);
    }

    public static string PrepareDatabase(string? localApplicationData = null, string? legacyDirectory = null)
    {
        var databasePath = GetDatabasePath(localApplicationData);
        var dataDirectory = Path.GetDirectoryName(databasePath)!;
        Directory.CreateDirectory(dataDirectory);

        var oldDatabasePath = Path.Combine(legacyDirectory ?? AppContext.BaseDirectory, DatabaseFileName);
        if (!File.Exists(databasePath) && File.Exists(oldDatabasePath) &&
            !Path.GetFullPath(oldDatabasePath).Equals(Path.GetFullPath(databasePath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(oldDatabasePath, databasePath);
            CopySidecarIfPresent(oldDatabasePath, databasePath, "-wal");
            CopySidecarIfPresent(oldDatabasePath, databasePath, "-shm");
        }

        return databasePath;
    }

    private static void CopySidecarIfPresent(string source, string destination, string suffix)
    {
        if (File.Exists(source + suffix)) File.Copy(source + suffix, destination + suffix);
    }
}
