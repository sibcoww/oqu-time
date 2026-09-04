using Microsoft.EntityFrameworkCore;
using SchoolScheduler.Core.Models;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.Services;

public sealed class CatalogService(IDbContextFactory<AppDbContext> factory) : ICatalogService
{
    public async Task<List<Subject>> GetSubjectsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<Subject> SaveSubjectAsync(Subject subject)
    {
        await using var db = await factory.CreateDbContextAsync();
        Subject entity;
        if (subject.Id == 0) { entity = new Subject(); db.Subjects.Add(entity); }
        else entity = await db.Subjects.FindAsync(subject.Id) ?? throw new InvalidOperationException("Предмет не найден.");
        entity.Name = subject.Name.Trim(); entity.ShortName = subject.ShortName.Trim();
        entity.Difficulty = subject.Difficulty; entity.Type = subject.Type;
        entity.AllowDoubleLessons = subject.AllowDoubleLessons;
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> SubjectExistsAsync(string name, int? excludedId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var names = await db.Subjects.AsNoTracking().Where(x => !excludedId.HasValue || x.Id != excludedId)
            .Select(x => x.Name).ToListAsync();
        return names.Any(x => string.Equals(x.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Room>> GetRoomsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Rooms.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<Room?> GetRoomAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Rooms.AsNoTracking().Include(x => x.Availability).SingleOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Room> SaveRoomAsync(Room room, IReadOnlyCollection<RoomAvailability> availability)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Room entity;
        if (room.Id == 0) { entity = new Room(); db.Rooms.Add(entity); }
        else entity = await db.Rooms.FindAsync(room.Id) ?? throw new InvalidOperationException("Кабинет не найден.");
        entity.Name = room.Name.Trim(); entity.Type = room.Type; entity.IsActive = room.IsActive;
        await db.SaveChangesAsync();
        db.RoomAvailabilities.RemoveRange(db.RoomAvailabilities.Where(x => x.RoomId == entity.Id));
        db.RoomAvailabilities.AddRange(availability.Select(x => new RoomAvailability
        { RoomId = entity.Id, DayOfWeek = x.DayOfWeek, LessonPeriodId = x.LessonPeriodId, IsAvailable = x.IsAvailable }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return entity;
    }

    public async Task ArchiveRoomAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var room = await db.Rooms.FindAsync(id); if (room is null) return;
        room.IsActive = false; await db.SaveChangesAsync();
    }

    public async Task<bool> RoomExistsAsync(string name, int? excludedId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var names = await db.Rooms.AsNoTracking().Where(x => !excludedId.HasValue || x.Id != excludedId)
            .Select(x => x.Name).ToListAsync();
        return names.Any(x => string.Equals(x.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
