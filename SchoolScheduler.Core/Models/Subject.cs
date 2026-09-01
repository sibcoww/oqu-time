namespace SchoolScheduler.Core.Models;

public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int Difficulty { get; set; } = 1;
    public SubjectType Type { get; set; } = SubjectType.Required;
    public bool AllowDoubleLessons { get; set; }
}

public enum SubjectType
{
    Required,
    Elective,
    Variable
}
