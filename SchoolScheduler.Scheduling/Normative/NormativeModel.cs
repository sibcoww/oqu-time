using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Scheduling.Normative;

public enum NormativeSeverity { Information, Recommendation, Critical }

public sealed record NormativeViolation(string RuleCode, NormativeSeverity Severity,
    string Message, string Source);

public sealed record NormativeContext(IReadOnlyList<SchoolClass> Classes,
    IReadOnlyList<TeachingLoad> TeachingLoads, IReadOnlyList<Shift> Shifts,
    IReadOnlyList<LessonPeriod> LessonPeriods);

public interface INormativeRule
{
    string Code { get; }
    IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context);
}

public interface INormativeRuleSet
{
    string Country { get; }
    string AcademicYear { get; }
    string Version { get; }
    DateOnly EffectiveFrom { get; }
    string Source { get; }
    IReadOnlyList<INormativeRule> Rules { get; }
    IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context);
}

public sealed class NormativeRuleSet(string country, string academicYear, string version,
    DateOnly effectiveFrom, string source, IReadOnlyList<INormativeRule> rules) : INormativeRuleSet
{
    public string Country { get; } = country;
    public string AcademicYear { get; } = academicYear;
    public string Version { get; } = version;
    public DateOnly EffectiveFrom { get; } = effectiveFrom;
    public string Source { get; } = source;
    public IReadOnlyList<INormativeRule> Rules { get; } = rules;
    public IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context) =>
        Rules.SelectMany(x => x.Evaluate(context)).ToList();
}
