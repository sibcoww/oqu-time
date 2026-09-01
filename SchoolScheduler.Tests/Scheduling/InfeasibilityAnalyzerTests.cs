using SchoolScheduler.Scheduling.Diagnostics;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class InfeasibilityAnalyzerTests
{
    [Fact]
    public void TeacherCapacityReason_ContainsResourceAndSuggestion()
    {
        var slots = Slots(2);
        var problem = Problem([Demand(1, teacher: 7, subject: 1, schoolClass: 1, hours: 2)], slots,
            [new ResourceAvailabilityConstraint(ResourceKind.Teacher, 7, new HashSet<int> { 1 })]);
        var reason = new InfeasibilityAnalyzer().Analyze(problem).Reasons
            .Single(x => x.Code == "TEACHER_CAPACITY_SHORTAGE");
        Assert.Equal(ResourceKind.Teacher, reason.ResourceKind);
        Assert.Equal(7, reason.ResourceId);
        Assert.Contains("Расширьте доступность", reason.Suggestion);
    }

    [Fact]
    public void FixedConflict_ReportsTeacherAndRoom()
    {
        var problem = Problem([
            Demand(1, 10, 1, 1, room: 30), Demand(2, 10, 2, 2, room: 30)
        ], Slots(2), [new FixedAssignmentConstraint(1, 1), new FixedAssignmentConstraint(2, 1)]);
        var reasons = new InfeasibilityAnalyzer().Analyze(problem).Reasons;
        Assert.Contains(reasons, x => x.Code == "FIXED_RESOURCE_CONFLICT" && x.ResourceKind == ResourceKind.Teacher);
        Assert.Contains(reasons, x => x.Code == "FIXED_RESOURCE_CONFLICT" && x.ResourceKind == ResourceKind.Room);
    }

    [Fact]
    public void DifferentFixedGroupsOfSameClass_AreNotReportedAsClassConflict()
    {
        var problem = Problem([
            Demand(1, 10, 1, 1, group: 101), Demand(2, 11, 2, 1, group: 102)
        ], Slots(1), [new FixedAssignmentConstraint(1, 1), new FixedAssignmentConstraint(2, 1)]);
        var reasons = new InfeasibilityAnalyzer().Analyze(problem, false).Reasons;
        Assert.DoesNotContain(reasons, x => x.Code == "FIXED_RESOURCE_CONFLICT" && x.ResourceKind == ResourceKind.Class);
    }

    [Fact]
    public void UnknownCombinedConflict_GetsFallbackAdvice()
    {
        var report = new InfeasibilityAnalyzer().Analyze(Problem([], Slots(1)));
        var reason = Assert.Single(report.Reasons);
        Assert.Equal(InfeasibilityCategory.Unknown, reason.Category);
        Assert.Contains("Ослабьте", reason.Suggestion);
    }

    private static LessonDemand Demand(int id, int teacher, int subject, int schoolClass,
        decimal hours = 1, int? group = null, int? room = null) =>
        new(id, hours, new(teacher, subject, schoolClass, group, room), false, false, 1, string.Empty);
    private static List<TimeSlot> Slots(int count) => Enumerable.Range(1, count)
        .Select(x => new TimeSlot(x, 1, 1, x, default, default, false)).ToList();
    private static SchedulingProblem Problem(IReadOnlyList<LessonDemand> demands, IReadOnlyList<TimeSlot> slots,
        IReadOnlyList<HardConstraint>? constraints = null) => new(demands, slots, constraints ?? [], []);
}
