using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class TimeModelStabilizationTests
{
    [Theory]
    [InlineData(5, 10)]
    [InlineData(6, 12)]
    public void WorkWeek_BuildsConfiguredNumberOfDays(int days, int expectedSlots)
    {
        var problem = Create(days, Period(11, 1, 1), Period(12, 1, 2));
        Assert.Equal(expectedSlots, problem.TimeSlots.Count);
        Assert.Equal(days, problem.TimeSlots.Max(x => x.DayOfWeek));
    }

    [Fact]
    public void TwoShifts_KeepSameLessonNumberAsDifferentPeriods()
    {
        var problem = Create(5, Period(11, 1, 1), Period(21, 2, 1));
        var firstLessons = problem.TimeSlots.Where(x => x.LessonNumber == 1 && x.DayOfWeek == 1).ToList();
        Assert.Equal(2, firstLessons.Count);
        Assert.Equal(2, firstLessons.Select(x => x.ShiftId).Distinct().Count());
    }

    [Fact]
    public void TeacherAvailability_IsDifferentForEachShift()
    {
        var periods = new[] { Period(11, 1, 1), Period(21, 2, 1) };
        var source = Source(periods, [new TeacherAvailability { TeacherId = 1, DayOfWeek = 1, LessonPeriodId = 21, IsAvailable = true }], []);
        var problem = new SchedulingProblemFactory().Create(source);
        var allowed = problem.HardConstraints.OfType<ResourceAvailabilityConstraint>().Single(x => x.ResourceKind == ResourceKind.Teacher).AllowedTimeSlotIds;
        Assert.All(problem.TimeSlots.Where(x => allowed.Contains(x.Id)), x => Assert.Equal(2, x.ShiftId));
    }

    [Fact]
    public void RoomAvailability_IsDifferentForEachShift()
    {
        var periods = new[] { Period(11, 1, 1), Period(21, 2, 1) };
        var source = Source(periods, [], [new RoomAvailability { RoomId = 1, DayOfWeek = 1, LessonPeriodId = 11, IsAvailable = true }]);
        var problem = new SchedulingProblemFactory().Create(source);
        var allowed = problem.HardConstraints.OfType<ResourceAvailabilityConstraint>().Single(x => x.ResourceKind == ResourceKind.Room).AllowedTimeSlotIds;
        Assert.All(problem.TimeSlots.Where(x => allowed.Contains(x.Id)), x => Assert.Equal(1, x.ShiftId));
    }

    [Fact]
    public void LessonPeriod_PreservesStartAndEndTime()
    {
        var period = new LessonPeriod { Id = 11, ShiftId = 1, Number = 1, StartTime = new(8, 15, 0), EndTime = new(9, 0, 0) };
        var slot = Assert.Single(Create(1, period).TimeSlots);
        Assert.Equal(new TimeSpan(8, 15, 0), slot.StartTime);
        Assert.Equal(new TimeSpan(9, 0, 0), slot.EndTime);
    }

    private static SchedulingProblem Create(int days, params LessonPeriod[] periods) =>
        new SchedulingProblemFactory().Create(Source(periods, [], [], days));
    private static SchedulingSource Source(IReadOnlyCollection<LessonPeriod> periods,
        IReadOnlyCollection<TeacherAvailability> teachers, IReadOnlyCollection<RoomAvailability> rooms, int days = 1) =>
        new([new TeachingLoad { Id = 1, TeacherId = 1, SubjectId = 1, ClassId = 1, RoomId = 1, HoursPerWeek = 1 }],
            [new SchoolClass { Id = 1, ShiftId = 1, MaxLessonsPerDay = 8 }], [new Subject { Id = 1 }], periods, teachers, rooms, days);
    private static LessonPeriod Period(int id, int shift, int number) => new()
    { Id = id, ShiftId = shift, Number = number, StartTime = new(8 + shift, 0, 0), EndTime = new(8 + shift, 45, 0) };
}
