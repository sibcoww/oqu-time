using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Scheduling.Validation;

public enum ValidationSeverity { Warning, Critical }

public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Message);
public sealed record FixedLessonAssignment(int TeachingLoadId, int TeacherId, int ClassId, int? RoomId,
    int DayOfWeek, int LessonNumber);

public sealed record PreScheduleData(
    IReadOnlyCollection<TeachingLoad> Loads,
    IReadOnlyCollection<Teacher> Teachers,
    IReadOnlyCollection<SchoolClass> Classes,
    IReadOnlyCollection<Subject> Subjects,
    IReadOnlyCollection<Room> Rooms,
    IReadOnlyCollection<Shift> Shifts,
    IReadOnlyCollection<LessonPeriod> LessonPeriods,
    IReadOnlyCollection<TeacherAvailability> TeacherAvailability,
    IReadOnlyCollection<RoomAvailability> RoomAvailability,
    int DaysPerWeek,
    IReadOnlyCollection<FixedLessonAssignment>? FixedLessons = null);

public sealed class PreScheduleValidator
{
    public IReadOnlyList<ValidationIssue> Validate(PreScheduleData data)
    {
        var issues = new List<ValidationIssue>();
        ValidateReferencesAndHours(data, issues);
        ValidateClassCapacity(data, issues);
        ValidateTeacherCapacity(data, issues);
        ValidateRooms(data, issues);
        ValidateFixedLessons(data.FixedLessons ?? [], issues);
        return issues;
    }

    private static void ValidateReferencesAndHours(PreScheduleData data, List<ValidationIssue> issues)
    {
        var teachers = data.Teachers.Select(x => x.Id).ToHashSet();
        var subjects = data.Subjects.Select(x => x.Id).ToHashSet();
        var classes = data.Classes.Select(x => x.Id).ToHashSet();
        var rooms = data.Rooms.Select(x => x.Id).ToHashSet();
        var shifts = data.Shifts.Select(x => x.Id).ToHashSet();
        foreach (var schoolClass in data.Classes.Where(x => x.IsActive && (x.ShiftId <= 0 || !shifts.Contains(x.ShiftId))))
            issues.Add(Critical("MISSING_SHIFT", $"Класс {schoolClass.Name} не назначен ни на одну существующую смену."));
        foreach (var load in data.Loads)
        {
            if (!teachers.Contains(load.TeacherId)) issues.Add(Critical("MISSING_TEACHER", $"У строки нагрузки #{load.Id} не указан существующий учитель."));
            if (!subjects.Contains(load.SubjectId)) issues.Add(Critical("MISSING_SUBJECT", $"У строки нагрузки #{load.Id} не указан существующий предмет."));
            if (!classes.Contains(load.ClassId)) issues.Add(Critical("MISSING_CLASS", $"У строки нагрузки #{load.Id} не указан существующий класс."));
            if (load.RoomId.HasValue && !rooms.Contains(load.RoomId.Value)) issues.Add(Critical("IMPOSSIBLE_ROOM", $"У строки нагрузки #{load.Id} указан несуществующий кабинет."));
            if (load.HoursPerWeek <= 0) issues.Add(Critical("INVALID_HOURS", $"У строки нагрузки #{load.Id} количество часов должно быть больше нуля."));
            else if (load.HoursPerWeek != decimal.Truncate(load.HoursPerWeek) && load.HoursPerWeek is not 0.25m and not 0.5m and not 0.75m)
                issues.Add(new(ValidationSeverity.Warning, "FRACTIONAL_HOURS", $"У строки нагрузки #{load.Id} нестандартная дробная нагрузка {load.HoursPerWeek}."));
        }
    }

    private static void ValidateClassCapacity(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var schoolClass in data.Classes.Where(x => x.IsActive))
        {
            var classLoads = data.Loads.Where(x => x.ClassId == schoolClass.Id && x.HoursPerWeek > 0).ToList();
            var hours = classLoads.Where(x => !x.GroupId.HasValue).Sum(x => x.HoursPerWeek) +
                classLoads.Where(x => x.GroupId.HasValue).GroupBy(x => x.SubjectId)
                    .Sum(subject => subject.GroupBy(x => x.GroupId).Max(group => group.Sum(x => x.HoursPerWeek)));
            var capacity = data.DaysPerWeek * schoolClass.MaxLessonsPerDay;
            if (hours > capacity) issues.Add(Critical("CLASS_OVERLOAD",
                $"{schoolClass.Name} имеет {hours:0.##} часа нагрузки, но доступно только {capacity} временных слотов."));
        }
    }

    private static void ValidateTeacherCapacity(PreScheduleData data, List<ValidationIssue> issues)
    {
        var defaultSlots = data.LessonPeriods.Select(x => x.Number).Distinct().Count() * data.DaysPerWeek;
        foreach (var teacher in data.Teachers.Where(x => x.IsActive))
        {
            var required = data.Loads.Where(x => x.TeacherId == teacher.Id && x.HoursPerWeek > 0).Sum(x => x.HoursPerWeek);
            var availability = data.TeacherAvailability.Where(x => x.TeacherId == teacher.Id).ToList();
            var available = availability.Count == 0 ? defaultSlots : availability.Count(x => x.IsAvailable && x.DayOfWeek <= data.DaysPerWeek);
            if (required > available) issues.Add(Critical("TEACHER_SLOT_SHORTAGE",
                $"Учитель {teacher.FullName} должен провести {required:0.##} урока, но доступность позволяет поставить максимум {available}."));
        }
    }

    private static void ValidateRooms(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var room in data.Rooms.Where(x => !x.IsActive && data.Loads.Any(l => l.RoomId == x.Id)))
            issues.Add(Critical("IMPOSSIBLE_ROOM", $"Кабинет {room.Name} архивирован, но используется в нагрузке."));
        foreach (var room in data.Rooms.Where(x => x.IsActive))
        {
            var required = data.Loads.Where(x => x.RoomId == room.Id && x.HoursPerWeek > 0).Sum(x => x.HoursPerWeek);
            var availability = data.RoomAvailability.Where(x => x.RoomId == room.Id).ToList();
            if (availability.Count > 0)
            {
                var available = availability.Count(x => x.IsAvailable && x.DayOfWeek <= data.DaysPerWeek);
                if (required > available) issues.Add(Critical("ROOM_SLOT_SHORTAGE",
                    $"Для кабинета {room.Name} требуется {required:0.##} занятия, но доступно только {available} слотов."));
            }
        }
    }

    private static void ValidateFixedLessons(IReadOnlyCollection<FixedLessonAssignment> fixedLessons, List<ValidationIssue> issues)
    {
        foreach (var slot in fixedLessons.GroupBy(x => (x.DayOfWeek, x.LessonNumber)))
        {
            foreach (var conflict in slot.GroupBy(x => x.TeacherId).Where(x => x.Count() > 1))
                issues.Add(Critical("FIXED_TEACHER_CONFLICT", $"Учитель #{conflict.Key} закреплён на несколько уроков одновременно: день {slot.Key.DayOfWeek}, урок {slot.Key.LessonNumber}."));
            foreach (var conflict in slot.GroupBy(x => x.ClassId).Where(x => x.Count() > 1))
                issues.Add(Critical("FIXED_CLASS_CONFLICT", $"Класс #{conflict.Key} имеет несколько закреплённых уроков одновременно: день {slot.Key.DayOfWeek}, урок {slot.Key.LessonNumber}."));
            foreach (var conflict in slot.Where(x => x.RoomId.HasValue).GroupBy(x => x.RoomId!.Value).Where(x => x.Count() > 1))
                issues.Add(Critical("FIXED_ROOM_CONFLICT", $"Кабинет #{conflict.Key} закреплён за несколькими уроками одновременно: день {slot.Key.DayOfWeek}, урок {slot.Key.LessonNumber}."));
        }
    }

    private static ValidationIssue Critical(string code, string message) => new(ValidationSeverity.Critical, code, message);
}
