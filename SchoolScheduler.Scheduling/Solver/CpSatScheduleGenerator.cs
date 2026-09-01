using Google.OrTools.Sat;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Scheduling.Solver;

public sealed class CpSatScheduleGenerator : IScheduleGenerator
{
    public ScheduleCandidate Generate(SchedulingProblem problem, TimeSpan? timeLimit = null)
    {
        var diagnostics = ValidateDiscreteProblem(problem);
        if (diagnostics.Count > 0) return Infeasible(diagnostics);

        var model = new CpModel();
        var variables = new Dictionary<(int DemandIndex, int SlotId), BoolVar>();
        var teacherAvailability = Availability(problem, ResourceKind.Teacher);
        var classAvailability = Availability(problem, ResourceKind.Class);

        for (var demandIndex = 0; demandIndex < problem.Demands.Count; demandIndex++)
        {
            var demand = problem.Demands[demandIndex];
            var demandVariables = new List<BoolVar>();
            foreach (var slot in problem.TimeSlots)
            {
                var variable = model.NewBoolVar($"d{demandIndex}_s{slot.Id}");
                variables[(demandIndex, slot.Id)] = variable;
                demandVariables.Add(variable);
                if (!IsAllowed(demand, slot, teacherAvailability, classAvailability)) model.Add(variable == 0);
            }
            model.Add(LinearExpr.Sum(demandVariables) == decimal.ToInt32(demand.WeeklyHours));
        }

        foreach (var group in problem.Demands.Select((d, i) => (Demand: d, Index: i)).GroupBy(x => x.Demand.Resources.TeacherId))
            foreach (var slot in problem.TimeSlots)
                model.Add(LinearExpr.Sum(group.Select(x => variables[(x.Index, slot.Id)])) <= 1);
        foreach (var group in problem.Demands.Select((d, i) => (Demand: d, Index: i)).GroupBy(x => x.Demand.Resources.ClassId))
            foreach (var slot in problem.TimeSlots)
                model.Add(LinearExpr.Sum(group.Select(x => variables[(x.Index, slot.Id)])) <= 1);

        foreach (var fixedAssignment in problem.HardConstraints.OfType<FixedAssignmentConstraint>())
        {
            var demandIndex = problem.Demands.ToList().FindIndex(x => x.Id == fixedAssignment.LessonDemandId);
            if (demandIndex >= 0 && variables.TryGetValue((demandIndex, fixedAssignment.TimeSlotId), out var variable))
                model.Add(variable == 1);
        }

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{(timeLimit ?? TimeSpan.FromSeconds(10)).TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        var status = solver.Solve(model);
        if (status is not CpSolverStatus.Feasible and not CpSolverStatus.Optimal)
            return Infeasible(BuildInfeasibleDiagnostics(problem, teacherAvailability, classAvailability, status));

        var lessons = new List<ScheduledLesson>();
        for (var demandIndex = 0; demandIndex < problem.Demands.Count; demandIndex++)
        {
            var occurrence = 0;
            foreach (var slot in problem.TimeSlots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.LessonNumber))
                if (solver.Value(variables[(demandIndex, slot.Id)]) == 1)
                    lessons.Add(new(problem.Demands[demandIndex].Id, occurrence++, slot.Id));
        }
        return new(lessons, ScheduleScore.Empty, true, []);
    }

    private static Dictionary<int, IReadOnlySet<int>> Availability(SchedulingProblem problem, ResourceKind kind) =>
        problem.HardConstraints.OfType<ResourceAvailabilityConstraint>().Where(x => x.ResourceKind == kind)
            .GroupBy(x => x.ResourceId).ToDictionary(x => x.Key, x => (IReadOnlySet<int>)x.SelectMany(v => v.AllowedTimeSlotIds).ToHashSet());

    private static bool IsAllowed(LessonDemand demand, TimeSlot slot,
        IReadOnlyDictionary<int, IReadOnlySet<int>> teacherAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> classAvailability)
    {
        if (slot.IsZeroLesson && !demand.AllowZeroLesson) return false;
        if (teacherAvailability.TryGetValue(demand.Resources.TeacherId, out var teacherSlots) && !teacherSlots.Contains(slot.Id)) return false;
        if (classAvailability.TryGetValue(demand.Resources.ClassId, out var classSlots) && !classSlots.Contains(slot.Id)) return false;
        return true;
    }

    private static List<string> ValidateDiscreteProblem(SchedulingProblem problem)
    {
        var diagnostics = new List<string>();
        if (problem.TimeSlots.Count == 0) diagnostics.Add("Не задано ни одного временного слота.");
        foreach (var demand in problem.Demands)
        {
            if (demand.WeeklyHours <= 0) diagnostics.Add($"Нагрузка #{demand.Id}: количество часов должно быть больше нуля.");
            if (demand.WeeklyHours != decimal.Truncate(demand.WeeklyHours))
                diagnostics.Add($"Нагрузка #{demand.Id}: {demand.WeeklyHours:0.##} часа нельзя распределить в недельной сетке без правила дробных занятий.");
        }
        return diagnostics;
    }

    private static IReadOnlyList<string> BuildInfeasibleDiagnostics(SchedulingProblem problem,
        IReadOnlyDictionary<int, IReadOnlySet<int>> teacherAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> classAvailability, CpSolverStatus status)
    {
        var diagnostics = new List<string>();
        foreach (var demand in problem.Demands)
        {
            var allowed = problem.TimeSlots.Count(slot => IsAllowed(demand, slot, teacherAvailability, classAvailability));
            if (allowed < demand.WeeklyHours) diagnostics.Add($"Нагрузка #{demand.Id} требует {demand.WeeklyHours:0} уроков, но имеет только {allowed} доступных слотов.");
        }
        foreach (var teacher in problem.Demands.GroupBy(x => x.Resources.TeacherId))
        {
            var required = teacher.Sum(x => x.WeeklyHours);
            var available = teacherAvailability.TryGetValue(teacher.Key, out var slots) ? slots.Count : problem.TimeSlots.Count;
            if (required > available) diagnostics.Add($"Учителю #{teacher.Key} требуется {required:0} уроков, но доступно только {available} слотов.");
        }
        if (diagnostics.Count == 0) diagnostics.Add($"CP-SAT не нашёл допустимое расписание. Статус: {status}.");
        return diagnostics;
    }

    private static ScheduleCandidate Infeasible(IReadOnlyList<string> diagnostics) =>
        new([], ScheduleScore.Empty, false, diagnostics);
}
