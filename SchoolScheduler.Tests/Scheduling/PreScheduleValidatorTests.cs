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
            [new LessonPeriod { Id = 1, ShiftId = 1, Number = 1 }],
            [new TeacherAvailability { TeacherId = 1, DayOfWeek = 1, LessonPeriodId = 1, IsAvailable = true }],
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

    [Fact]
    public void SameLessonNumberInNonOverlappingShifts_IsNotFixedConflict()
    {
        FixedLessonAssignment[] fixedLessons = [new(1, 10, 20, 30, 1, 1), new(2, 10, 21, 30, 1, 1)];
        var data = new PreScheduleData([], [],
            [new SchoolClass { Id = 20, ShiftId = 1 }, new SchoolClass { Id = 21, ShiftId = 2 }], [], [],
            [new Shift { Id = 1 }, new Shift { Id = 2 }],
            [new LessonPeriod { Id = 1, ShiftId = 1, Number = 1, StartTime = new(8,0,0), EndTime = new(8,45,0) },
             new LessonPeriod { Id = 2, ShiftId = 2, Number = 1, StartTime = new(14,0,0), EndTime = new(14,45,0) }],
            [], [], 5, fixedLessons);
        Assert.DoesNotContain(new PreScheduleValidator().Validate(data), x => x.Code.StartsWith("FIXED_"));
    }

    [Fact]
    public void BellScheduleValidation_FindsInvalidTimeOverlapAndDuplicateNumber()
    {
        var periods = new LessonPeriod[]
        {
            new() { Id = 1, ShiftId = 1, Number = 1, StartTime = new(8,0,0), EndTime = new(8,45,0) },
            new() { Id = 2, ShiftId = 1, Number = 2, StartTime = new(8,30,0), EndTime = new(9,0,0) },
            new() { Id = 3, ShiftId = 1, Number = 2, StartTime = new(9,5,0), EndTime = new(9,0,0) }
        };
        var data = new PreScheduleData([], [], [], [], [], [new Shift { Id = 1 }], periods, [], [], 5);
        var codes = new PreScheduleValidator().Validate(data).Select(x => x.Code).ToHashSet();
        Assert.Contains("INVALID_LESSON_TIME", codes);
        Assert.Contains("OVERLAPPING_LESSON_PERIODS", codes);
        Assert.Contains("DUPLICATE_LESSON_PERIOD", codes);
    }

    [Fact]
    public void Capacity_CountsMissingAvailabilityAsAvailable_AndOnlyFalseAsUnavailable()
    {
        var periods = Enumerable.Range(0, 7).Select(n => new LessonPeriod { Id = n + 1, ShiftId = 1, Number = n }).ToArray();
        var loads = new[] { new TeachingLoad { TeacherId = 1, SubjectId = 1, ClassId = 1, RoomId = 1, HoursPerWeek = 7 } };
        var baseline = new PreScheduleData(loads, [new Teacher { Id = 1, IsActive = true }],
            [new SchoolClass { Id = 1, ShiftId = 1, MaxLessonsPerDay = 7 }], [new Subject { Id = 1 }],
            [new Room { Id = 1, IsActive = true }], [new Shift { Id = 1 }], periods,
            Enumerable.Range(2, 6).Select(id => new TeacherAvailability { TeacherId = 1, DayOfWeek = 1, LessonPeriodId = id, IsAvailable = true }).ToArray(),
            Enumerable.Range(2, 6).Select(id => new RoomAvailability { RoomId = 1, DayOfWeek = 1, LessonPeriodId = id, IsAvailable = true }).ToArray(), 1);
        var baselineCodes = new PreScheduleValidator().Validate(baseline).Select(x => x.Code).ToHashSet();
        Assert.DoesNotContain("TEACHER_SLOT_SHORTAGE", baselineCodes);
        Assert.DoesNotContain("ROOM_SLOT_SHORTAGE", baselineCodes);

        var blocked = baseline with
        {
            TeacherAvailability = baseline.TeacherAvailability.Append(new TeacherAvailability
                { TeacherId = 1, DayOfWeek = 1, LessonPeriodId = 1, IsAvailable = false }).ToArray(),
            RoomAvailability = baseline.RoomAvailability.Append(new RoomAvailability
                { RoomId = 1, DayOfWeek = 1, LessonPeriodId = 1, IsAvailable = false }).ToArray()
        };
        var blockedCodes = new PreScheduleValidator().Validate(blocked).Select(x => x.Code).ToHashSet();
        Assert.Contains("TEACHER_SLOT_SHORTAGE", blockedCodes);
        Assert.Contains("ROOM_SLOT_SHORTAGE", blockedCodes);
    }

    private static PreScheduleData EmptyData(IReadOnlyCollection<FixedLessonAssignment> fixedLessons) =>
        new([], [], [new SchoolClass { Id = 20, ShiftId = 1 }], [], [], [new Shift { Id = 1 }],
            [new LessonPeriod { Id = 1, ShiftId = 1, Number = 2, StartTime = new(8, 0, 0), EndTime = new(8, 45, 0) }],
            [], [], 5, fixedLessons);
}
