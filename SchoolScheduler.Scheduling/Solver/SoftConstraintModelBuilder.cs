using Google.OrTools.Sat;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Scheduling.Solver;

internal sealed record PenaltyTerm(string Code, IntVar Variable, int Weight);

internal static class SoftConstraintModelBuilder
{
    public static List<PenaltyTerm> Add(CpModel model, SchedulingProblem problem,
        IReadOnlyDictionary<(int DemandIndex, int SlotId), BoolVar> variables)
    {
        var terms = new List<PenaltyTerm>();
        AddTeacherGaps(model, problem, variables, terms);
        AddClassBalance(model, problem, variables, terms);
        AddSubjectSpread(model, problem, variables, terms);
        AddDifficultConsecutive(model, problem, variables, terms);
        AddEdgeAndEarlyLessons(problem, variables, terms);
        AddUserPreferences(problem, variables, terms);
        return terms;
    }

    private static void AddTeacherGaps(CpModel model, SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        var weight = Weight<MinimizeTeacherGapsConstraint>(problem); if (weight <= 0) return;
        foreach (var teacher in Indexed(problem).GroupBy(x => x.Demand.Resources.TeacherId))
        foreach (var slotGroup in problem.TimeSlots.GroupBy(x => (x.CycleWeek, x.DayOfWeek, x.ShiftId)))
        {
            var slots = slotGroup.OrderBy(x => x.LessonNumber).ToList();
            var occupied = slots.Select(slot => Occupancy(model, teacher.Select(x => variables[(x.Index, slot.Id)]), $"t{teacher.Key}_s{slot.Id}")).ToList();
            for (var i = 1; i < occupied.Count - 1; i++)
            {
                var before = model.NewBoolVar($"tb{teacher.Key}_{slotGroup.Key}_{i}");
                var after = model.NewBoolVar($"ta{teacher.Key}_{slotGroup.Key}_{i}");
                model.AddMaxEquality(before, occupied.Take(i)); model.AddMaxEquality(after, occupied.Skip(i + 1));
                var gap = model.NewBoolVar($"tg{teacher.Key}_{slotGroup.Key}_{i}");
                model.Add(gap <= before); model.Add(gap <= after); model.Add(gap + occupied[i] <= 1);
                model.Add(gap >= before + after - occupied[i] - 1);
                terms.Add(new("MINIMIZE_TEACHER_GAPS", gap, weight));
            }
        }
    }

    private static void AddClassBalance(CpModel model, SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        var weight = Weight<BalanceClassDayConstraint>(problem); if (weight <= 0) return;
        foreach (var schoolClass in Indexed(problem).GroupBy(x => x.Demand.Resources.ClassId))
        {
            foreach (var week in problem.TimeSlots.Select(x => x.CycleWeek).Distinct())
            {
            var counts = problem.TimeSlots.Where(x => x.CycleWeek == week).Select(x => x.DayOfWeek).Distinct().Order().Select(day =>
            {
                var count = model.NewIntVar(0, schoolClass.Count() * problem.TimeSlots.Count, $"cd{schoolClass.Key}_{week}_{day}");
                model.Add(count == LinearExpr.Sum(from demand in schoolClass from slot in problem.TimeSlots
                    where slot.CycleWeek == week && slot.DayOfWeek == day select variables[(demand.Index, slot.Id)])); return count;
            }).ToList();
            if (counts.Count < 2) continue;
            var max = model.NewIntVar(0, 1000, $"cmax{schoolClass.Key}"); var min = model.NewIntVar(0, 1000, $"cmin{schoolClass.Key}");
            model.AddMaxEquality(max, counts); model.AddMinEquality(min, counts);
            var spread = model.NewIntVar(0, 1000, $"cspread{schoolClass.Key}"); model.Add(spread == max - min);
            terms.Add(new("BALANCE_CLASS_DAY", spread, weight));
            }
        }
    }

    private static void AddSubjectSpread(CpModel model, SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        var weight = Weight<SpreadSubjectAcrossWeekConstraint>(problem); if (weight <= 0) return;
        foreach (var subject in Indexed(problem).GroupBy(x => (x.Demand.Resources.ClassId, x.Demand.Resources.SubjectId)))
        foreach (var weekDay in problem.TimeSlots.Select(x => (x.CycleWeek, x.DayOfWeek)).Distinct())
        {
            var count = model.NewIntVar(0, 1000, $"sd{subject.Key}_{weekDay}");
            model.Add(count == LinearExpr.Sum(from demand in subject from slot in problem.TimeSlots
                where slot.CycleWeek == weekDay.CycleWeek && slot.DayOfWeek == weekDay.DayOfWeek select variables[(demand.Index, slot.Id)]));
            var excess = model.NewIntVar(0, 1000, $"se{subject.Key}_{weekDay}"); model.Add(excess >= count - 1);
            terms.Add(new("SPREAD_SUBJECT_WEEK", excess, weight));
        }
    }

    private static void AddDifficultConsecutive(CpModel model, SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        var rule = problem.SoftConstraints.OfType<AvoidConsecutiveDifficultSubjectsConstraint>().FirstOrDefault(); if (rule is null) return;
        foreach (var schoolClass in Indexed(problem).GroupBy(x => x.Demand.Resources.ClassId))
        foreach (var dayShift in problem.TimeSlots.GroupBy(x => (x.CycleWeek, x.DayOfWeek, x.ShiftId)))
        {
            var slots = dayShift.OrderBy(x => x.LessonNumber).ToList();
            var heavyDemands = schoolClass.Where(x => x.Demand.SubjectDifficulty >= rule.DifficultyThreshold).ToList();
            if (heavyDemands.Count == 0) continue;
            var occupied = slots.Select(slot => Occupancy(model, heavyDemands.Select(x => variables[(x.Index, slot.Id)]), $"heavy{schoolClass.Key}_{slot.Id}")).ToList();
            for (var i = 0; i + 1 < occupied.Count; i++)
            {
                var pair = model.NewBoolVar($"hp{schoolClass.Key}_{dayShift.Key}_{i}");
                model.Add(pair <= occupied[i]); model.Add(pair <= occupied[i + 1]); model.Add(pair >= occupied[i] + occupied[i + 1] - 1);
                terms.Add(new("AVOID_CONSECUTIVE_DIFFICULT", pair, rule.Weight));
            }
        }
    }

    private static void AddEdgeAndEarlyLessons(SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        var edgeWeight = Weight<AvoidEdgeLessonsConstraint>(problem); var earlyWeight = Weight<PreferEarlierLessonsConstraint>(problem);
        var lastIds = problem.TimeSlots.GroupBy(x => (x.CycleWeek, x.DayOfWeek, x.ShiftId)).Select(x => x.MaxBy(s => s.LessonNumber)!.Id).ToHashSet();
        foreach (var demand in Indexed(problem)) foreach (var slot in problem.TimeSlots)
        {
            var variable = variables[(demand.Index, slot.Id)];
            if (edgeWeight > 0 && (slot.IsZeroLesson || lastIds.Contains(slot.Id))) terms.Add(new("AVOID_EDGE_LESSONS", variable, edgeWeight));
            if (earlyWeight > 0 && slot.LessonNumber > 1) terms.Add(new("PREFER_EARLIER_LESSONS", variable, earlyWeight * (slot.LessonNumber - 1)));
        }
    }

    private static void AddUserPreferences(SchedulingProblem problem,
        IReadOnlyDictionary<(int, int), BoolVar> variables, List<PenaltyTerm> terms)
    {
        foreach (var preference in problem.SoftConstraints.OfType<PreferredTimeSlotsConstraint>())
        {
            var index = problem.Demands.ToList().FindIndex(x => x.Id == preference.LessonDemandId); if (index < 0) continue;
            foreach (var slot in problem.TimeSlots.Where(x => !preference.PreferredTimeSlotIds.Contains(x.Id)))
                terms.Add(new("USER_TIME_PREFERENCE", variables[(index, slot.Id)], preference.Weight));
        }
    }

    private static BoolVar Occupancy(CpModel model, IEnumerable<BoolVar> source, string name)
    {
        var values = source.ToList(); var occupied = model.NewBoolVar(name);
        if (values.Count == 0) model.Add(occupied == 0); else model.Add(occupied == LinearExpr.Sum(values));
        return occupied;
    }
    private static int Weight<T>(SchedulingProblem problem) where T : SoftConstraint => problem.SoftConstraints.OfType<T>().FirstOrDefault()?.Weight ?? 0;
    private static IEnumerable<(LessonDemand Demand, int Index)> Indexed(SchedulingProblem problem) => problem.Demands.Select((d, i) => (d, i));
}
