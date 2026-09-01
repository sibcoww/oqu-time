namespace SchoolScheduler.Core.Models;

public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DaysPerWeek { get; set; } = 5;
    public string Region { get; set; } = "KZ";
    public bool UseRegionalNorms { get; set; } = false;
}