using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public class SchoolSetupService(IDbContextFactory<AppDbContext> dbContextFactory) : ISchoolSetupService
{
    public async Task<bool> IsSchoolConfiguredAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        return await context.Schools.AnyAsync();
    }

    public async Task SaveSetupAsync(School school, AcademicYear year, IReadOnlyCollection<Shift> shifts)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        context.Schools.Add(school);
        year.IsActive = true;
        context.AcademicYears.Add(year);
        foreach (var input in shifts)
        {
            var shift = new Shift { Name = input.Name.Trim() };
            foreach (var period in input.LessonPeriods.OrderBy(x => x.Number))
                shift.LessonPeriods.Add(new LessonPeriod
                    { Number = period.Number, StartTime = period.StartTime, EndTime = period.EndTime });
            context.Shifts.Add(shift);
        }
        await context.SaveChangesAsync();
    }

    public async Task<(School School, IReadOnlyList<Shift> Shifts)> GetTimeModelAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        var school = await context.Schools.AsNoTracking().FirstAsync();
        var shifts = await context.Shifts.AsNoTracking().Include(x => x.LessonPeriods)
            .OrderBy(x => x.Id).ToListAsync();
        return (school, shifts);
    }

    public async Task SaveBellScheduleAsync(IReadOnlyCollection<Shift> shifts)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (var input in shifts)
        {
            var shift = await context.Shifts.Include(x => x.LessonPeriods).SingleAsync(x => x.Id == input.Id);
            shift.Name = input.Name.Trim();
            var retainedIds = input.LessonPeriods.Where(x => x.Id != 0).Select(x => x.Id).ToHashSet();
            context.LessonPeriods.RemoveRange(shift.LessonPeriods.Where(x => !retainedIds.Contains(x.Id)));
            foreach (var period in input.LessonPeriods)
            {
                var entity = period.Id == 0 ? new LessonPeriod { ShiftId = shift.Id } :
                    shift.LessonPeriods.Single(x => x.Id == period.Id);
                if (period.Id == 0) context.LessonPeriods.Add(entity);
                entity.Number = period.Number;
                entity.StartTime = period.StartTime;
                entity.EndTime = period.EndTime;
            }
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
