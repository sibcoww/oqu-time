using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Data;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.App.Services;

public sealed class PreScheduleValidationService(IDbContextFactory<AppDbContext> factory,
    PreScheduleValidator validator) : IPreScheduleValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>> ValidateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var school = await db.Schools.AsNoTracking().FirstOrDefaultAsync();
        var data = new PreScheduleData(
            await db.TeachingLoads.AsNoTracking().ToListAsync(),
            await db.Teachers.AsNoTracking().ToListAsync(),
            await db.SchoolClasses.AsNoTracking().ToListAsync(),
            await db.Subjects.AsNoTracking().ToListAsync(),
            await db.Rooms.AsNoTracking().ToListAsync(),
            await db.Shifts.AsNoTracking().ToListAsync(),
            await db.LessonPeriods.AsNoTracking().ToListAsync(),
            await db.TeacherAvailabilities.AsNoTracking().ToListAsync(),
            await db.RoomAvailabilities.AsNoTracking().ToListAsync(),
            school?.DaysPerWeek ?? 5);
        return validator.Validate(data);
    }
}
