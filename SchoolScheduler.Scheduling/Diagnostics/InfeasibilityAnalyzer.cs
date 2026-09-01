using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Scheduling.Diagnostics;

public sealed class InfeasibilityAnalyzer
{
    public InfeasibilityReport Analyze(SchedulingProblem problem, bool includeUnknownFallback = true)
    {
        var reasons = new List<InfeasibilityReason>();
        var teacherAvailability = Availability(problem, ResourceKind.Teacher);
        var classAvailability = Availability(problem, ResourceKind.Class);
        var roomAvailability = Availability(problem, ResourceKind.Room);

        if (problem.TimeSlots.Count == 0)
            reasons.Add(Reason("NO_TIME_SLOTS", InfeasibilityCategory.Capacity, null, null, null,
                "Не задано ни одного временного слота.", "Настройте смены и расписание звонков."));
        foreach (var demand in problem.Demands)
        {
            if (demand.WeeklyHours <= 0)
                reasons.Add(Reason("INVALID_HOURS", InfeasibilityCategory.InvalidDemand, null, null, demand.Id,
                    $"У нагрузки #{demand.Id} некорректное количество часов: {demand.WeeklyHours:0.##}.", "Укажите положительное количество часов."));
            else if (demand.WeeklyHours != decimal.Truncate(demand.WeeklyHours))
                reasons.Add(Reason("FRACTIONAL_HOURS_UNSUPPORTED", InfeasibilityCategory.InvalidDemand, null, null, demand.Id,
                    $"Нагрузка #{demand.Id} содержит {demand.WeeklyHours:0.##} часа, которые нельзя разместить без правила дробных занятий.",
                    "Настройте правило дробных занятий или используйте целое число еженедельных уроков."));
            var allowed = AllowedSlots(problem, demand, teacherAvailability, classAvailability, roomAvailability);
            if (demand.WeeklyHours > 0 && demand.WeeklyHours == decimal.Truncate(demand.WeeklyHours) && allowed.Count < demand.WeeklyHours)
                reasons.Add(Reason("DEMAND_SLOT_SHORTAGE", InfeasibilityCategory.Availability, null, null, demand.Id,
                    $"Нагрузке #{demand.Id} требуется {demand.WeeklyHours:0} уроков, но подходит только {allowed.Count} слотов.",
                    "Расширьте доступность учителя/кабинета, смену класса или уменьшите часы."));
        }

        AnalyzeResourceCapacity(problem, ResourceKind.Teacher, teacherAvailability, reasons);
        AnalyzeResourceCapacity(problem, ResourceKind.Room, roomAvailability, reasons);
        AnalyzeFixedAssignments(problem, teacherAvailability, classAvailability, roomAvailability, reasons);

        if (reasons.Count == 0 && includeUnknownFallback)
            reasons.Add(Reason("UNRESOLVED_CONFLICT", InfeasibilityCategory.Unknown, null, null, null,
                "Ограничения по отдельности выглядят допустимыми, но вместе не образуют расписание.",
                "Ослабьте одну из фиксаций или доступностей и повторите генерацию."));
        return new(reasons.DistinctBy(x => (x.Code, x.ResourceKind, x.ResourceId, x.LessonDemandId, x.Message)).ToList());
    }

    private static void AnalyzeResourceCapacity(SchedulingProblem problem, ResourceKind kind,
        IReadOnlyDictionary<int, IReadOnlySet<int>> availability, List<InfeasibilityReason> reasons)
    {
        var groups = kind switch
        {
            ResourceKind.Teacher => problem.Demands.GroupBy(x => x.Resources.TeacherId),
            ResourceKind.Room => problem.Demands.Where(x => x.Resources.RoomId.HasValue).GroupBy(x => x.Resources.RoomId!.Value),
            _ => Enumerable.Empty<IGrouping<int, LessonDemand>>()
        };
        foreach (var resource in groups)
        {
            var required = resource.Where(x => x.WeeklyHours > 0).Sum(x => x.WeeklyHours);
            var available = availability.TryGetValue(resource.Key, out var slots) ? slots.Count : problem.TimeSlots.Count;
            if (required <= available) continue;
            var label = kind == ResourceKind.Teacher ? "Учителю" : "Кабинету";
            reasons.Add(Reason($"{kind.ToString().ToUpperInvariant()}_CAPACITY_SHORTAGE", InfeasibilityCategory.Capacity,
                kind, resource.Key, null, $"{label} #{resource.Key} требуется {required:0.##} уроков, но доступно только {available} слотов.",
                kind == ResourceKind.Teacher ? "Расширьте доступность учителя или перераспределите нагрузку." : "Освободите кабинет или назначьте другой допустимый кабинет."));
        }
    }

    private static void AnalyzeFixedAssignments(SchedulingProblem problem,
        IReadOnlyDictionary<int, IReadOnlySet<int>> teacherAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> classAvailability,
        IReadOnlyDictionary<int, IReadOnlySet<int>> roomAvailability,
        List<InfeasibilityReason> reasons)
    {
        var fixedAssignments = problem.HardConstraints.OfType<FixedAssignmentConstraint>().ToList();
        foreach (var fixedAssignment in fixedAssignments)
        {
            var demand = problem.Demands.FirstOrDefault(x => x.Id == fixedAssignment.LessonDemandId);
            if (demand is null || problem.TimeSlots.All(x => x.Id != fixedAssignment.TimeSlotId))
            { reasons.Add(Reason("INVALID_FIXED_ASSIGNMENT", InfeasibilityCategory.FixedAssignmentConflict, null, null,
                fixedAssignment.LessonDemandId, $"Фиксация нагрузки #{fixedAssignment.LessonDemandId} ссылается на отсутствующую нагрузку или слот.", "Удалите или переназначьте фиксацию.")); continue; }
            var allowed = AllowedSlots(problem, demand, teacherAvailability, classAvailability, roomAvailability);
            if (!allowed.Contains(fixedAssignment.TimeSlotId))
                reasons.Add(Reason("FIXED_SLOT_UNAVAILABLE", InfeasibilityCategory.FixedAssignmentConflict, null, null, demand.Id,
                    $"Нагрузка #{demand.Id} закреплена в недоступном слоте #{fixedAssignment.TimeSlotId}.", "Снимите фиксацию или расширьте доступность ресурса."));
        }
        foreach (var slot in fixedAssignments.GroupBy(x => x.TimeSlotId))
        {
            var demands = slot.Select(x => problem.Demands.FirstOrDefault(d => d.Id == x.LessonDemandId)).Where(x => x is not null).Cast<LessonDemand>().ToList();
            AddFixedConflict(demands.GroupBy(x => x.Resources.TeacherId), slot.Key, ResourceKind.Teacher, reasons);
            foreach (var schoolClass in demands.GroupBy(x => x.Resources.ClassId))
            {
                var whole = schoolClass.Count(x => !x.Resources.GroupId.HasValue);
                var sameGroup = schoolClass.Where(x => x.Resources.GroupId.HasValue).GroupBy(x => x.Resources.GroupId).Any(x => x.Count() > 1);
                if (whole > 1 || (whole > 0 && schoolClass.Count() > whole) || sameGroup)
                    reasons.Add(Reason("FIXED_RESOURCE_CONFLICT", InfeasibilityCategory.FixedAssignmentConflict,
                        ResourceKind.Class, schoolClass.Key, null,
                        $"Класс #{schoolClass.Key} имеет конфликтующие закреплённые занятия в слоте #{slot.Key}.",
                        "Перенесите фиксацию целого класса или одной из совпадающих групп."));
            }
            AddFixedConflict(demands.Where(x => x.Resources.RoomId.HasValue).GroupBy(x => x.Resources.RoomId!.Value), slot.Key, ResourceKind.Room, reasons);
        }
    }

    private static void AddFixedConflict(IEnumerable<IGrouping<int, LessonDemand>> groups, int slotId,
        ResourceKind kind, List<InfeasibilityReason> reasons)
    {
        foreach (var group in groups.Where(x => x.Count() > 1)) reasons.Add(Reason("FIXED_RESOURCE_CONFLICT",
            InfeasibilityCategory.FixedAssignmentConflict, kind, group.Key, null,
            $"Ресурс {kind} #{group.Key} закреплён за {group.Count()} занятиями в слоте #{slotId}.",
            "Перенесите или снимите одну из конфликтующих фиксаций."));
    }

    private static IReadOnlySet<int> AllowedSlots(SchedulingProblem problem, LessonDemand demand,
        IReadOnlyDictionary<int, IReadOnlySet<int>> teachers, IReadOnlyDictionary<int, IReadOnlySet<int>> classes,
        IReadOnlyDictionary<int, IReadOnlySet<int>> rooms) => problem.TimeSlots.Where(slot =>
        (!slot.IsZeroLesson || demand.AllowZeroLesson) &&
        (!teachers.TryGetValue(demand.Resources.TeacherId, out var t) || t.Contains(slot.Id)) &&
        (!classes.TryGetValue(demand.Resources.ClassId, out var c) || c.Contains(slot.Id)) &&
        (!demand.Resources.RoomId.HasValue || !rooms.TryGetValue(demand.Resources.RoomId.Value, out var r) || r.Contains(slot.Id)))
        .Select(x => x.Id).ToHashSet();

    private static Dictionary<int, IReadOnlySet<int>> Availability(SchedulingProblem problem, ResourceKind kind) =>
        problem.HardConstraints.OfType<ResourceAvailabilityConstraint>().Where(x => x.ResourceKind == kind)
            .GroupBy(x => x.ResourceId).ToDictionary(x => x.Key, x => (IReadOnlySet<int>)x.SelectMany(v => v.AllowedTimeSlotIds).ToHashSet());
    private static InfeasibilityReason Reason(string code, InfeasibilityCategory category, ResourceKind? kind,
        int? resourceId, int? demandId, string message, string suggestion) => new(code, category, kind, resourceId, demandId, message, suggestion);
}
