using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public sealed record TeachingLoadReferences(List<Teacher> Teachers, List<Subject> Subjects,
    List<SchoolClass> Classes, List<SchoolGroup> Groups, List<Room> Rooms);

public interface ITeachingLoadService
{
    Task<List<TeachingLoad>> GetAllAsync();
    Task<TeachingLoadReferences> GetReferencesAsync();
    Task SaveAllAsync(IReadOnlyCollection<TeachingLoad> rows);
    Task DeleteAsync(int id);
}
