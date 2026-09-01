using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public class SchoolClassService : ISchoolClassService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public SchoolClassService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<SchoolClass>> GetAllClassesAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.SchoolClasses.AsNoTracking().ToListAsync();
    }

    public async Task<List<Shift>> GetShiftsAsync()
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Shifts.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<SchoolClass> AddClassAsync(SchoolClass schoolClass)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        context.SchoolClasses.Add(schoolClass);
        await context.SaveChangesAsync();
        return schoolClass;
    }

    public async Task UpdateClassAsync(SchoolClass schoolClass)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.SchoolClasses.FindAsync(schoolClass.Id)
            ?? throw new InvalidOperationException("Класс не найден.");
        entity.Name = schoolClass.Name;
        entity.Parallel = schoolClass.Parallel;
        entity.Letter = schoolClass.Letter;
        entity.ShiftId = schoolClass.ShiftId;
        entity.MaxLessonsPerDay = schoolClass.MaxLessonsPerDay;
        entity.IsActive = schoolClass.IsActive;
        await context.SaveChangesAsync();
    }

    public async Task ArchiveClassAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var schoolClass = await context.SchoolClasses.FindAsync(id);
        if (schoolClass != null)
        {
            schoolClass.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task BulkCreateClassesAsync(int startParallel, int endParallel, List<string> letters, int shiftId, int maxLessonsPerDay)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var existingClasses = await context.SchoolClasses.ToListAsync();
        var newClasses = new List<SchoolClass>();

        for (int p = startParallel; p <= endParallel; p++)
        {
            foreach (var l in letters)
            {
                if (!existingClasses.Any(c => c.Parallel == p && c.Letter == l))
                {
                    newClasses.Add(new SchoolClass
                    {
                        Name = $"{p}{l}",
                        Parallel = p,
                        Letter = l,
                        ShiftId = shiftId,
                        MaxLessonsPerDay = maxLessonsPerDay,
                        IsActive = true
                    });
                }
            }
        }

        if (newClasses.Any())
        {
            context.SchoolClasses.AddRange(newClasses);
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> ClassExistsAsync(int parallel, string letter, int? excludedId = null)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var normalizedLetter = letter.Trim().ToUpperInvariant();
        return await context.SchoolClasses.AnyAsync(c =>
            c.Parallel == parallel && c.Letter.ToUpper() == normalizedLetter &&
            (!excludedId.HasValue || c.Id != excludedId.Value));
    }
}
