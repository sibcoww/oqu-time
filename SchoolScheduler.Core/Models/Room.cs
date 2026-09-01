namespace SchoolScheduler.Core.Models;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RoomType Type { get; set; } = RoomType.Standard;
    public bool IsActive { get; set; } = true;
    public ICollection<RoomAvailability> Availability { get; set; } = new List<RoomAvailability>();
}

public enum RoomType
{
    Standard,
    ComputerScience,
    Laboratory,
    Chemistry,
    Physics,
    Workshop,
    Gym,
    PhysicalEducation,
    Other
}
