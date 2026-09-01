using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class SubjectsViewModel(ICatalogService service, IDialogService dialogs) : ViewModelBase
{
    public IReadOnlyList<SubjectType> SubjectTypes { get; } = Enum.GetValues<SubjectType>();
    [ObservableProperty] private ObservableCollection<Subject> _subjects = new();
    [ObservableProperty] private Subject? _selectedSubject;

    [RelayCommand]
    private async Task LoadAsync() => Subjects = new(await service.GetSubjectsAsync());

    [RelayCommand]
    private void Add() => SelectedSubject = new Subject { Difficulty = 1, Type = SubjectType.Required };

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSubject is null || string.IsNullOrWhiteSpace(SelectedSubject.Name) ||
            string.IsNullOrWhiteSpace(SelectedSubject.ShortName))
        { dialogs.ShowError("Укажите полное и короткое название предмета."); return; }
        if (SelectedSubject.Difficulty is < 1 or > 10)
        { dialogs.ShowError("Сложность должна быть от 1 до 10."); return; }
        if (await service.SubjectExistsAsync(SelectedSubject.Name, SelectedSubject.Id == 0 ? null : SelectedSubject.Id))
        { dialogs.ShowError("Предмет с таким названием уже существует."); return; }
        await service.SaveSubjectAsync(SelectedSubject);
        await LoadAsync();
    }
}
