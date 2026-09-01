namespace SchoolScheduler.Core.Models;

public class AcademicYear
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}