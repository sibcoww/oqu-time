using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public sealed class TeacherService(IDbContextFactory<AppDbContext> dbContextFactory) : ITeacherService
{
    public async Task<List<Teacher>> GetTeachersAsync(string? search = null)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var teachers = await db.Teachers.AsNoTracking().OrderBy(x => x.FullName).ToListAsync();
        if (string.IsNullOrWhiteSpace(search)) return teachers;
        var term = search.Trim();
        return teachers.Where(x => x.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<Teacher?> GetTeacherAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Teachers.AsNoTracking().Include(x => x.Availability)
            .SingleOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Teacher> SaveTeacherAsync(Teacher teacher, IReadOnlyCollection<TeacherAvailability> availability)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Teacher entity;
        if (teacher.Id == 0)
        {
            entity = new Teacher();
            db.Teachers.Add(entity);
        }
        else
        {
            entity = await db.Teachers.SingleOrDefaultAsync(x => x.Id == teacher.Id)
                ?? throw new InvalidOperationException("Учитель не найден.");
        }
        entity.FullName = teacher.FullName.Trim();
        entity.IsActive = teacher.IsActive;
        await db.SaveChangesAsync();

        db.TeacherAvailabilities.RemoveRange(db.TeacherAvailabilities.Where(x => x.TeacherId == entity.Id));
        db.TeacherAvailabilities.AddRange(availability.Select(x => new TeacherAvailability
        {
            TeacherId = entity.Id, DayOfWeek = x.DayOfWeek,
            LessonPeriodId = x.LessonPeriodId, IsAvailable = x.IsAvailable
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return entity;
    }

    public async Task ArchiveTeacherAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var teacher = await db.Teachers.FindAsync(id);
        if (teacher is null) return;
        teacher.IsActive = false;
        await db.SaveChangesAsync();
    }

    public async Task<bool> TeacherExistsAsync(string fullName, int? excludedId = null)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var normalized = fullName.Trim();
        var names = await db.Teachers.AsNoTracking()
            .Where(x => !excludedId.HasValue || x.Id != excludedId.Value)
            .Select(x => x.FullName)
            .ToListAsync();
        return names.Any(x => string.Equals(x.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }
}
