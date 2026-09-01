using System.Threading.Tasks;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface ISchoolSetupService
{
    Task<bool> IsSchoolConfiguredAsync();
    Task SaveSetupAsync(School school, AcademicYear year, int shiftCount, int lessonsPerShift);
}