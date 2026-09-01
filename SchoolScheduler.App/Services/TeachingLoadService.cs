using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public sealed class TeachingLoadService(IDbContextFactory<AppDbContext> factory) : ITeachingLoadService
{
    public async Task<List<TeachingLoad>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TeachingLoads.AsNoTracking().Include(x => x.Teacher).Include(x => x.Subject)
            .Include(x => x.Class).Include(x => x.Group).Include(x => x.Room)
            .OrderBy(x => x.Class!.Parallel).ThenBy(x => x.Class!.Letter).ThenBy(x => x.Subject!.Name).ToListAsync();
    }

    public async Task<TeachingLoadReferences> GetReferencesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return new(
            await db.Teachers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync(),
            await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            await db.SchoolClasses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Parallel).ThenBy(x => x.Letter).ToListAsync(),
            await db.SchoolGroups.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            await db.Rooms.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
    }

    public async Task SaveAllAsync(IReadOnlyCollection<TeachingLoad> rows)
    {
        await using var db = await factory.CreateDbContextAsync();
        var groupClasses = await db.SchoolGroups.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ClassId);
        foreach (var row in rows)
        {
            if (row.TeacherId <= 0 || row.SubjectId <= 0 || row.ClassId <= 0 || row.HoursPerWeek <= 0)
                throw new InvalidOperationException("В каждой строке выберите учителя, предмет, класс и положительное количество часов.");
            if (row.GroupId.HasValue && (!groupClasses.TryGetValue(row.GroupId.Value, out var classId) || classId != row.ClassId))
                throw new InvalidOperationException("Выбранная группа не принадлежит классу строки нагрузки.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var row in rows)
        {
            TeachingLoad entity;
            if (row.Id == 0) { entity = new TeachingLoad(); db.TeachingLoads.Add(entity); }
            else entity = await db.TeachingLoads.FindAsync(row.Id) ?? throw new InvalidOperationException("Строка нагрузки не найдена.");
            entity.TeacherId = row.TeacherId; entity.SubjectId = row.SubjectId; entity.ClassId = row.ClassId;
            entity.GroupId = row.GroupId; entity.HoursPerWeek = row.HoursPerWeek; entity.RoomId = row.RoomId;
            entity.AllowZeroLesson = row.AllowZeroLesson; entity.Comment = row.Comment?.Trim() ?? string.Empty;
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.TeachingLoads.FindAsync(id); if (row is null) return;
        db.Remove(row); await db.SaveChangesAsync();
    }
}
