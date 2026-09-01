using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class GroupsViewModel(IGroupService service, IDialogService dialogs) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<SchoolGroup> _groups = new();
    [ObservableProperty] private ObservableCollection<SchoolClass> _classes = new();
    [ObservableProperty] private ObservableCollection<Subject> _subjects = new();
    [ObservableProperty] private SchoolGroup? _selectedGroup;

    [RelayCommand] private async Task LoadAsync()
    { Classes = new(await service.GetClassesAsync()); Subjects = new(await service.GetSubjectsAsync()); Groups = new(await service.GetGroupsAsync()); }

    [RelayCommand] private void Add() => SelectedGroup = new SchoolGroup
    { ClassId = Classes.FirstOrDefault()?.Id ?? 0, Name = "Группа 1", IsActive = true };

    [RelayCommand] private async Task SaveAsync()
    {
        if (SelectedGroup is null) return;
        if (SelectedGroup.ClassId <= 0 || string.IsNullOrWhiteSpace(SelectedGroup.Name))
        { dialogs.ShowError("Выберите класс и укажите название группы."); return; }
        if (await service.ExistsAsync(SelectedGroup.ClassId, SelectedGroup.Name, SelectedGroup.Id == 0 ? null : SelectedGroup.Id))
        { dialogs.ShowError("В этом классе уже есть группа с таким названием."); return; }
        try { await service.SaveAsync(SelectedGroup); await LoadAsync(); }
        catch (Exception ex) { dialogs.ShowError($"Ошибка сохранения: {ex.Message}"); }
    }

    [RelayCommand] private async Task ArchiveAsync()
    { if (SelectedGroup is null) return; await service.ArchiveAsync(SelectedGroup.Id); await LoadAsync(); SelectedGroup = null; }
}
