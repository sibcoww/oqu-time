using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface ITeacherService
{
    Task<List<Teacher>> GetTeachersAsync(string? search = null);
    Task<Teacher?> GetTeacherAsync(int id);
    Task<Teacher> SaveTeacherAsync(Teacher teacher, IReadOnlyCollection<TeacherAvailability> availability);
    Task ArchiveTeacherAsync(int id);
    Task<bool> TeacherExistsAsync(string fullName, int? excludedId = null);
}
