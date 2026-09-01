using SchoolScheduler.Core.Models;

namespace SchoolScheduler.Scheduling.Normative;

public sealed class MaximumLessonDurationRule(int maximumMinutes, string source) : INormativeRule
{
    public string Code => "RK.LESSON_DURATION";
    public IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context) => context.LessonPeriods
        .Where(x => x.EndTime - x.StartTime > TimeSpan.FromMinutes(maximumMinutes))
        .Select(x => new NormativeViolation(Code, NormativeSeverity.Critical,
            $"Смена #{x.ShiftId}, урок {x.Number}: продолжительность {(x.EndTime - x.StartTime).TotalMinutes:0} мин., максимум — {maximumMinutes} мин.", source)).ToList();
}

public sealed class BreakDurationRule(int minimumMinutes, int longBreakMinutes, string source) : INormativeRule
{
    public string Code => "RK.BREAK_DURATION";
    public IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context)
    {
        var violations = new List<NormativeViolation>();
        foreach (var shift in context.LessonPeriods.GroupBy(x => x.ShiftId))
        {
            var periods = shift.OrderBy(x => x.Number).ToList();
            var gaps = periods.Zip(periods.Skip(1), (a, b) => (After: a.Number, Minutes: (b.StartTime - a.EndTime).TotalMinutes)).ToList();
            violations.AddRange(gaps.Where(x => x.Minutes < minimumMinutes).Select(x =>
                new NormativeViolation(Code, NormativeSeverity.Critical,
                    $"Смена #{shift.Key}: перемена после урока {x.After} длится {x.Minutes:0} мин., минимум — {minimumMinutes} мин.", source)));
            var hasLongBreak = gaps.Any(x => (x.After == 2 || x.After == 3) && x.Minutes >= longBreakMinutes);
            var hasTwoBreaks = gaps.Any(x => x.After == 2 && x.Minutes >= 15) && gaps.Any(x => x.After == 4 && x.Minutes >= 15);
            if (periods.Count >= 4 && !hasLongBreak && !hasTwoBreaks)
                violations.Add(new(Code, NormativeSeverity.Critical,
                    $"Смена #{shift.Key}: нет большой перемены {longBreakMinutes} мин. после 2-го/3-го урока или двух перемен по 15 мин. после 2-го/4-го.", source));
        }
        return violations;
    }
}

public sealed class ShiftIntervalRule(int minimumMinutes, string source) : INormativeRule
{
    public string Code => "RK.SHIFT_INTERVAL";
    public IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context)
    {
        var ranges = context.LessonPeriods.GroupBy(x => x.ShiftId)
            .Select(x => (Shift: x.Key, Start: x.Min(y => y.StartTime), End: x.Max(y => y.EndTime)))
            .OrderBy(x => x.Start).ToList();
        return ranges.Zip(ranges.Skip(1), (a, b) => (a, b, Gap: (b.Start - a.End).TotalMinutes))
            .Where(x => x.Gap < minimumMinutes)
            .Select(x => new NormativeViolation(Code, NormativeSeverity.Critical,
                $"Интервал между сменами #{x.a.Shift} и #{x.b.Shift} — {x.Gap:0} мин., минимум — {minimumMinutes} мин.", source)).ToList();
    }
}

public sealed class WeeklyClassLoadRule(IReadOnlyDictionary<int, decimal> maximumByParallel, string source) : INormativeRule
{
    public string Code => "RK.WEEKLY_LOAD";
    public IReadOnlyList<NormativeViolation> Evaluate(NormativeContext context)
    {
        var classes = context.Classes.ToDictionary(x => x.Id);
        return context.TeachingLoads.GroupBy(x => x.ClassId).Select(x =>
            (Class: classes.GetValueOrDefault(x.Key), Hours: x.Sum(y => y.HoursPerWeek)))
            .Where(x => x.Class is not null && maximumByParallel.TryGetValue(x.Class.Parallel, out var max) && x.Hours > max)
            .Select(x => new NormativeViolation(Code, NormativeSeverity.Critical,
                $"{x.Class!.Name}: недельная нагрузка {x.Hours:0.##} ч., максимум для {x.Class.Parallel} класса — {maximumByParallel[x.Class.Parallel]:0.##} ч.", source)).ToList();
    }
}
