using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.App;

public sealed class GroupServiceTests
{
    [Fact]
    public async Task TwoGroupsInSameClass_CanHaveDifferentTeacherLoads()
    {
        using var factory = new Factory(Options());
        int classId, subjectId, firstTeacherId, secondTeacherId;
        await using (var db = factory.CreateDbContext())
        {
            var schoolClass = new SchoolClass { Name = "6Б", Parallel = 6, Letter = "Б", ShiftId = 1, MaxLessonsPerDay = 7 };
            var subject = new Subject { Name = "Английский язык", ShortName = "Англ" };
            var firstTeacher = new Teacher { FullName = "Учитель А" };
            var secondTeacher = new Teacher { FullName = "Учитель Б" };
            db.AddRange(schoolClass, subject, firstTeacher, secondTeacher);
            await db.SaveChangesAsync(); classId = schoolClass.Id; subjectId = subject.Id;
            firstTeacherId = firstTeacher.Id; secondTeacherId = secondTeacher.Id;
        }
        var service = new GroupService(factory);
        var first = await service.SaveAsync(new SchoolGroup { Name = "Группа 1", ClassId = classId, SubjectId = subjectId });
        var second = await service.SaveAsync(new SchoolGroup { Name = "Группа 2", ClassId = classId, SubjectId = subjectId });

        await using (var db = factory.CreateDbContext())
        {
            db.TeachingLoads.AddRange(
                new TeachingLoad { ClassId = classId, GroupId = first.Id, SubjectId = subjectId, TeacherId = firstTeacherId, HoursPerWeek = 3 },
                new TeachingLoad { ClassId = classId, GroupId = second.Id, SubjectId = subjectId, TeacherId = secondTeacherId, HoursPerWeek = 3 });
            await db.SaveChangesAsync();
        }

        var groups = await service.GetGroupsAsync();
        Assert.Equal(2, groups.Count);
        await using var check = factory.CreateDbContext();
        Assert.Equal(2, await check.TeachingLoads.Select(x => x.TeacherId).Distinct().CountAsync());
        Assert.Equal(2, await check.TeachingLoads.Select(x => x.GroupId).Distinct().CountAsync());
    }

    [Fact]
    public async Task DuplicateName_IsCheckedWithinParentClassOnly()
    {
        using var factory = new Factory(Options());
        await using (var db = factory.CreateDbContext())
        {
            db.SchoolClasses.AddRange(
                new SchoolClass { Name = "6А", Parallel = 6, Letter = "А", ShiftId = 1, MaxLessonsPerDay = 7 },
                new SchoolClass { Name = "6Б", Parallel = 6, Letter = "Б", ShiftId = 1, MaxLessonsPerDay = 7 });
            await db.SaveChangesAsync();
        }
        var service = new GroupService(factory);
        var classes = await service.GetClassesAsync();
        await service.SaveAsync(new SchoolGroup { ClassId = classes[0].Id, Name = "Группа 1" });
        Assert.True(await service.ExistsAsync(classes[0].Id, " группа 1 "));
        Assert.False(await service.ExistsAsync(classes[1].Id, "Группа 1"));
    }

    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source=group-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared").Options;
    private sealed class Factory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options; private readonly AppDbContext _keeper;
        public Factory(DbContextOptions<AppDbContext> options) { _options = options; _keeper = new(options); _keeper.Database.OpenConnection(); _keeper.Database.EnsureCreated(); }
        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
