using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Data;
using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Solver;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.App.Services;

public sealed class ScheduleGenerationService(IDbContextFactory<AppDbContext> factory,
    PreScheduleValidator validator, SchedulingProblemFactory problemFactory,
    IScheduleGenerator generator) : IScheduleGenerationService
{
    public async Task<GeneratedSchedule> GenerateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var loads = await db.TeachingLoads.AsNoTracking().ToListAsync(cancellationToken);
        var teachers = await db.Teachers.AsNoTracking().ToListAsync(cancellationToken);
        var classes = await db.SchoolClasses.AsNoTracking().ToListAsync(cancellationToken);
        var subjects = await db.Subjects.AsNoTracking().ToListAsync(cancellationToken);
        var rooms = await db.Rooms.AsNoTracking().ToListAsync(cancellationToken);
        var groups = await db.SchoolGroups.AsNoTracking().ToListAsync(cancellationToken);
        var shifts = await db.Shifts.AsNoTracking().ToListAsync(cancellationToken);
        var periods = await db.LessonPeriods.AsNoTracking().ToListAsync(cancellationToken);
        var teacherAvailability = await db.TeacherAvailabilities.AsNoTracking().ToListAsync(cancellationToken);
        var roomAvailability = await db.RoomAvailabilities.AsNoTracking().ToListAsync(cancellationToken);
        var days = (await db.Schools.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.DaysPerWeek ?? 5;

        var validation = validator.Validate(new(loads, teachers, classes, subjects, rooms, shifts, periods,
            teacherAvailability, roomAvailability, days));
        var critical = validation.Where(x => x.Severity == ValidationSeverity.Critical).ToList();
        if (critical.Count > 0)
        {
            var reasons = critical.Select(x => new InfeasibilityReason(x.Code, InfeasibilityCategory.InvalidDemand,
                null, null, null, x.Message, "Исправьте исходные данные на экране «Ограничения»." )).ToList();
            return new(new([], [], [], []), new([], ScheduleScore.Empty, false,
                reasons.Select(x => x.Message).ToList(), new(reasons)), teachers, subjects, classes, groups, rooms, days);
        }

        var problem = problemFactory.Create(new(loads, classes, subjects, periods,
            teacherAvailability, roomAvailability, days));
        var candidate = await Task.Run(() => generator.Generate(problem), cancellationToken);
        return new(problem, candidate, teachers, subjects, classes, groups, rooms, days);
    }

    public async Task<GeneratedSchedule> ReoptimizeAsync(GeneratedSchedule current,
        IReadOnlyCollection<PreservedScheduleAssignment> preservedAssignments,
        CancellationToken cancellationToken = default)
    {
        var preservedConstraints = preservedAssignments
            .Select(x => new FixedAssignmentConstraint(x.LessonDemandId, x.TimeSlotId))
            .Cast<HardConstraint>();
        var constraints = current.Problem.HardConstraints.Concat(preservedConstraints)
            .DistinctBy(x => x is FixedAssignmentConstraint f ? $"{f.LessonDemandId}:{f.TimeSlotId}" : $"{x.Code}:{x.Description}")
            .ToList();
        var problem = current.Problem with { HardConstraints = constraints };
        var candidate = await Task.Run(() => generator.Generate(problem), cancellationToken);
        // Temporary preservation constraints belong only to this solver run. Keeping them in the
        // displayed problem would silently preserve old manual edits in later, explicitly unlocked runs.
        return current with { Candidate = candidate };
    }
}
