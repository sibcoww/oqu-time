using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class TeachersViewModel(ITeacherService teacherService, ISchoolSetupService setupService,
    IDialogService dialogService) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Teacher> _teachers = new();
    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private ObservableCollection<AvailabilityPeriodRow> _availability = new();
    [ObservableProperty] private ObservableCollection<string> _dayNames = new();
    private IReadOnlyList<Shift> _shifts = [];
    private int _daysPerWeek;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var time = await setupService.GetTimeModelAsync();
        _shifts = time.Shifts; _daysPerWeek = time.School.DaysPerWeek;
        DayNames = new(DayNamesFor(_daysPerWeek));
        Teachers = new(await teacherService.GetTeachersAsync(SearchText));
        if (SelectedTeacher is null) Availability = CreateRows();
    }

    partial void OnSelectedTeacherChanged(Teacher? value) => _ = LoadSelectedAsync(value);
    private async Task LoadSelectedAsync(Teacher? teacher)
    {
        if (teacher is null) return;
        var details = await teacherService.GetTeacherAsync(teacher.Id); if (details is null) return;
        FullName = details.FullName; IsActive = details.IsActive;
        Availability = CreateRows(details.Availability);
    }

    [RelayCommand] private void Add() { SelectedTeacher = null; FullName = ""; IsActive = true; Availability = CreateRows(); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName)) { dialogService.ShowError("Укажите ФИО учителя."); return; }
        var id = SelectedTeacher?.Id ?? 0;
        if (await teacherService.TeacherExistsAsync(FullName, id == 0 ? null : id)) { dialogService.ShowError("Учитель с таким ФИО уже существует."); return; }
        try
        {
            var slots = Availability.SelectMany(x => x.Cells.Select(c => new TeacherAvailability
                { LessonPeriodId = x.LessonPeriodId, DayOfWeek = c.DayOfWeek, IsAvailable = c.IsAvailable })).ToList();
            var saved = await teacherService.SaveTeacherAsync(new Teacher { Id = id, FullName = FullName, IsActive = IsActive }, slots);
            await LoadAsync(); SelectedTeacher = Teachers.FirstOrDefault(x => x.Id == saved.Id);
        }
        catch (Exception ex) { dialogService.ShowError($"Ошибка сохранения: {ex.Message}"); }
    }

    [RelayCommand] private async Task ArchiveAsync()
    { if (SelectedTeacher is null) return; await teacherService.ArchiveTeacherAsync(SelectedTeacher.Id); await LoadAsync(); SelectedTeacher = null; }

    private ObservableCollection<AvailabilityPeriodRow> CreateRows(IEnumerable<TeacherAvailability>? saved = null)
    {
        var values = saved?.ToDictionary(x => (x.LessonPeriodId, x.DayOfWeek), x => x.IsAvailable);
        return new(_shifts.SelectMany(s => s.LessonPeriods.OrderBy(p => p.Number).Select(p =>
            new AvailabilityPeriodRow(p.Id, s.Name, p.Number, _daysPerWeek, values))));
    }

    internal static IEnumerable<string> DayNamesFor(int count) =>
        new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" }.Take(count);
}

public sealed class AvailabilityPeriodRow
{
    public int LessonPeriodId { get; }
    public string ShiftName { get; }
    public int LessonNumber { get; }
    public ObservableCollection<AvailabilityCell> Cells { get; }
    public AvailabilityPeriodRow(int id, string shift, int number, int days,
        IReadOnlyDictionary<(int, int), bool>? values)
    {
        LessonPeriodId = id; ShiftName = shift; LessonNumber = number;
        Cells = new(Enumerable.Range(1, days).Select(day => new AvailabilityCell(day,
            values is null || !values.TryGetValue((id, day), out var value) || value)));
    }
}

public partial class AvailabilityCell(int dayOfWeek, bool isAvailable) : ObservableObject
{
    public int DayOfWeek { get; } = dayOfWeek;
    [ObservableProperty] private bool _isAvailable = isAvailable;
}
