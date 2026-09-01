using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface IGroupService
{
    Task<List<SchoolGroup>> GetGroupsAsync();
    Task<List<SchoolClass>> GetClassesAsync();
    Task<List<Subject>> GetSubjectsAsync();
    Task<SchoolGroup> SaveAsync(SchoolGroup group);
    Task ArchiveAsync(int id);
    Task<bool> ExistsAsync(int classId, string name, int? excludedId = null);
}
