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
    int SubjectDifficulty,
    string Comment);

public sealed record TimeSlot(int Id, int ShiftId, int DayOfWeek, int LessonNumber,
    TimeSpan StartTime, TimeSpan EndTime, bool IsZeroLesson, int CycleWeek = 1);

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
public sealed record SpreadSubjectAcrossWeekConstraint(int Weight = 4)
    : SoftConstraint("SPREAD_SUBJECT_WEEK", "Распределять один предмет по разным дням", Weight);
public sealed record AvoidConsecutiveDifficultSubjectsConstraint(int DifficultyThreshold = 7, int Weight = 3)
    : SoftConstraint("AVOID_CONSECUTIVE_DIFFICULT", "Не ставить тяжёлые предметы подряд", Weight);
public sealed record AvoidEdgeLessonsConstraint(int Weight = 2)
    : SoftConstraint("AVOID_EDGE_LESSONS", "Избегать нулевых и последних уроков", Weight);
public sealed record PreferredTimeSlotsConstraint(int LessonDemandId, IReadOnlySet<int> PreferredTimeSlotIds, int Weight = 5)
    : SoftConstraint("USER_TIME_PREFERENCE", $"Предпочтительные слоты нагрузки #{LessonDemandId}", Weight);

public enum ResourceKind { Teacher, Class, Group, Room }
