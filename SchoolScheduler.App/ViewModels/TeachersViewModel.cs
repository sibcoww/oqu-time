using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class TeachersViewModel(ITeacherService teacherService, IDialogService dialogService) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Teacher> _teachers = new();
    [ObservableProperty] private Teacher? _selectedTeacher;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private ObservableCollection<AvailabilityDayRow> _availability = CreateAvailabilityRows();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var teachers = await teacherService.GetTeachersAsync(SearchText);
        Teachers = new ObservableCollection<Teacher>(teachers);
    }

    partial void OnSelectedTeacherChanged(Teacher? value) => _ = LoadSelectedAsync(value);

    private async Task LoadSelectedAsync(Teacher? teacher)
    {
        if (teacher is null) return;
        var details = await teacherService.GetTeacherAsync(teacher.Id);
        if (details is null) return;
        FullName = details.FullName;
        IsActive = details.IsActive;
        Availability = CreateAvailabilityRows(details.Availability);
    }

    [RelayCommand]
    private void Add()
    {
        SelectedTeacher = null;
        FullName = string.Empty;
        IsActive = true;
        Availability = CreateAvailabilityRows();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            dialogService.ShowError("Укажите ФИО учителя.");
            return;
        }
        var id = SelectedTeacher?.Id ?? 0;
        if (await teacherService.TeacherExistsAsync(FullName, id == 0 ? null : id))
        {
            dialogService.ShowError("Учитель с таким ФИО уже существует.");
            return;
        }
        try
        {
            var slots = Availability.SelectMany(x => x.ToEntities()).ToList();
            await teacherService.SaveTeacherAsync(new Teacher { Id = id, FullName = FullName, IsActive = IsActive }, slots);
            await LoadAsync();
            SelectedTeacher = Teachers.FirstOrDefault(x => x.Id == id) ?? Teachers.LastOrDefault();
        }
        catch (Exception ex)
        {
            dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (SelectedTeacher is null) return;
        await teacherService.ArchiveTeacherAsync(SelectedTeacher.Id);
        await LoadAsync();
        SelectedTeacher = null;
    }

    private static ObservableCollection<AvailabilityDayRow> CreateAvailabilityRows(
        IEnumerable<TeacherAvailability>? saved = null)
    {
        var values = saved?.ToDictionary(x => (x.DayOfWeek, x.LessonNumber), x => x.IsAvailable);
        string[] names = ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота"];
        return new(names.Select((name, index) => new AvailabilityDayRow(index + 1, name, values)));
    }
}

public partial class AvailabilityDayRow : ObservableObject
{
    public int DayOfWeek { get; }
    public string DayName { get; }
    [ObservableProperty] private bool _lesson0;
    [ObservableProperty] private bool _lesson1;
    [ObservableProperty] private bool _lesson2;
    [ObservableProperty] private bool _lesson3;
    [ObservableProperty] private bool _lesson4;
    [ObservableProperty] private bool _lesson5;
    [ObservableProperty] private bool _lesson6;
    [ObservableProperty] private bool _lesson7;
    [ObservableProperty] private bool _lesson8;

    public AvailabilityDayRow(int dayOfWeek, string dayName, IReadOnlyDictionary<(int, int), bool>? values)
    {
        DayOfWeek = dayOfWeek;
        DayName = dayName;
        bool Value(int lesson) => values is null || !values.TryGetValue((dayOfWeek, lesson), out var value) || value;
        Lesson0 = Value(0); Lesson1 = Value(1); Lesson2 = Value(2); Lesson3 = Value(3); Lesson4 = Value(4);
        Lesson5 = Value(5); Lesson6 = Value(6); Lesson7 = Value(7); Lesson8 = Value(8);
    }

    public IEnumerable<TeacherAvailability> ToEntities()
    {
        bool[] values = [Lesson0, Lesson1, Lesson2, Lesson3, Lesson4, Lesson5, Lesson6, Lesson7, Lesson8];
        return values.Select((value, lesson) => new TeacherAvailability
            { DayOfWeek = DayOfWeek, LessonNumber = lesson, IsAvailable = value });
    }
}
