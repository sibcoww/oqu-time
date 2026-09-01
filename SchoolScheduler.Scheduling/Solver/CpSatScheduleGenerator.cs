using Google.OrTools.Sat;
using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Diagnostics;

namespace SchoolScheduler.Scheduling.Solver;

public sealed class CpSatScheduleGenerator : IScheduleGenerator
{
    public ScheduleCandidate Generate(SchedulingProblem problem, TimeSpan? timeLimit = null)
    {
        var analyzer = new InfeasibilityAnalyzer();
        var preflight = analyzer.Analyze(problem, false);
        if (preflight.Reasons.Count > 0) return Infeasible(preflight);

        var model = new CpModel();
        var variables = new Dictionary<(int DemandIndex, int SlotId), BoolVar>();
        var teacherAvailability = Availability(problem, ResourceKind.Teacher);
        var classAvailability = Availability(problem, ResourceKind.Class);
        var roomAvailability = Availability(problem, ResourceKind.Room);

        for (var demandIndex = 0; demandIndex < problem.Demands.Count; demandIndex++)
        {
            var demand = problem.Demands[demandIndex];
            var demandVariables = new List<BoolVar>();
            foreach (var slot in problem.TimeSlots)
            {
                var variable = model.NewBoolVar($"d{demandIndex}_s{slot.Id}");
                variables[(demandIndex, slot.Id)] = variable;
                demandVariables.Add(variable);
                if (!IsAllowed(demand, slot, teacherAvailability, classAvailability, roomAvailability)) model.Add(variable == 0);
            }
            model.Add(LinearExpr.Sum(demandVariables) == decimal.ToInt32(demand.WeeklyHours));
        }

        foreach (var group in problem.Demands.Select((d, i) => (Demand: d, Index: i)).GroupBy(x => x.Demand.Resources.TeacherId))
            foreach (var slot in problem.TimeSlots)
                model.Add(LinearExpr.Sum(group.Select(x => variables[(x.Index, slot.Id)])) <= 1);
        foreach (var classGroup in problem.Demands.Select((d, i) => (Demand: d, Index: i)).GroupBy(x => x.Demand.Resources.ClassId))
            foreach (var slot in problem.TimeSlots)
            {
                var wholeClass = classGroup.Where(x => !x.Demand.Resources.GroupId.HasValue).ToList();
                if (wholeClass.Count > 0) model.Add(LinearExpr.Sum(wholeClass.Select(x => variables[(x.Index, slot.Id)])) <= 1);
                foreach (var lessonGroup in classGroup.Where(x => x.Demand.Resources.GroupId.HasValue)
                             .GroupBy(x => x.Demand.Resources.GroupId!.Value))
                    model.Add(LinearExpr.Sum(wholeClass.Concat(lessonGroup).Select(x => variables[(x.Index, slot.Id)])) <= 1);
            }
        foreach (var roomGroup in problem.Demands.Select((d, i) => (Demand: d, Index: i))
                     .Where(x => x.Demand.Resources.RoomId.HasValue).GroupBy(x => x.Demand.Resources.RoomId!.Value))
            foreach (var slot in problem.TimeSlots)
                model.Add(LinearExpr.Sum(roomGroup.Select(x => variables[(x.Index, slot.Id)])) <= 1);

        foreach (var fixedAssignment in problem.HardConstraints.OfType<FixedAssignmentConstraint>())
        {
            var demandIndex = problem.Demands.ToList().FindIndex(x => x.Id == fixedAssignment.LessonDemandId);
            if (demandIndex >= 0 && variables.TryGetValue((demandIndex, fixedAssignment.TimeSlotId), out var variable))
                model.Add(variable == 1);
        }

        var penaltyTerms = SoftConstraintModelBuilder.Add(model, problem, variables);
        if (penaltyTerms.Count > 0)
            model.Minimize(LinearExpr.Sum(penaltyTerms.Select(x => LinearExpr.Term(x.Variable, x.Weight))));

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{(timeLimit ?? TimeSpan.FromSeconds(10)).TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        var status = solver.Solve(model);
        if (status is not CpSolverStatus.Feasible and not CpSolverStatus.Optimal)
            return Infeasible(analyzer.Analyze(problem));

        var lessons = new List<ScheduledLesson>();
        for (var demandIndex = 0; demandIndex < problem.Demands.Count; demandIndex++)
        {
            var occurrence = 0;
            foreach (var slot in problem.TimeSlots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.LessonNumber))
                if (solver.Value(variables[(demandIndex, slot.Id)]) == 1)
                    lessons.Add(new(problem.Demands[demandIndex].Id, occurrence++, slot.Id));
        }
        var penalties = penaltyTerms.GroupBy(x => x.Code).ToDictionary(x => x.Key,
            x => x.Sum(term => checked((int)solver.Value(term.Variable) * term.Weight)));
        return new(lessons, new(penalties.Values.Sum(), penalties), true, []);
    }

    private static Dictionary<int, IReadOnlySet<int>> Availability(SchedulingProblem problem, ResourceKind kind) =>
        problem.HardConstraints.OfType<ResourceAvailabilityConstraint>().Where(x => x.ResourceKind == kind)
            .GroupBy(x => x.ResourceId).ToDictionary(x => x.Key, x => (IReadOnlySet<int>)x.SelectMany(v => v.AllowedTimeSlotIds).ToHashSet());

    private static bool IsAllowed(LessonDemand demand, TimeSlot slot,
        IReadOnlyDictionary<int, IReadOnlySet<int>> teacherAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> classAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> roomAvailability)
    {
        if (slot.IsZeroLesson && !demand.AllowZeroLesson) return false;
        if (teacherAvailability.TryGetValue(demand.Resources.TeacherId, out var teacherSlots) && !teacherSlots.Contains(slot.Id)) return false;
        if (classAvailability.TryGetValue(demand.Resources.ClassId, out var classSlots) && !classSlots.Contains(slot.Id)) return false;
        if (demand.Resources.RoomId.HasValue && roomAvailability.TryGetValue(demand.Resources.RoomId.Value, out var roomSlots) && !roomSlots.Contains(slot.Id)) return false;
        return true;
    }

    private static ScheduleCandidate Infeasible(InfeasibilityReport report) =>
        new([], ScheduleScore.Empty, false, report.Reasons.Select(x => x.Message).ToList(), report);
}
