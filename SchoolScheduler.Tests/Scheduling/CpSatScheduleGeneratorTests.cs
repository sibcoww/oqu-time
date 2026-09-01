using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Solver;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class CpSatScheduleGeneratorTests
{
    [Fact]
    public void DatasetA_MinimalSchool_DistributesEveryRequiredLesson()
    {
        var problem = Problem(
            [Demand(1, 1, 1, 1, 3), Demand(2, 2, 2, 1, 2),
             Demand(3, 1, 3, 2, 2), Demand(4, 3, 4, 2, 3)],
            Slots(5, 3));
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(10, result.Lessons.Count);
        AssertNoTeacherOrClassOverlap(problem, result);
    }

    [Fact]
    public void DatasetB_OneTeacherForTwoClasses_HasNoDoubleAssignment()
    {
        var problem = Problem([Demand(1, 1, 1, 1, 2), Demand(2, 1, 2, 2, 2)], Slots(2, 2));
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible, string.Join(Environment.NewLine, result.Diagnostics));
        AssertNoTeacherOrClassOverlap(problem, result);
        Assert.Equal(4, result.Lessons.Select(x => x.TimeSlotId).Distinct().Count());
    }

    [Fact]
    public void DatasetE_ContradictoryAvailability_ReturnsReadableDiagnostic()
    {
        var slots = Slots(1, 2);
        var problem = Problem([Demand(1, 1, 1, 1, 2)], slots,
            [new ResourceAvailabilityConstraint(ResourceKind.Teacher, 1, new HashSet<int> { slots[0].Id })]);
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.False(result.IsFeasible);
        Assert.Contains(result.Diagnostics, x => x.Contains("только 1 доступных слотов") || x.Contains("только 1 слотов"));
    }

    [Fact]
    public void FractionalHours_AreRejectedWithoutSilentRounding()
    {
        var result = new CpSatScheduleGenerator().Generate(Problem([Demand(1, 1, 1, 1, 0.5m)], Slots(5, 2)));
        Assert.False(result.IsFeasible);
        Assert.Contains(result.Diagnostics, x => x.Contains("правила дробных занятий"));
    }

    [Fact]
    public void DatasetC_SameRoomCannotBeUsedByTwoLessonsAtOnce()
    {
        var problem = Problem([Demand(1, 1, 1, 1, 1, room: 10), Demand(2, 2, 2, 2, 1, room: 10)], Slots(1, 2));
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(2, result.Lessons.Select(x => x.TimeSlotId).Distinct().Count());
    }

    [Fact]
    public void AssignedRoomAvailability_IsHardConstraint()
    {
        var slots = Slots(1, 2);
        var problem = Problem([Demand(1, 1, 1, 1, 1, room: 10)], slots,
            [new ResourceAvailabilityConstraint(ResourceKind.Room, 10, new HashSet<int> { slots[1].Id })]);
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible);
        Assert.Equal(slots[1].Id, Assert.Single(result.Lessons).TimeSlotId);
    }

    [Fact]
    public void DatasetD_DifferentGroupsCanStudyInParallel()
    {
        var slots = Slots(1, 1);
        var problem = Problem([
            Demand(1, 1, 1, 1, 1, group: 101, room: 10),
            Demand(2, 2, 1, 1, 1, group: 102, room: 11)
        ], slots);
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(2, result.Lessons.Count);
        Assert.Single(result.Lessons.Select(x => x.TimeSlotId).Distinct());
    }

    [Fact]
    public void WholeClassLessonConflictsWithEveryGroupLesson()
    {
        var result = new CpSatScheduleGenerator().Generate(Problem([
            Demand(1, 1, 1, 1, 1), Demand(2, 2, 2, 1, 1, group: 101)
        ], Slots(1, 1)));
        Assert.False(result.IsFeasible);
    }

    [Fact]
    public void ClassShiftAvailability_RestrictsLessonToItsShiftSlots()
    {
        var slots = new List<TimeSlot>
        {
            new(1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false),
            new(2, 2, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false)
        };
        var problem = Problem([Demand(1, 1, 1, 1, 1)], slots,
            [new ResourceAvailabilityConstraint(ResourceKind.Class, 1, new HashSet<int> { 2 })]);
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible);
        Assert.Equal(2, Assert.Single(result.Lessons).TimeSlotId);
    }

    [Fact]
    public void FixedLesson_IsPlacedIntoRequiredSlot()
    {
        var problem = Problem([Demand(7, 1, 1, 1, 1)], Slots(2, 2),
            [new FixedAssignmentConstraint(7, 4)]);
        var result = new CpSatScheduleGenerator().Generate(problem);
        Assert.True(result.IsFeasible);
        Assert.Equal(4, Assert.Single(result.Lessons).TimeSlotId);
    }

    private static LessonDemand Demand(int id, int teacher, int subject, int schoolClass, decimal hours,
        int? group = null, int? room = null) =>
        new(id, hours, new(teacher, subject, schoolClass, group, room), false, false, string.Empty);

    private static List<TimeSlot> Slots(int days, int lessons) =>
        (from day in Enumerable.Range(1, days) from lesson in Enumerable.Range(1, lessons)
         select new TimeSlot((day - 1) * lessons + lesson, 1, day, lesson, TimeSpan.Zero, TimeSpan.Zero, false)).ToList();

    private static SchedulingProblem Problem(IReadOnlyList<LessonDemand> demands, IReadOnlyList<TimeSlot> slots,
        IReadOnlyList<HardConstraint>? extra = null)
    {
        var constraints = new List<HardConstraint>
        { new NoResourceOverlapConstraint(ResourceKind.Teacher), new NoResourceOverlapConstraint(ResourceKind.Class) };
        if (extra is not null) constraints.AddRange(extra);
        return new(demands, slots, constraints, []);
    }

    private static void AssertNoTeacherOrClassOverlap(SchedulingProblem problem, ScheduleCandidate result)
    {
        var demands = problem.Demands.ToDictionary(x => x.Id);
        Assert.All(result.Lessons.GroupBy(x => (x.TimeSlotId, demands[x.LessonDemandId].Resources.TeacherId)), x => Assert.Single(x));
        Assert.All(result.Lessons.GroupBy(x => (x.TimeSlotId, demands[x.LessonDemandId].Resources.ClassId)), x => Assert.Single(x));
    }
}
