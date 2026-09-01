namespace SchoolScheduler.Scheduling.Domain;

public sealed record SchedulingProblem(
    IReadOnlyList<LessonDemand> Demands,
    IReadOnlyList<TimeSlot> TimeSlots,
    IReadOnlyList<HardConstraint> HardConstraints,
    IReadOnlyList<SoftConstraint> SoftConstraints);

public sealed record LessonDemand(
    int Id,
    decimal WeeklyHours,
    ResourceRequirement Resources,
    bool AllowZeroLesson,
    bool AllowDoubleLessons,
    string Comment);

public sealed record TimeSlot(int Id, int ShiftId, int DayOfWeek, int LessonNumber,
    TimeSpan StartTime, TimeSpan EndTime, bool IsZeroLesson);

public sealed record ResourceRequirement(int TeacherId, int SubjectId, int ClassId,
    int? GroupId, int? RoomId);

public abstract record HardConstraint(string Code, string Description);
public sealed record ResourceAvailabilityConstraint(ResourceKind ResourceKind, int ResourceId,
    IReadOnlySet<int> AllowedTimeSlotIds)
    : HardConstraint("RESOURCE_AVAILABILITY", $"Доступность ресурса {ResourceKind} #{ResourceId}");
public sealed record FixedAssignmentConstraint(int LessonDemandId, int TimeSlotId)
    : HardConstraint("FIXED_ASSIGNMENT", $"Фиксация нагрузки #{LessonDemandId} в слоте #{TimeSlotId}");
public sealed record NoResourceOverlapConstraint(ResourceKind ResourceKind)
    : HardConstraint("NO_RESOURCE_OVERLAP", $"Запрет одновременного использования ресурса {ResourceKind}");

public abstract record SoftConstraint(string Code, string Description, int Weight);
public sealed record MinimizeTeacherGapsConstraint(int Weight = 10)
    : SoftConstraint("MINIMIZE_TEACHER_GAPS", "Минимизировать окна учителей", Weight);
public sealed record BalanceClassDayConstraint(int Weight = 5)
    : SoftConstraint("BALANCE_CLASS_DAY", "Равномерно распределять уроки класса", Weight);
public sealed record PreferEarlierLessonsConstraint(int Weight = 1)
    : SoftConstraint("PREFER_EARLIER_LESSONS", "Предпочитать более ранние уроки", Weight);

public enum ResourceKind { Teacher, Class, Group, Room }
