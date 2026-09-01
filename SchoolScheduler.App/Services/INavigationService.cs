namespace SchoolScheduler.App.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ViewModels.ViewModelBase;
}