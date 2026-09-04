using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class RoomsViewModel(ICatalogService service, ISchoolSetupService setupService, IDialogService dialogs) : ViewModelBase
{
    public IReadOnlyList<RoomType> RoomTypes { get; } = Enum.GetValues<RoomType>();
    [ObservableProperty] private ObservableCollection<Room> _rooms = new();
    [ObservableProperty] private Room? _selectedRoom;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private RoomType _type;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private ObservableCollection<AvailabilityPeriodRow> _availability = new();
    [ObservableProperty] private ObservableCollection<string> _dayNames = new();
    private IReadOnlyList<Shift> _shifts = [];
    private int _daysPerWeek;

    [RelayCommand] private async Task LoadAsync()
    {
        var time = await setupService.GetTimeModelAsync(); _shifts = time.Shifts; _daysPerWeek = time.School.DaysPerWeek;
        DayNames = new(TeachersViewModel.DayNamesFor(_daysPerWeek));
        Rooms = new(await service.GetRoomsAsync()); if (SelectedRoom is null) Availability = CreateRows();
    }
    partial void OnSelectedRoomChanged(Room? value) => _ = LoadSelectedAsync(value);
    private async Task LoadSelectedAsync(Room? room)
    { if (room is null) return; var saved = await service.GetRoomAsync(room.Id); if (saved is null) return;
      Name = saved.Name; Type = saved.Type; IsActive = saved.IsActive; Availability = CreateRows(saved.Availability); }
    [RelayCommand] private void Add() { SelectedRoom = null; Name = ""; Type = RoomType.Standard; IsActive = true; Availability = CreateRows(); }
    [RelayCommand] private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { dialogs.ShowError("Укажите номер или название кабинета."); return; }
        var id = SelectedRoom?.Id ?? 0;
        if (await service.RoomExistsAsync(Name, id == 0 ? null : id)) { dialogs.ShowError("Кабинет с таким названием уже существует."); return; }
        var slots = Availability.SelectMany(x => x.Cells.Select(c => new RoomAvailability
            { LessonPeriodId = x.LessonPeriodId, DayOfWeek = c.DayOfWeek, IsAvailable = c.IsAvailable })).ToList();
        var saved = await service.SaveRoomAsync(new Room { Id = id, Name = Name, Type = Type, IsActive = IsActive }, slots);
        await LoadAsync(); SelectedRoom = Rooms.FirstOrDefault(x => x.Id == saved.Id);
    }
    [RelayCommand] private async Task ArchiveAsync()
    { if (SelectedRoom is null) return; await service.ArchiveRoomAsync(SelectedRoom.Id); await LoadAsync(); SelectedRoom = null; }
    private ObservableCollection<AvailabilityPeriodRow> CreateRows(IEnumerable<RoomAvailability>? saved = null)
    {
        var values = saved?.ToDictionary(x => (x.LessonPeriodId, x.DayOfWeek), x => x.IsAvailable);
        return new(_shifts.SelectMany(s => s.LessonPeriods.OrderBy(p => p.Number).Select(p =>
            new AvailabilityPeriodRow(p.Id, s.Name, p.Number, _daysPerWeek, values))));
    }
}
