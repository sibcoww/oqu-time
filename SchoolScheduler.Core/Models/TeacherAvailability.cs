namespace SchoolScheduler.Core.Models;

public class TeacherAvailability
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int DayOfWeek { get; set; }
    public int LessonPeriodId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public Teacher? Teacher { get; set; }
    public LessonPeriod? LessonPeriod { get; set; }
}
