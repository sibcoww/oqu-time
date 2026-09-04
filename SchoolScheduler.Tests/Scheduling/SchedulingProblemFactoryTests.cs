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
        Assert.Equal(10, problem.TimeSlots.Count);
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
            [new TeacherAvailability { TeacherId = 10, DayOfWeek = 1, LessonPeriodId = 22, IsAvailable = true }],
            [new RoomAvailability { RoomId = 40, DayOfWeek = 2, LessonPeriodId = 21, IsAvailable = true }],
            2, [new FixedLessonAssignment(1, 10, 30, 40, 2, 1)]);

        var problem = new SchedulingProblemFactory().Create(source);
        var teacher = problem.HardConstraints.OfType<ResourceAvailabilityConstraint>()
            .Single(x => x.ResourceKind == ResourceKind.Teacher);
        var teacherSlot = Assert.Single(teacher.AllowedTimeSlotIds);
        Assert.Equal((1, 2), problem.TimeSlots.Where(x => x.Id == teacherSlot)
            .Select(x => (x.DayOfWeek, x.LessonNumber)).Single());
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
}
