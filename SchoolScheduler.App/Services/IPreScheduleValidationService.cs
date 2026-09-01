using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.App.Services;

public interface IPreScheduleValidationService
{
    Task<IReadOnlyList<ValidationIssue>> ValidateAsync();
}
