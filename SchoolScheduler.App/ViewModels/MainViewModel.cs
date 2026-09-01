using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;

namespace SchoolScheduler.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
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
