using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Normative;

namespace SchoolScheduler.Tests.Scheduling;

public sealed class NormativeRuleSetTests
{
    [Fact]
    public void KazakhstanSet_HasVersionedOfficialMetadataAndReplaceableRules()
    {
        var set = KazakhstanRuleSet2026.Create();
        Assert.Equal("KZ", set.Country);
        Assert.Equal("2026–2027", set.AcademicYear);
        Assert.Equal(new DateOnly(2026, 9, 1), set.EffectiveFrom);
        Assert.Contains("adilet.zan.kz", set.Source);
        Assert.All(set.Rules, rule => Assert.IsAssignableFrom<INormativeRule>(rule));
    }

    [Fact]
    public void Evaluate_ReportsLongLessonAndShortBreak()
    {
        var periods = new[]
        {
            Period(1, 1, 8, 0, 8, 46),
            Period(1, 2, 8, 49, 9, 34),
            Period(1, 3, 9, 39, 10, 24),
            Period(1, 4, 10, 29, 11, 14)
        };

        var violations = KazakhstanRuleSet2026.Create().Evaluate(Context(periods: periods));

        Assert.Contains(violations, x => x.RuleCode == "RK.LESSON_DURATION");
        Assert.Contains(violations, x => x.RuleCode == "RK.BREAK_DURATION" && x.Message.Contains("3 мин."));
        Assert.Contains(violations, x => x.RuleCode == "RK.BREAK_DURATION" && x.Message.Contains("большой перемены"));
    }

    [Fact]
    public void Evaluate_AcceptsThirtyMinuteLongBreakAndFortyMinuteShiftInterval()
    {
        var periods = new[]
        {
            Period(1, 1, 8, 0, 8, 45), Period(1, 2, 8, 50, 9, 35),
            Period(1, 3, 10, 5, 10, 50), Period(1, 4, 10, 55, 11, 40),
            Period(2, 1, 12, 20, 13, 5), Period(2, 2, 13, 10, 13, 55),
            Period(2, 3, 14, 25, 15, 10), Period(2, 4, 15, 15, 16, 0)
        };

        var violations = KazakhstanRuleSet2026.Create().Evaluate(Context(periods: periods));

        Assert.DoesNotContain(violations, x => x.RuleCode is "RK.LESSON_DURATION" or "RK.BREAK_DURATION" or "RK.SHIFT_INTERVAL");
    }

    [Fact]
    public void Evaluate_ReportsWeeklyLoadAboveParallelLimit()
    {
        var schoolClass = new SchoolClass { Id = 10, Name = "5А", Parallel = 5 };
        var loads = new[]
        {
            new TeachingLoad { ClassId = 10, HoursPerWeek = 20 },
            new TeachingLoad { ClassId = 10, HoursPerWeek = 14 }
        };

        var violation = Assert.Single(KazakhstanRuleSet2026.Create()
            .Evaluate(Context([schoolClass], loads)), x => x.RuleCode == "RK.WEEKLY_LOAD");
        Assert.Equal(NormativeSeverity.Critical, violation.Severity);
        Assert.Contains("34", violation.Message);
        Assert.Contains("33", violation.Message);
    }

    private static NormativeContext Context(IReadOnlyList<SchoolClass>? classes = null,
        IReadOnlyList<TeachingLoad>? loads = null, IReadOnlyList<LessonPeriod>? periods = null) =>
        new(classes ?? [], loads ?? [], [], periods ?? []);

    private static LessonPeriod Period(int shift, int number, int startHour, int startMinute, int endHour, int endMinute) =>
        new() { ShiftId = shift, Number = number, StartTime = new(startHour, startMinute, 0), EndTime = new(endHour, endMinute, 0) };
}
