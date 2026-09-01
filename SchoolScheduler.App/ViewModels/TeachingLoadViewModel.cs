using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class TeachingLoadViewModel(ITeachingLoadService service, IDialogService dialogs) : ViewModelBase
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
}
