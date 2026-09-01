namespace SchoolScheduler.Core.Models;

public class SchoolGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public int? SubjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public SchoolClass? Class { get; set; }
    public Subject? Subject { get; set; }
}
