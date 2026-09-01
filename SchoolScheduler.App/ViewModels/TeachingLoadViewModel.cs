using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.App.ViewModels;

public partial class TeachingLoadViewModel(ITeachingLoadService service, IDialogService dialogs,
    IFileDialogService files, TeachingLoadExcelService excel) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<TeachingLoad> _rows = new();
    [ObservableProperty] private TeachingLoad? _selectedRow;
    [ObservableProperty] private ObservableCollection<Teacher> _teachers = new();
    [ObservableProperty] private ObservableCollection<Subject> _subjects = new();
    [ObservableProperty] private ObservableCollection<SchoolClass> _classes = new();
    [ObservableProperty] private ObservableCollection<SchoolGroup> _groups = new();
    [ObservableProperty] private ObservableCollection<Room> _rooms = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var references = await service.GetReferencesAsync();
        Teachers = new(references.Teachers); Subjects = new(references.Subjects); Classes = new(references.Classes);
        Groups = new(references.Groups); Rooms = new(references.Rooms);
        Rows = new(await service.GetAllAsync());
    }

    [RelayCommand]
    private void Add()
    {
        var row = new TeachingLoad { TeacherId = Teachers.FirstOrDefault()?.Id ?? 0,
            SubjectId = Subjects.FirstOrDefault()?.Id ?? 0, ClassId = Classes.FirstOrDefault()?.Id ?? 0, HoursPerWeek = 1 };
        Rows.Add(row); SelectedRow = row;
    }

    [RelayCommand]
    private void Copy()
    {
        if (SelectedRow is null) return;
        var copy = new TeachingLoad { TeacherId = SelectedRow.TeacherId, SubjectId = SelectedRow.SubjectId,
            ClassId = SelectedRow.ClassId, GroupId = SelectedRow.GroupId, HoursPerWeek = SelectedRow.HoursPerWeek,
            RoomId = SelectedRow.RoomId, AllowZeroLesson = SelectedRow.AllowZeroLesson, Comment = SelectedRow.Comment };
        Rows.Add(copy); SelectedRow = copy;
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        try { await service.SaveAllAsync(Rows); await LoadAsync(); dialogs.ShowMessage("Готово", "Учебная нагрузка сохранена."); }
        catch (Exception ex) { dialogs.ShowError(ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedRow is null) return;
        if (SelectedRow.Id > 0) await service.DeleteAsync(SelectedRow.Id);
        Rows.Remove(SelectedRow); SelectedRow = null;
    }

    [RelayCommand]
    private void SaveTemplate()
    {
        var path = files.ChooseExcelSavePath("Шаблон учебной нагрузки.xlsx"); if (path is null) return;
        try
        {
            excel.CreateTemplate(path, new(
                Teachers.Select(x => x.FullName).ToList(), Subjects.Select(x => x.Name).ToList(),
                Classes.Select(x => x.Name).ToList(), Groups.Select(x => x.Name).ToList(), Rooms.Select(x => x.Name).ToList()));
            dialogs.ShowMessage("Готово", "Шаблон Excel сохранён.");
        }
        catch (Exception ex) { dialogs.ShowError($"Не удалось сохранить шаблон: {ex.Message}"); }
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var path = files.ChooseExcelOpenPath(); if (path is null) return;
        var result = excel.Import(path);
        var mappingErrors = new List<string>(); var imported = new List<TeachingLoad>();
        foreach (var source in result.Rows)
        {
            var teacher = Find(Teachers, x => x.FullName, source.Teacher);
            var subject = Find(Subjects, x => x.Name, source.Subject);
            var schoolClass = Find(Classes, x => x.Name, source.SchoolClass);
            var group = source.Group is null ? null : Find(Groups, x => x.Name, source.Group);
            var room = source.Room is null ? null : Find(Rooms, x => x.Name, source.Room);
            if (teacher is null) mappingErrors.Add($"Строка {source.RowNumber}: учитель «{source.Teacher}» не найден.");
            if (subject is null) mappingErrors.Add($"Строка {source.RowNumber}: предмет «{source.Subject}» не найден.");
            if (schoolClass is null) mappingErrors.Add($"Строка {source.RowNumber}: класс «{source.SchoolClass}» не найден.");
            if (source.Group is not null && group is null) mappingErrors.Add($"Строка {source.RowNumber}: группа «{source.Group}» не найдена.");
            if (source.Room is not null && room is null) mappingErrors.Add($"Строка {source.RowNumber}: кабинет «{source.Room}» не найден.");
            if (teacher is null || subject is null || schoolClass is null || (source.Group is not null && group is null) || (source.Room is not null && room is null)) continue;
            imported.Add(new TeachingLoad { TeacherId = teacher.Id, SubjectId = subject.Id, ClassId = schoolClass.Id,
                GroupId = group?.Id, RoomId = room?.Id, HoursPerWeek = source.HoursPerWeek,
                AllowZeroLesson = source.AllowZeroLesson, Comment = source.Comment });
        }
        var errors = result.Errors.Concat(mappingErrors).ToList();
        if (errors.Count > 0) { dialogs.ShowError(string.Join(Environment.NewLine, errors.Take(15))); return; }
        Rows = new(imported); SelectedRow = Rows.FirstOrDefault();
        dialogs.ShowMessage("Предпросмотр", $"Загружено строк: {Rows.Count}. Проверьте таблицу и нажмите «Сохранить все».");
    }

    private static T? Find<T>(IEnumerable<T> items, Func<T, string> name, string value) where T : class =>
        items.FirstOrDefault(x => string.Equals(name(x).Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
}
