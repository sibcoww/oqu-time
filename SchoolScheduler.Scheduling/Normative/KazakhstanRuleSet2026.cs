namespace SchoolScheduler.Scheduling.Normative;

public static class KazakhstanRuleSet2026
{
    public const string SanitarySource = "Приказ МЗ РК от 05.08.2021 № ҚР ДСМ-76, пункты 71, 72, 75 и приложения 3–4 (редакция, актуальная на 01.09.2026)";
    public const string SanitarySourceUrl = "https://adilet.zan.kz/rus/docs/V2100023890";

    public static INormativeRuleSet Create() => new NormativeRuleSet("KZ", "2026–2027", "KZ-2026.1",
        new DateOnly(2026, 9, 1), $"{SanitarySource}; {SanitarySourceUrl}",
        [
            new MaximumLessonDurationRule(45, SanitarySource),
            new BreakDurationRule(5, 30, SanitarySource),
            new ShiftIntervalRule(40, SanitarySource),
            new WeeklyClassLoadRule(new Dictionary<int, decimal>
            {
                [1] = 24, [2] = 25, [3] = 29, [4] = 29, [5] = 33, [6] = 33,
                [7] = 34, [8] = 36, [9] = 38, [10] = 39, [11] = 39
            }, SanitarySource)
        ]);
}
