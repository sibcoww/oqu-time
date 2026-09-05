using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class SchedulingProblemFactoryTests
{
    [Fact]
    public void Create_MapsOrmModelsToIndependentSchedulingValues()
    {
        var load = new TeachingLoad { Id = 7, TeacherId = 1, SubjectId = 2, ClassId = 3,
            GroupId = 4, RoomId = 5, HoursPerWeek = 0.5m, AllowZeroLesson = true, Comment = "Исходное" };
        var subject = new Subject { Id = 2, AllowDoubleLessons = true };
        var source = new SchedulingSource([load],
            [new SchoolClass { Id = 3, ShiftId = 1, MaxLessonsPerDay = 2 }], [subject],
            [new LessonPeriod { ShiftId = 1, Number = 1 }, new LessonPeriod { ShiftId = 1, Number = 2 }],
            [], [], 5);

        var problem = new SchedulingProblemFactory().Create(source);
        load.Comment = "Изменено"; load.HoursPerWeek = 10; subject.AllowDoubleLessons = false;

        var demand = Assert.Single(problem.Demands);
        Assert.Equal(0.5m, demand.WeeklyHours);
        Assert.Equal("Исходное", demand.Comment);
        Assert.True(demand.AllowDoubleLessons);
        Assert.Equal(1, demand.SubjectDifficulty);
        Assert.Equal(40, problem.TimeSlots.Count);
        Assert.Equal(4, problem.TimeSlots.Max(x => x.CycleWeek));
        Assert.Equal(4, demand.Resources.GroupId);
    }

    [Fact]
    public void Create_MapsAvailabilityAndFixedAssignmentToHardConstraints()
    {
        var source = new SchedulingSource(
            [new TeachingLoad { Id = 1, TeacherId = 10, SubjectId = 20, ClassId = 30, RoomId = 40, HoursPerWeek = 1 }],
            [new SchoolClass { Id = 30, ShiftId = 2, MaxLessonsPerDay = 3 }],
            [new Subject { Id = 20 }],
            [new LessonPeriod { Id = 21, ShiftId = 2, Number = 1 }, new LessonPeriod { Id = 22, ShiftId = 2, Number = 2 }],
            [new TeacherAvailability { TeacherId = 10, DayOfWeek = 1, LessonPeriodId = 22, IsAvailable = false }],
            [new RoomAvailability { RoomId = 40, DayOfWeek = 2, LessonPeriodId = 21, IsAvailable = true }],
            2, [new FixedLessonAssignment(1, 10, 30, 40, 2, 1)]);

        var problem = new SchedulingProblemFactory().Create(source);
        var teacher = problem.HardConstraints.OfType<ResourceAvailabilityConstraint>()
            .Single(x => x.ResourceKind == ResourceKind.Teacher);
        Assert.Equal(3, teacher.AllowedTimeSlotIds.Count);
        Assert.DoesNotContain(problem.TimeSlots.Single(x => x.DayOfWeek == 1 && x.LessonNumber == 2).Id,
            teacher.AllowedTimeSlotIds);
        Assert.Single(problem.HardConstraints.OfType<FixedAssignmentConstraint>());
        Assert.Equal(6, problem.SoftConstraints.Count);
    }

    [Fact]
    public void ScheduleCandidate_StoresScoreBreakdownAndDiagnostics()
    {
        var score = new ScheduleScore(12, new Dictionary<string, int> { ["MINIMIZE_TEACHER_GAPS"] = 12 });
        var candidate = new ScheduleCandidate([new ScheduledLesson(1, 0, 5)], score, true, []);
        Assert.True(candidate.IsFeasible);
        Assert.Equal(12, candidate.Score.TotalPenalty);
        Assert.Equal(5, candidate.Lessons.Single().TimeSlotId);
    }

    [Fact]
    public void MissingTeacherAvailabilityForNewZeroPeriod_IsAvailable()
    {
        var problem = AvailabilityProblem(
            Enumerable.Range(1, 6).Select(n => new TeacherAvailability
                { TeacherId = 10, DayOfWeek = 1, LessonPeriodId = n, IsAvailable = true }).ToArray(), []);
        Assert.True(IsAllowed(problem, ResourceKind.Teacher, 10, 0));
    }

    [Fact]
    public void ExplicitlyUnavailableTeacherZeroPeriod_IsForbidden()
    {
        var problem = AvailabilityProblem(
            [new TeacherAvailability { TeacherId = 10, DayOfWeek = 1, LessonPeriodId = 100, IsAvailable = false }], []);
        Assert.False(IsAllowed(problem, ResourceKind.Teacher, 10, 0));
    }

    [Fact]
    public void RoomZeroPeriod_UsesMissingAvailableAndExplicitFalseSemantics()
    {
        var missing = AvailabilityProblem([], Enumerable.Range(1, 6).Select(n => new RoomAvailability
            { RoomId = 40, DayOfWeek = 1, LessonPeriodId = n, IsAvailable = true }).ToArray());
        Assert.True(IsAllowed(missing, ResourceKind.Room, 40, 0));
        var forbidden = AvailabilityProblem([],
            [new RoomAvailability { RoomId = 40, DayOfWeek = 1, LessonPeriodId = 100, IsAvailable = false }]);
        Assert.False(IsAllowed(forbidden, ResourceKind.Room, 40, 0));
    }

    private static SchedulingProblem AvailabilityProblem(IReadOnlyCollection<TeacherAvailability> teachers,
        IReadOnlyCollection<RoomAvailability> rooms)
    {
        var periods = Enumerable.Range(1, 6).Select(n => new LessonPeriod { Id = n, ShiftId = 1, Number = n })
            .Prepend(new LessonPeriod { Id = 100, ShiftId = 1, Number = 0 }).ToArray();
        return new SchedulingProblemFactory().Create(new(
            [new TeachingLoad { Id = 1, TeacherId = 10, SubjectId = 20, ClassId = 30, RoomId = 40, HoursPerWeek = 1 }],
            [new SchoolClass { Id = 30, ShiftId = 1, MaxLessonsPerDay = 6 }], [new Subject { Id = 20 }],
            periods, teachers, rooms, 1));
    }

    private static bool IsAllowed(SchedulingProblem problem, ResourceKind kind, int resourceId, int lessonNumber)
    {
        var slot = problem.TimeSlots.Single(x => x.LessonNumber == lessonNumber);
        return problem.HardConstraints.OfType<ResourceAvailabilityConstraint>()
            .Single(x => x.ResourceKind == kind && x.ResourceId == resourceId).AllowedTimeSlotIds.Contains(slot.Id);
    }
}
