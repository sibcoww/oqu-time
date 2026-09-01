using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class TeachingLoadServiceTests
{
    [Fact]
    public async Task SaveAll_PersistsFractionalHoursRoomZeroLessonAndComment()
    {
        using var factory = new Factory(Options());
        var ids = await SeedAsync(factory);
        var service = new TeachingLoadService(factory);
        await service.SaveAllAsync([
            new TeachingLoad { TeacherId = ids.Teacher, SubjectId = ids.Subject, ClassId = ids.Class,
                GroupId = ids.Group, RoomId = ids.Room, HoursPerWeek = 0.25m,
                AllowZeroLesson = true, Comment = "Раз в четыре недели" }
        ]);

        var row = Assert.Single(await service.GetAllAsync());
        Assert.Equal(0.25m, row.HoursPerWeek);
        Assert.Equal("12", row.Room!.Name);
        Assert.Equal("Группа 1", row.Group!.Name);
        Assert.True(row.AllowZeroLesson);
        Assert.Equal("Раз в четыре недели", row.Comment);
    }

    [Fact]
    public async Task SaveAll_RejectsGroupFromAnotherClass()
    {
        using var factory = new Factory(Options());
        var ids = await SeedAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.SchoolClasses.Add(new SchoolClass { Name = "7А", Parallel = 7, Letter = "А", ShiftId = 1, MaxLessonsPerDay = 7 });
            await db.SaveChangesAsync();
        }
        var otherClass = (await new GroupService(factory).GetClassesAsync()).Single(x => x.Name == "7А");
        var row = new TeachingLoad { TeacherId = ids.Teacher, SubjectId = ids.Subject,
            ClassId = otherClass.Id, GroupId = ids.Group, HoursPerWeek = 1 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TeachingLoadService(factory).SaveAllAsync([row]));
    }

    private static async Task<(int Teacher, int Subject, int Class, int Group, int Room)> SeedAsync(Factory factory)
    {
        await using var db = factory.CreateDbContext();
        var teacher = new Teacher { FullName = "Бакенова Ж.А." };
        var subject = new Subject { Name = "Математика", ShortName = "Матем" };
        var schoolClass = new SchoolClass { Name = "7Б", Parallel = 7, Letter = "Б", ShiftId = 1, MaxLessonsPerDay = 7 };
        var room = new Room { Name = "12" };
        db.AddRange(teacher, subject, schoolClass, room); await db.SaveChangesAsync();
        var group = new SchoolGroup { Name = "Группа 1", ClassId = schoolClass.Id, SubjectId = subject.Id };
        db.Add(group); await db.SaveChangesAsync();
        return (teacher.Id, subject.Id, schoolClass.Id, group.Id, room.Id);
    }

    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source=load-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared").Options;
    private sealed class Factory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options; private readonly AppDbContext _keeper;
        public Factory(DbContextOptions<AppDbContext> options) { _options = options; _keeper = new(options); _keeper.Database.OpenConnection(); _keeper.Database.EnsureCreated(); }
        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
