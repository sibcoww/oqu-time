namespace SchoolScheduler.Core.Models;

public class SchoolClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Parallel { get; set; }
    public string Letter { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public int MaxLessonsPerDay { get; set; }
    public bool IsActive { get; set; } = true;
}