using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface ISchoolClassService
{
    Task<List<SchoolClass>> GetAllClassesAsync();
    Task<List<Shift>> GetShiftsAsync();
    Task<SchoolClass> AddClassAsync(SchoolClass schoolClass);
    Task UpdateClassAsync(SchoolClass schoolClass);
    Task ArchiveClassAsync(int id);
    Task BulkCreateClassesAsync(int startParallel, int endParallel, List<string> letters, int shiftId, int maxLessonsPerDay);
    Task<bool> ClassExistsAsync(int parallel, string letter, int? excludedId = null);
}
