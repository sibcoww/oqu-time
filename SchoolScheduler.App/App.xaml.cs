using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SchoolScheduler.App.Services;
using SchoolScheduler.App.ViewModels;
using System.Windows.Threading;

namespace SchoolScheduler.App;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var dialogService = _serviceProvider?.GetService<IDialogService>();
        if (dialogService != null)
        {
            dialogService.ShowError($"Произошла непредвиденная ошибка:\n{e.Exception.Message}");
        }
        else
        {
            MessageBox.Show($"Критическая ошибка:\n{e.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        e.Handled = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainWindow.DataContext = mainViewModel;

        // Setup initial navigation
        var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<HomeViewModel>();

        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Views
        services.AddTransient<MainWindow>();

        // Services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService>(sp => 
        {
            var mainViewModel = sp.GetRequiredService<MainViewModel>();
            return new NavigationService(sp, vm => mainViewModel.SetCurrentViewModel(vm));
        });

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SchoolViewModel>();
        services.AddTransient<ClassesViewModel>();
        services.AddTransient<TeachersViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<RoomsViewModel>();
        services.AddTransient<TeachingLoadViewModel>();
        services.AddTransient<ConstraintsViewModel>();
        services.AddTransient<ScheduleViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}

