using System.Threading.Tasks;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface ISchoolSetupService
{
    Task<bool> IsSchoolConfiguredAsync();
    Task SaveSetupAsync(School school, AcademicYear year, IReadOnlyCollection<Shift> shifts);
    Task<(School School, IReadOnlyList<Shift> Shifts)> GetTimeModelAsync();
    Task SaveBellScheduleAsync(IReadOnlyCollection<Shift> shifts);
}
