using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class TeacherServiceTests
{
    [Fact]
    public async Task SaveTeacher_PersistsAvailability_AndReplacesItOnEdit()
    {
        using var factory = new TestDbContextFactory(CreateOptions());
        var service = new TeacherService(factory);
        var teacher = await service.SaveTeacherAsync(
            new Teacher { FullName = "Иванова А. Б." },
            [new TeacherAvailability { DayOfWeek = 1, LessonPeriodId = 2, IsAvailable = false }]);

        var saved = await service.GetTeacherAsync(teacher.Id);
        Assert.NotNull(saved);
        Assert.Single(saved.Availability);
        Assert.False(saved.Availability.Single().IsAvailable);

        await service.SaveTeacherAsync(saved,
            [new TeacherAvailability { DayOfWeek = 3, LessonPeriodId = 4, IsAvailable = true }]);
        var edited = await service.GetTeacherAsync(teacher.Id);
        Assert.Equal(3, edited!.Availability.Single().DayOfWeek);
        Assert.Equal(4, edited.Availability.Single().LessonPeriodId);
    }

    [Fact]
    public async Task SearchDuplicateAndArchive_WorkWithoutTeachingLoad()
    {
        using var factory = new TestDbContextFactory(CreateOptions());
        var service = new TeacherService(factory);
        var teacher = await service.SaveTeacherAsync(new Teacher { FullName = "Серикова Жанна" }, []);

        Assert.Single(await service.GetTeachersAsync("жанна"));
        Assert.True(await service.TeacherExistsAsync(" серикова жанна "));
        Assert.False(await service.TeacherExistsAsync("Серикова Жанна", teacher.Id));

        await service.ArchiveTeacherAsync(teacher.Id);
        Assert.False((await service.GetTeacherAsync(teacher.Id))!.IsActive);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=teacher-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared")
            .Options;

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _keeper;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
            _keeper = new AppDbContext(options);
            _keeper.Database.OpenConnection();
            _keeper.Database.EnsureCreated();
            _keeper.Shifts.Add(new Shift { Id = 1, Name = "Shift", LessonPeriods =
                [new LessonPeriod { Id = 2, Number = 2 }, new LessonPeriod { Id = 4, Number = 4 }] });
            _keeper.SaveChanges();
        }

        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
