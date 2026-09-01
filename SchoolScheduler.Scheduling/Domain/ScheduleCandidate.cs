namespace SchoolScheduler.Scheduling.Domain;

public sealed record ScheduledLesson(int LessonDemandId, int OccurrenceIndex, int TimeSlotId);

public sealed record ScheduleCandidate(IReadOnlyList<ScheduledLesson> Lessons, ScheduleScore Score,
    bool IsFeasible, IReadOnlyList<string> Diagnostics);

public sealed record ScheduleScore(int TotalPenalty, IReadOnlyDictionary<string, int> Penalties)
{
    public static ScheduleScore Empty { get; } = new(0, new Dictionary<string, int>());
}
