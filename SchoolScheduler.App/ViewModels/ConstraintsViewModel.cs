using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Scheduling.Validation;

namespace SchoolScheduler.App.ViewModels;

public partial class ConstraintsViewModel(IPreScheduleValidationService service, IDialogService dialogs) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<ValidationIssue> _issues = new();
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    public bool CanGenerate => CriticalCount == 0;

    [RelayCommand]
    private async Task ValidateAsync()
    {
        try
        {
            Issues = new(await service.ValidateAsync());
            CriticalCount = Issues.Count(x => x.Severity == ValidationSeverity.Critical);
            WarningCount = Issues.Count(x => x.Severity == ValidationSeverity.Warning);
            OnPropertyChanged(nameof(CanGenerate));
        }
        catch (Exception ex) { dialogs.ShowError($"Не удалось проверить данные: {ex.Message}"); }
    }
}
