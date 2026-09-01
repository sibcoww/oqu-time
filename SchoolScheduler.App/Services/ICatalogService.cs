using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.Services;

public interface ICatalogService
{
    Task<List<Subject>> GetSubjectsAsync();
    Task<Subject> SaveSubjectAsync(Subject subject);
    Task<bool> SubjectExistsAsync(string name, int? excludedId = null);
    Task<List<Room>> GetRoomsAsync();
    Task<Room?> GetRoomAsync(int id);
    Task<Room> SaveRoomAsync(Room room, IReadOnlyCollection<RoomAvailability> availability);
    Task ArchiveRoomAsync(int id);
    Task<bool> RoomExistsAsync(string name, int? excludedId = null);
}
