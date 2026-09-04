namespace SchoolScheduler.Core.Models;

public class Shift
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<LessonPeriod> LessonPeriods { get; set; } = new List<LessonPeriod>();
}
