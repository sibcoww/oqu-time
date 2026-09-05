using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Scheduling.Validation;

public enum ValidationSeverity { Warning, Critical }
public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Message);
public sealed record FixedLessonAssignment(int TeachingLoadId, int TeacherId, int ClassId, int? RoomId,
    int DayOfWeek, int LessonNumber);
public sealed record PreScheduleData(IReadOnlyCollection<TeachingLoad> Loads, IReadOnlyCollection<Teacher> Teachers,
    IReadOnlyCollection<SchoolClass> Classes, IReadOnlyCollection<Subject> Subjects, IReadOnlyCollection<Room> Rooms,
    IReadOnlyCollection<Shift> Shifts, IReadOnlyCollection<LessonPeriod> LessonPeriods,
    IReadOnlyCollection<TeacherAvailability> TeacherAvailability, IReadOnlyCollection<RoomAvailability> RoomAvailability,
    int DaysPerWeek, IReadOnlyCollection<FixedLessonAssignment>? FixedLessons = null);

public sealed class PreScheduleValidator
{
    public IReadOnlyList<ValidationIssue> Validate(PreScheduleData data)
    {
        var issues = new List<ValidationIssue>();
        ValidateReferencesAndHours(data, issues);
        ValidateLessonPeriods(data, issues);
        ValidateClassCapacity(data, issues);
        ValidateTeacherCapacity(data, issues);
        ValidateRooms(data, issues);
        ValidateFixedLessons(data, data.FixedLessons ?? [], issues);
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
            issues.Add(Critical("MISSING_SHIFT", $"Класс {schoolClass.Name} не назначен на существующую смену."));
        foreach (var load in data.Loads)
        {
            if (!teachers.Contains(load.TeacherId)) issues.Add(Critical("MISSING_TEACHER", $"У строки нагрузки #{load.Id} отсутствует учитель."));
            if (!subjects.Contains(load.SubjectId)) issues.Add(Critical("MISSING_SUBJECT", $"У строки нагрузки #{load.Id} отсутствует предмет."));
            if (!classes.Contains(load.ClassId)) issues.Add(Critical("MISSING_CLASS", $"У строки нагрузки #{load.Id} отсутствует класс."));
            if (load.RoomId.HasValue && !rooms.Contains(load.RoomId.Value)) issues.Add(Critical("IMPOSSIBLE_ROOM", $"У строки нагрузки #{load.Id} указан несуществующий кабинет."));
            if (load.HoursPerWeek <= 0) issues.Add(Critical("INVALID_HOURS", $"Часы строки нагрузки #{load.Id} должны быть больше нуля."));
            else if (load.HoursPerWeek != decimal.Truncate(load.HoursPerWeek) && load.HoursPerWeek is not 0.25m and not 0.5m and not 0.75m)
                issues.Add(new(ValidationSeverity.Warning, "FRACTIONAL_HOURS", $"Нестандартная дробная нагрузка {load.HoursPerWeek} у строки #{load.Id}."));
        }
    }

    private static void ValidateLessonPeriods(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var duplicate in data.LessonPeriods.GroupBy(x => (x.ShiftId, x.Number)).Where(x => x.Count() > 1))
            issues.Add(Critical("DUPLICATE_LESSON_PERIOD", $"Урок {duplicate.Key.Number} смены #{duplicate.Key.ShiftId} указан несколько раз."));
        foreach (var period in data.LessonPeriods.Where(x => x.EndTime <= x.StartTime))
            issues.Add(Critical("INVALID_LESSON_TIME", $"Урок {period.Number} смены #{period.ShiftId} должен заканчиваться позже начала."));
        foreach (var shift in data.LessonPeriods.GroupBy(x => x.ShiftId))
        {
            var ordered = shift.OrderBy(x => x.StartTime).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].StartTime < ordered[i - 1].EndTime)
                    issues.Add(Critical("OVERLAPPING_LESSON_PERIODS", $"Уроки смены #{shift.Key} пересекаются."));
        }
    }

    private static void ValidateClassCapacity(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var schoolClass in data.Classes.Where(x => x.IsActive))
        {
            var loads = data.Loads.Where(x => x.ClassId == schoolClass.Id && x.HoursPerWeek > 0).ToList();
            var hours = loads.Where(x => !x.GroupId.HasValue).Sum(x => x.HoursPerWeek) +
                loads.Where(x => x.GroupId.HasValue).GroupBy(x => x.SubjectId)
                    .Sum(subject => subject.GroupBy(x => x.GroupId).Max(group => group.Sum(x => x.HoursPerWeek)));
            var periods = data.LessonPeriods.Count(x => x.ShiftId == schoolClass.ShiftId && x.Number <= schoolClass.MaxLessonsPerDay);
            var capacity = data.DaysPerWeek * periods;
            if (hours > capacity) issues.Add(Critical("CLASS_OVERLOAD", $"Класс {schoolClass.Name}: нагрузка {hours:0.##}, доступно {capacity} слотов."));
        }
    }

    private static void ValidateTeacherCapacity(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var teacher in data.Teachers.Where(x => x.IsActive))
        {
            var required = data.Loads.Where(x => x.TeacherId == teacher.Id && x.HoursPerWeek > 0).Sum(x => x.HoursPerWeek);
            var available = AvailableSlotCount(data.LessonPeriods, data.DaysPerWeek,
                data.TeacherAvailability.Where(x => x.TeacherId == teacher.Id)
                    .Select(x => (x.DayOfWeek, x.LessonPeriodId, x.IsAvailable)));
            if (required > available) issues.Add(Critical("TEACHER_SLOT_SHORTAGE", $"Учителю {teacher.FullName} нужно {required:0.##} урока, доступно {available}."));
        }
    }

    private static void ValidateRooms(PreScheduleData data, List<ValidationIssue> issues)
    {
        foreach (var room in data.Rooms.Where(x => !x.IsActive && data.Loads.Any(l => l.RoomId == x.Id)))
            issues.Add(Critical("IMPOSSIBLE_ROOM", $"Кабинет {room.Name} архивирован, но используется в нагрузке."));
        foreach (var room in data.Rooms.Where(x => x.IsActive))
        {
            var required = data.Loads.Where(x => x.RoomId == room.Id && x.HoursPerWeek > 0).Sum(x => x.HoursPerWeek);
            var available = AvailableSlotCount(data.LessonPeriods, data.DaysPerWeek,
                data.RoomAvailability.Where(x => x.RoomId == room.Id)
                    .Select(x => (x.DayOfWeek, x.LessonPeriodId, x.IsAvailable)));
            if (required > available) issues.Add(Critical("ROOM_SLOT_SHORTAGE", $"Для кабинета {room.Name} нужно {required:0.##} занятия, доступно {available}."));
        }
    }

    private static int AvailableSlotCount(IReadOnlyCollection<LessonPeriod> periods, int daysPerWeek,
        IEnumerable<(int DayOfWeek, int LessonPeriodId, bool IsAvailable)> availability)
    {
        var periodIds = periods.Select(x => x.Id).ToHashSet();
        var unavailable = availability.Where(x => !x.IsAvailable && x.DayOfWeek >= 1 &&
                x.DayOfWeek <= daysPerWeek && periodIds.Contains(x.LessonPeriodId))
            .Select(x => (x.DayOfWeek, x.LessonPeriodId)).Distinct().Count();
        return periods.Count * daysPerWeek - unavailable;
    }

    private static void ValidateFixedLessons(PreScheduleData data, IReadOnlyCollection<FixedLessonAssignment> fixedLessons,
        List<ValidationIssue> issues)
    {
        var classes = data.Classes.ToDictionary(x => x.Id);
        var periods = data.LessonPeriods.GroupBy(x => (x.ShiftId, x.Number)).ToDictionary(x => x.Key, x => x.First());
        var resolved = fixedLessons.Select(x => classes.TryGetValue(x.ClassId, out var c) && periods.TryGetValue((c.ShiftId, x.LessonNumber), out var p)
            ? (Lesson: x, Period: p) : (Lesson: x, Period: (LessonPeriod?)null)).Where(x => x.Period is not null).ToList();
        for (var i = 0; i < resolved.Count; i++)
        for (var j = i + 1; j < resolved.Count; j++)
        {
            var a = resolved[i]; var b = resolved[j];
            var samePeriod = a.Period!.ShiftId == b.Period!.ShiftId && a.Period.Number == b.Period.Number;
            var overlaps = samePeriod || (a.Period.StartTime < b.Period.EndTime && b.Period.StartTime < a.Period.EndTime);
            if (a.Lesson.DayOfWeek != b.Lesson.DayOfWeek || !overlaps) continue;
            if (a.Lesson.TeacherId == b.Lesson.TeacherId) issues.Add(Critical("FIXED_TEACHER_CONFLICT", $"Учитель #{a.Lesson.TeacherId} закреплён на пересекающиеся уроки."));
            if (a.Lesson.ClassId == b.Lesson.ClassId) issues.Add(Critical("FIXED_CLASS_CONFLICT", $"Класс #{a.Lesson.ClassId} имеет пересекающиеся уроки."));
            if (a.Lesson.RoomId.HasValue && a.Lesson.RoomId == b.Lesson.RoomId) issues.Add(Critical("FIXED_ROOM_CONFLICT", $"Кабинет #{a.Lesson.RoomId} закреплён на пересекающиеся уроки."));
        }
    }

    private static ValidationIssue Critical(string code, string message) => new(ValidationSeverity.Critical, code, message);
}
