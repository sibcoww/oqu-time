namespace SchoolScheduler.Core.Models;

public class TeachingLoad
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int ClassId { get; set; }
    public int HoursPerWeek { get; set; }
}