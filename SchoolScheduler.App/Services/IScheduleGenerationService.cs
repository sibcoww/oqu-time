using SchoolScheduler.Core.Models;
using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.App.Services;

public sealed record GeneratedSchedule(SchedulingProblem Problem, ScheduleCandidate Candidate,
    IReadOnlyList<Teacher> Teachers, IReadOnlyList<Subject> Subjects,
    IReadOnlyList<SchoolClass> Classes, IReadOnlyList<SchoolGroup> Groups,
    IReadOnlyList<Room> Rooms, int DaysPerWeek);
public sealed record PreservedScheduleAssignment(int LessonDemandId, int OccurrenceIndex, int TimeSlotId);

public interface IScheduleGenerationService
{
    Task<GeneratedSchedule> GenerateAsync(CancellationToken cancellationToken = default);
    Task<GeneratedSchedule> ReoptimizeAsync(GeneratedSchedule current,
        IReadOnlyCollection<PreservedScheduleAssignment> preservedAssignments,
        CancellationToken cancellationToken = default);
}
