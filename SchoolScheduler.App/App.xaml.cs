using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolScheduler.App.Services;
using SchoolScheduler.App.ViewModels;
using SchoolScheduler.App.Views;
using SchoolScheduler.Data;
using System.Windows.Threading;
using SchoolScheduler.ImportExport;
using SchoolScheduler.Scheduling.Validation;
using SchoolScheduler.Scheduling.Domain;
using SchoolScheduler.Scheduling.Solver;

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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Check if school setup is required
        var setupService = _serviceProvider.GetRequiredService<ISchoolSetupService>();
        bool isConfigured = await setupService.IsSchoolConfiguredAsync();

        if (!isConfigured)
        {
            var setupWindow = _serviceProvider.GetRequiredService<SetupWizardWindow>();
            setupWindow.DataContext = _serviceProvider.GetRequiredService<SetupWizardViewModel>();

            bool? result = setupWindow.ShowDialog();
            if (result != true)
            {
                Shutdown();
                return;
            }
        }

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
        // DbContext
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite("Data Source=school.db"));

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SetupWizardWindow>();
        services.AddTransient<BulkCreateClassesWindow>();

        // Services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService>(sp => 
        {
            var mainViewModel = sp.GetRequiredService<MainViewModel>();
            return new NavigationService(sp, vm => mainViewModel.SetCurrentViewModel(vm));
        });
        services.AddTransient<ISchoolSetupService, SchoolSetupService>();
        services.AddTransient<ISchoolClassService, SchoolClassService>();
        services.AddTransient<ITeacherService, TeacherService>();
        services.AddTransient<ICatalogService, CatalogService>();
        services.AddTransient<IGroupService, GroupService>();
        services.AddTransient<ITeachingLoadService, TeachingLoadService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<TeachingLoadExcelService>();
        services.AddSingleton<ScheduleExcelService>();
        services.AddSingleton<PreScheduleValidator>();
        services.AddTransient<IPreScheduleValidationService, PreScheduleValidationService>();
        services.AddSingleton<SchedulingProblemFactory>();
        services.AddSingleton<IScheduleGenerator, CpSatScheduleGenerator>();
        services.AddTransient<IScheduleGenerationService, ScheduleGenerationService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SchoolViewModel>();
        services.AddTransient<BulkCreateClassesViewModel>();
        services.AddTransient<GroupsViewModel>();
        services.AddTransient<ClassesViewModel>(sp => new ClassesViewModel(
            sp.GetRequiredService<ISchoolClassService>(),
            sp.GetRequiredService<IDialogService>(),
            () =>
            {
                var window = sp.GetRequiredService<BulkCreateClassesWindow>();
                window.DataContext = sp.GetRequiredService<BulkCreateClassesViewModel>();
                window.Owner = Current.MainWindow;
                window.ShowDialog();
            }));
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

