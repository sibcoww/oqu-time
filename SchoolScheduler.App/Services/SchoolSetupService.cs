using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public class SchoolSetupService : ISchoolSetupService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public SchoolSetupService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> IsSchoolConfiguredAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        // Since we are setting up locally, ensure DB is created first
        await context.Database.MigrateAsync();
        return await context.Schools.AnyAsync();
    }

    public async Task SaveSetupAsync(School school, AcademicYear year, int shiftCount, int lessonsPerShift)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();

        context.Schools.Add(school);

        year.IsActive = true;
        context.AcademicYears.Add(year);

        for (int i = 1; i <= shiftCount; i++)
        {
            var shift = new Shift { Name = $"Смена {i}" };
            context.Shifts.Add(shift);

            // Add periods for the shift
            // Base on specification, we could wait for SaveChanges to get ShiftId or let EF core handle relationships if we added navigation properties.
            // Since Core models don't have navigation properties right now, we need to save Shift first to get Identity, then Periods.
            await context.SaveChangesAsync();

            for (int j = 1; j <= lessonsPerShift; j++)
            {
                context.LessonPeriods.Add(new LessonPeriod
                {
                    ShiftId = shift.Id,
                    Number = j
                });
            }
        }

        await context.SaveChangesAsync();
    }
}