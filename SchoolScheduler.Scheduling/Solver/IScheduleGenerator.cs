using SchoolScheduler.Scheduling.Domain;

namespace SchoolScheduler.Scheduling.Solver;

public interface IScheduleGenerator
{
    ScheduleCandidate Generate(SchedulingProblem problem, TimeSpan? timeLimit = null);
}
