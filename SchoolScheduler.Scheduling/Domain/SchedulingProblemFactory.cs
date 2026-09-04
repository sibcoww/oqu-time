using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.Scheduling.Domain;

public sealed record SchedulingSource(
    IReadOnlyCollection<TeachingLoad> Loads,
    IReadOnlyCollection<SchoolClass> Classes,
    IReadOnlyCollection<Subject> Subjects,
    IReadOnlyCollection<LessonPeriod> LessonPeriods,
    IReadOnlyCollection<TeacherAvailability> TeacherAvailability,
    IReadOnlyCollection<RoomAvailability> RoomAvailability,
    int DaysPerWeek,
    IReadOnlyCollection<FixedLessonAssignment>? FixedLessons = null);

public sealed class SchedulingProblemFactory
{
    public SchedulingProblem Create(SchedulingSource source)
    {
        var subjects = source.Subjects.ToDictionary(x => x.Id);
        var classes = source.Classes.ToDictionary(x => x.Id);
        var demands = source.Loads.Select(load => new LessonDemand(
            load.Id, load.HoursPerWeek,
            new(load.TeacherId, load.SubjectId, load.ClassId, load.GroupId, load.RoomId),
            load.AllowZeroLesson,
            subjects.TryGetValue(load.SubjectId, out var subject) && subject.AllowDoubleLessons,
            subjects.TryGetValue(load.SubjectId, out subject) ? subject.Difficulty : 1,
            load.Comment ?? string.Empty)).ToList();

        var slots = new List<TimeSlot>();
        var slotId = 1;
        foreach (var period in source.LessonPeriods.OrderBy(x => x.ShiftId).ThenBy(x => x.Number))
            for (var day = 1; day <= source.DaysPerWeek; day++)
                slots.Add(new(slotId++, period.ShiftId, day, period.Number,
                    period.StartTime, period.EndTime, period.Number == 0));

        var hard = new List<HardConstraint>
        {
            new NoResourceOverlapConstraint(ResourceKind.Teacher),
            new NoResourceOverlapConstraint(ResourceKind.Class),
            new NoResourceOverlapConstraint(ResourceKind.Room)
        };
        foreach (var teacher in source.Loads.Select(x => x.TeacherId).Distinct())
            hard.Add(new ResourceAvailabilityConstraint(ResourceKind.Teacher, teacher,
                AllowedSlots(slots, source.TeacherAvailability.Where(x => x.TeacherId == teacher)
                    .Select(x => (x.DayOfWeek, x.LessonPeriodId, x.IsAvailable)), source.LessonPeriods)));
        foreach (var room in source.Loads.Where(x => x.RoomId.HasValue).Select(x => x.RoomId!.Value).Distinct())
            hard.Add(new ResourceAvailabilityConstraint(ResourceKind.Room, room,
                AllowedSlots(slots, source.RoomAvailability.Where(x => x.RoomId == room)
                    .Select(x => (x.DayOfWeek, x.LessonPeriodId, x.IsAvailable)), source.LessonPeriods)));
        foreach (var schoolClass in classes.Values)
            hard.Add(new ResourceAvailabilityConstraint(ResourceKind.Class, schoolClass.Id,
                slots.Where(x => x.ShiftId == schoolClass.ShiftId && x.LessonNumber <= schoolClass.MaxLessonsPerDay)
                    .Select(x => x.Id).ToHashSet()));
        foreach (var fixedLesson in source.FixedLessons ?? [])
        {
            var slot = slots.FirstOrDefault(x => x.DayOfWeek == fixedLesson.DayOfWeek && x.LessonNumber == fixedLesson.LessonNumber &&
                classes.TryGetValue(fixedLesson.ClassId, out var schoolClass) && x.ShiftId == schoolClass.ShiftId);
            if (slot is not null) hard.Add(new FixedAssignmentConstraint(fixedLesson.TeachingLoadId, slot.Id));
        }

        SoftConstraint[] soft = [new MinimizeTeacherGapsConstraint(), new BalanceClassDayConstraint(),
            new SpreadSubjectAcrossWeekConstraint(), new AvoidConsecutiveDifficultSubjectsConstraint(),
            new AvoidEdgeLessonsConstraint(), new PreferEarlierLessonsConstraint()];
        return new(demands, slots, hard, soft);
    }

    private static IReadOnlySet<int> AllowedSlots(IEnumerable<TimeSlot> slots,
        IEnumerable<(int DayOfWeek, int LessonPeriodId, bool IsAvailable)> availability,
        IReadOnlyCollection<LessonPeriod> periods)
    {
        var values = availability.ToList();
        if (values.Count == 0) return slots.Select(x => x.Id).ToHashSet();
        var periodKeys = periods.ToDictionary(x => x.Id, x => (x.ShiftId, x.Number));
        var allowed = values.Where(x => x.IsAvailable && periodKeys.ContainsKey(x.LessonPeriodId))
            .Select(x => (x.DayOfWeek, periodKeys[x.LessonPeriodId])).ToHashSet();
        return slots.Where(slot => allowed.Contains((slot.DayOfWeek, (slot.ShiftId, slot.LessonNumber))))
            .Select(x => x.Id).ToHashSet();
    }
}
