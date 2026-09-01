using System;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolScheduler.App.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    public event Action<ViewModels.ViewModelBase>? Navigated;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModels.ViewModelBase
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        Navigated?.Invoke(viewModel);
    }
}
