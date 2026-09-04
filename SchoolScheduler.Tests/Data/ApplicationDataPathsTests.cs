using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.Data;

public sealed class ApplicationDataPathsTests
{
    [Fact]
    public void PrepareDatabase_CreatesUserDataDirectory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = ApplicationDataPaths.PrepareDatabase(root, Path.Combine(root, "legacy"));

            Assert.Equal(Path.Combine(root, "SchoolScheduler", "data", "school.db"), path);
            Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PrepareDatabase_CopiesLegacyDatabaseOnlyWhenTargetIsMissing()
    {
        var root = CreateTemporaryDirectory();
        var legacy = Path.Combine(root, "legacy");
        Directory.CreateDirectory(legacy);
        var oldPath = Path.Combine(legacy, "school.db");
        File.WriteAllText(oldPath, "legacy");

        try
        {
            var path = ApplicationDataPaths.PrepareDatabase(root, legacy);
            Assert.Equal("legacy", File.ReadAllText(path));

            File.WriteAllText(oldPath, "changed legacy");
            File.WriteAllText(path, "current");
            ApplicationDataPaths.PrepareDatabase(root, legacy);

            Assert.Equal("current", File.ReadAllText(path));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SchoolSchedulerPaths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
