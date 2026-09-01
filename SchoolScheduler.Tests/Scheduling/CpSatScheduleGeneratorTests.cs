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

    private static LessonDemand Demand(int id, int teacher, int subject, int schoolClass, decimal hours) =>
        new(id, hours, new(teacher, subject, schoolClass, null, null), false, false, string.Empty);

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
