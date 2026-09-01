using SchoolScheduler.Scheduling.Normative;

namespace SchoolScheduler.App.Services;

public sealed record NormativeCheckResult(INormativeRuleSet RuleSet,
    IReadOnlyList<NormativeViolation> Violations);

public interface INormativeValidationService
{
    Task<NormativeCheckResult> ValidateAsync(CancellationToken cancellationToken = default);
}
