using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class RoomsViewModel(ICatalogService service, IDialogService dialogs) : ViewModelBase
{
    public IReadOnlyList<RoomType> RoomTypes { get; } = Enum.GetValues<RoomType>();
    [ObservableProperty] private ObservableCollection<Room> _rooms = new();
    [ObservableProperty] private Room? _selectedRoom;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private RoomType _type;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private ObservableCollection<AvailabilityDayRow> _availability = CreateRows();

    [RelayCommand] private async Task LoadAsync() => Rooms = new(await service.GetRoomsAsync());
    partial void OnSelectedRoomChanged(Room? value) => _ = LoadSelectedAsync(value);

    private async Task LoadSelectedAsync(Room? room)
    {
        if (room is null) return;
        var saved = await service.GetRoomAsync(room.Id); if (saved is null) return;
        Name = saved.Name; Type = saved.Type; IsActive = saved.IsActive;
        Availability = CreateRows(saved.Availability);
    }

    [RelayCommand]
    private void Add()
    { SelectedRoom = null; Name = string.Empty; Type = RoomType.Standard; IsActive = true; Availability = CreateRows(); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { dialogs.ShowError("Укажите номер или название кабинета."); return; }
        var id = SelectedRoom?.Id ?? 0;
        if (await service.RoomExistsAsync(Name, id == 0 ? null : id))
        { dialogs.ShowError("Кабинет с таким номером или названием уже существует."); return; }
        var slots = Availability.SelectMany(x => x.ToEntities()).Select(x => new RoomAvailability
        { DayOfWeek = x.DayOfWeek, LessonNumber = x.LessonNumber, IsAvailable = x.IsAvailable }).ToList();
        await service.SaveRoomAsync(new Room { Id = id, Name = Name, Type = Type, IsActive = IsActive }, slots);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    { if (SelectedRoom is null) return; await service.ArchiveRoomAsync(SelectedRoom.Id); await LoadAsync(); SelectedRoom = null; }

    private static ObservableCollection<AvailabilityDayRow> CreateRows(IEnumerable<RoomAvailability>? saved = null)
    {
        var values = saved?.ToDictionary(x => (x.DayOfWeek, x.LessonNumber), x => x.IsAvailable);
        string[] names = ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота"];
        return new(names.Select((name, index) => new AvailabilityDayRow(index + 1, name, values)));
    }
}
