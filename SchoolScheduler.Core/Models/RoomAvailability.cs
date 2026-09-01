namespace SchoolScheduler.Core.Models;

public class RoomAvailability
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int DayOfWeek { get; set; }
    public int LessonNumber { get; set; }
    public bool IsAvailable { get; set; } = true;
    public Room? Room { get; set; }
}
