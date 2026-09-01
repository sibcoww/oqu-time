using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.App.Services;

public sealed record GeneratedSchedule(SchedulingProblem Problem, ScheduleCandidate Candidate,
    IReadOnlyList<Teacher> Teachers, IReadOnlyList<Subject> Subjects,
    IReadOnlyList<SchoolClass> Classes, IReadOnlyList<SchoolGroup> Groups,
    IReadOnlyList<Room> Rooms, int DaysPerWeek);

public interface IScheduleGenerationService
{
    Task<GeneratedSchedule> GenerateAsync(CancellationToken cancellationToken = default);
}
