namespace SchoolScheduler.App.Services;

public interface INavigationService
{
    event Action<ViewModels.ViewModelBase>? Navigated;
    void NavigateTo<TViewModel>() where TViewModel : ViewModels.ViewModelBase;
}
