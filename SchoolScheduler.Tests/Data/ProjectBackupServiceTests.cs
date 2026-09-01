using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.Data;

public sealed class ProjectBackupServiceTests
{
    [Fact]
    public async Task BackupAndRestore_PreservesKeyProjectDataInCleanDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "school.db");
        var backupPath = Path.Combine(directory, "school.schoolscheduler");
        try
        {
            IDbContextFactory<AppDbContext> factory = new FileContextFactory(databasePath);
            await SeedAsync(factory);
            var service = new ProjectBackupService(factory);

            await service.CreateAsync(backupPath);

            using (var archive = ZipFile.OpenRead(backupPath))
            {
                Assert.NotNull(archive.GetEntry(ProjectBackupService.ManifestEntryName));
                Assert.NotNull(archive.GetEntry(ProjectBackupService.DatabaseEntryName));
            }
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();
                Assert.Empty(await db.Schools.ToListAsync());
            }

            await service.RestoreAsync(backupPath);

            await using var restored = await factory.CreateDbContextAsync();
            Assert.Equal("Школа №15", Assert.Single(await restored.Schools.ToListAsync()).Name);
            Assert.Equal("2026–2027", Assert.Single(await restored.AcademicYears.ToListAsync()).Name);
            Assert.Equal("Иванова А.А.", Assert.Single(await restored.Teachers.ToListAsync()).FullName);
            Assert.Equal("12", Assert.Single(await restored.Rooms.ToListAsync()).Name);
            Assert.Equal(5m, Assert.Single(await restored.TeachingLoads.ToListAsync()).HoursPerWeek);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Restore_RejectsForeignFileAndKeepsCurrentDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"backup-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "school.db");
        var invalidPath = Path.Combine(directory, "invalid.schoolscheduler");
        try
        {
            IDbContextFactory<AppDbContext> factory = new FileContextFactory(databasePath);
            await SeedAsync(factory);
            await File.WriteAllTextAsync(invalidPath, "not an archive");

            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new ProjectBackupService(factory).RestoreAsync(invalidPath));

            await using var db = await factory.CreateDbContextAsync();
            Assert.Equal("Школа №15", Assert.Single(await db.Schools.ToListAsync()).Name);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    private static async Task SeedAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        var teacher = new Teacher { FullName = "Иванова А.А." };
        var subject = new Subject { Name = "Математика", ShortName = "Мат." };
        var schoolClass = new SchoolClass { Name = "7Б", Parallel = 7, Letter = "Б", ShiftId = 1, MaxLessonsPerDay = 7 };
        var room = new Room { Name = "12" };
        db.AddRange(new School { Name = "Школа №15", DaysPerWeek = 6 },
            new AcademicYear { Name = "2026–2027", IsActive = true }, teacher, subject, schoolClass, room);
        await db.SaveChangesAsync();
        db.TeachingLoads.Add(new TeachingLoad
        {
            TeacherId = teacher.Id, SubjectId = subject.Id, ClassId = schoolClass.Id,
            RoomId = room.Id, HoursPerWeek = 5m
        });
        await db.SaveChangesAsync();
    }

    private sealed class FileContextFactory(string path) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options);
    }
}
