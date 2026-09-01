using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public sealed class GroupService(IDbContextFactory<AppDbContext> factory) : IGroupService
{
    public async Task<List<SchoolGroup>> GetGroupsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SchoolGroups.AsNoTracking().Include(x => x.Class).Include(x => x.Subject)
            .OrderBy(x => x.Class!.Parallel).ThenBy(x => x.Class!.Letter).ThenBy(x => x.Name).ToListAsync();
    }

    public async Task<List<SchoolClass>> GetClassesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SchoolClasses.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Parallel).ThenBy(x => x.Letter).ToListAsync();
    }

    public async Task<List<Subject>> GetSubjectsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<SchoolGroup> SaveAsync(SchoolGroup group)
    {
        await using var db = await factory.CreateDbContextAsync();
        SchoolGroup entity;
        if (group.Id == 0) { entity = new SchoolGroup(); db.SchoolGroups.Add(entity); }
        else entity = await db.SchoolGroups.FindAsync(group.Id) ?? throw new InvalidOperationException("Группа не найдена.");
        entity.Name = group.Name.Trim(); entity.ClassId = group.ClassId;
        entity.SubjectId = group.SubjectId; entity.IsActive = group.IsActive;
        await db.SaveChangesAsync(); return entity;
    }

    public async Task ArchiveAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var group = await db.SchoolGroups.FindAsync(id); if (group is null) return;
        group.IsActive = false; await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int classId, string name, int? excludedId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var names = await db.SchoolGroups.AsNoTracking()
            .Where(x => x.ClassId == classId && (!excludedId.HasValue || x.Id != excludedId.Value))
            .Select(x => x.Name).ToListAsync();
        return names.Any(x => string.Equals(x.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
