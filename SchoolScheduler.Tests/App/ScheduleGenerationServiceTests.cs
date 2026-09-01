using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;
using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Solver;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.Tests.App;

public sealed class ScheduleGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesDatabaseAndReturnsRealSolverResult()
    {
        using var factory = new Factory(Options());
        await using (var db = factory.CreateDbContext())
        {
            var school = new School { Name = "Школа", DaysPerWeek = 2 };
            var shift = new Shift { Name = "Смена 1" }; db.AddRange(school, shift); await db.SaveChangesAsync();
            db.LessonPeriods.AddRange(new LessonPeriod { ShiftId = shift.Id, Number = 1 }, new LessonPeriod { ShiftId = shift.Id, Number = 2 });
            var teacher = new Teacher { FullName = "Иванова А.А." };
            var subject = new Subject { Name = "Математика", ShortName = "Матем", Difficulty = 8 };
            var schoolClass = new SchoolClass { Name = "7Б", Parallel = 7, Letter = "Б", ShiftId = shift.Id, MaxLessonsPerDay = 2 };
            db.AddRange(teacher, subject, schoolClass); await db.SaveChangesAsync();
            db.TeachingLoads.Add(new TeachingLoad { TeacherId = teacher.Id, SubjectId = subject.Id, ClassId = schoolClass.Id, HoursPerWeek = 2 });
            await db.SaveChangesAsync();
        }
        var service = new ScheduleGenerationService(factory, new PreScheduleValidator(),
            new SchedulingProblemFactory(), new CpSatScheduleGenerator());
        var result = await service.GenerateAsync();
        Assert.True(result.Candidate.IsFeasible, string.Join(Environment.NewLine, result.Candidate.Diagnostics));
        Assert.Equal(2, result.Candidate.Lessons.Count);
        Assert.Equal("7Б", Assert.Single(result.Classes).Name);

        var preservedLesson = result.Candidate.Lessons[0];
        var optimized = await service.ReoptimizeAsync(result,
            [new(preservedLesson.LessonDemandId, preservedLesson.OccurrenceIndex, preservedLesson.TimeSlotId)]);
        Assert.True(optimized.Candidate.IsFeasible);
        Assert.Contains(optimized.Candidate.Lessons, x =>
            x.LessonDemandId == preservedLesson.LessonDemandId && x.TimeSlotId == preservedLesson.TimeSlotId);
        Assert.Empty(optimized.Problem.HardConstraints.OfType<FixedAssignmentConstraint>());
    }

    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source=schedule-generation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared").Options;
    private sealed class Factory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options; private readonly AppDbContext _keeper;
        public Factory(DbContextOptions<AppDbContext> options) { _options = options; _keeper = new(options); _keeper.Database.OpenConnection(); _keeper.Database.EnsureCreated(); }
        public AppDbContext CreateDbContext() => new(_options);
        public void Dispose() => _keeper.Dispose();
    }
}
