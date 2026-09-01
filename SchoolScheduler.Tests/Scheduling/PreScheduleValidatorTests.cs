using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class PreScheduleValidatorTests
{
    [Fact]
    public void Validate_FindsAllRequiredCriticalInputProblems()
    {
        var data = new PreScheduleData(
            [
                new TeachingLoad { Id = 1, TeacherId = 1, SubjectId = 1, ClassId = 1, RoomId = 1, HoursPerWeek = 8 },
                new TeachingLoad { Id = 2, TeacherId = 1, SubjectId = 99, ClassId = 1, HoursPerWeek = -1 }
            ],
            [new Teacher { Id = 1, FullName = "Иванов", IsActive = true }],
            [new SchoolClass { Id = 1, Name = "5А", ShiftId = 99, MaxLessonsPerDay = 1, IsActive = true }],
            [new Subject { Id = 1, Name = "Математика" }],
            [new Room { Id = 1, Name = "12", IsActive = false }],
            [new Shift { Id = 1, Name = "Смена 1" }],
            [new LessonPeriod { ShiftId = 1, Number = 1 }],
            [new TeacherAvailability { TeacherId = 1, DayOfWeek = 1, LessonNumber = 1, IsAvailable = true }],
            [], 5);

        var codes = new PreScheduleValidator().Validate(data).Select(x => x.Code).ToHashSet();
        Assert.Contains("MISSING_SHIFT", codes);
        Assert.Contains("MISSING_SUBJECT", codes);
        Assert.Contains("INVALID_HOURS", codes);
        Assert.Contains("CLASS_OVERLOAD", codes);
        Assert.Contains("TEACHER_SLOT_SHORTAGE", codes);
        Assert.Contains("IMPOSSIBLE_ROOM", codes);
    }

    [Fact]
    public void Validate_FindsTeacherClassAndRoomConflictsInFixedLessons()
    {
        FixedLessonAssignment[] fixedLessons =
        [
            new(1, 10, 20, 30, 1, 2),
            new(2, 10, 20, 30, 1, 2)
        ];
        var data = EmptyData(fixedLessons);
        var codes = new PreScheduleValidator().Validate(data).Select(x => x.Code).ToHashSet();
        Assert.Contains("FIXED_TEACHER_CONFLICT", codes);
        Assert.Contains("FIXED_CLASS_CONFLICT", codes);
        Assert.Contains("FIXED_ROOM_CONFLICT", codes);
    }

    [Fact]
    public void GroupLoadsForSameSubject_CountAsParallelClassHours()
    {
        var data = new PreScheduleData(
            [new TeachingLoad { TeacherId = 1, SubjectId = 1, ClassId = 1, GroupId = 1, HoursPerWeek = 3 },
             new TeachingLoad { TeacherId = 2, SubjectId = 1, ClassId = 1, GroupId = 2, HoursPerWeek = 3 }],
            [new Teacher { Id = 1 }, new Teacher { Id = 2 }],
            [new SchoolClass { Id = 1, Name = "6Б", ShiftId = 1, MaxLessonsPerDay = 1 }],
            [new Subject { Id = 1 }], [], [new Shift { Id = 1 }],
            [new LessonPeriod { Number = 1, ShiftId = 1 }], [], [], 3);
        Assert.DoesNotContain(new PreScheduleValidator().Validate(data), x => x.Code == "CLASS_OVERLOAD");
    }

    private static PreScheduleData EmptyData(IReadOnlyCollection<FixedLessonAssignment> fixedLessons) =>
        new([], [], [], [], [], [], [], [], [], 5, fixedLessons);
}
