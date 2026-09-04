using System;

namespace SchoolScheduler.Core.Models;

public class LessonPeriod
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public int Number { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Shift? Shift { get; set; }
}
