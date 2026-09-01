using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Data;

namespace SchoolScheduler.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IProjectBackupService _backups;
    private readonly IFileDialogService _files;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public MainViewModel(INavigationService navigationService, IProjectBackupService backups,
        IFileDialogService files, IDialogService dialogs)
    {
        _navigationService = navigationService;
        _navigationService.Navigated += SetCurrentViewModel;
        _backups = backups;
        _files = files;
        _dialogs = dialogs;
        NavigationItems.Add(new NavigationItem("Группы", NavigateToCommand<GroupsViewModel>()));

        NavigationItems.Add(new NavigationItem("Главная", NavigateToCommand<HomeViewModel>()));
        NavigationItems.Add(new NavigationItem("Школа", NavigateToCommand<SchoolViewModel>()));
        NavigationItems.Add(new NavigationItem("Классы", NavigateToCommand<ClassesViewModel>()));
        NavigationItems.Add(new NavigationItem("Учителя", NavigateToCommand<TeachersViewModel>()));
        NavigationItems.Add(new NavigationItem("Предметы", NavigateToCommand<SubjectsViewModel>()));
        NavigationItems.Add(new NavigationItem("Кабинеты", NavigateToCommand<RoomsViewModel>()));
        NavigationItems.Add(new NavigationItem("Нагрузка", NavigateToCommand<TeachingLoadViewModel>()));
        NavigationItems.Add(new NavigationItem("Ограничения", NavigateToCommand<ConstraintsViewModel>()));
        NavigationItems.Add(new NavigationItem("Расписание", NavigateToCommand<ScheduleViewModel>()));
        NavigationItems.Add(new NavigationItem("Экспорт", NavigateToCommand<ExportViewModel>()));
        NavigationItems.Add(new NavigationItem("Настройки", NavigateToCommand<SettingsViewModel>()));
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var path = _files.ChooseBackupSavePath($"SchoolScheduler_{DateTime.Today:yyyy-MM-dd}.schoolscheduler");
        if (path is null) return;
        try
        {
            await _backups.CreateAsync(path);
            _dialogs.ShowMessage("Резервная копия", "Резервная копия проекта успешно создана.");
        }
        catch (Exception ex) { _dialogs.ShowError($"Не удалось создать резервную копию: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var path = _files.ChooseBackupOpenPath();
        if (path is null) return;
        if (!_dialogs.Confirm("Восстановление проекта",
                "Текущие данные будут заменены данными из резервной копии. Продолжить?")) return;
        try
        {
            await _backups.RestoreAsync(path);
            _dialogs.ShowMessage("Восстановление проекта",
                "Проект восстановлен. Перезапустите приложение, чтобы все экраны загрузили восстановленные данные.");
        }
        catch (Exception ex) { _dialogs.ShowError($"Не удалось восстановить проект: {ex.Message}"); }
    }

    private void NavigateTo<T>() where T : ViewModelBase
    {
        _navigationService.NavigateTo<T>();
    }

    private IRelayCommand NavigateToCommand<T>() where T : ViewModelBase
    {
        return new RelayCommand(NavigateTo<T>);
    }

    public void SetCurrentViewModel(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
    }
}

public class NavigationItem
{
    public string Title { get; }
    public IRelayCommand Command { get; }

    public NavigationItem(string title, IRelayCommand command)
    {
        Title = title;
        Command = command;
    }
}
