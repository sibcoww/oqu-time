namespace SchoolScheduler.Core.Models;

public class TeachingLoad
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int ClassId { get; set; }
    public int? GroupId { get; set; }
    public decimal HoursPerWeek { get; set; }
    public int? RoomId { get; set; }
    public bool AllowZeroLesson { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Teacher? Teacher { get; set; }
    public Subject? Subject { get; set; }
    public SchoolClass? Class { get; set; }
    public SchoolGroup? Group { get; set; }
    public Room? Room { get; set; }
}
