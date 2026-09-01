using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolScheduler.App.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Action<ViewModels.ViewModelBase> _onNavigate;

    public NavigationService(IServiceProvider serviceProvider, Action<ViewModels.ViewModelBase> onNavigate)
    {
        _serviceProvider = serviceProvider;
        _onNavigate = onNavigate;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModels.ViewModelBase
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        _onNavigate(viewModel);
    }
}