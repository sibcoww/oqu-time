using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Scheduling.Validation;
using SchoolScheduler.Scheduling.Normative;

namespace SchoolScheduler.App.ViewModels;

public partial class ConstraintsViewModel(IPreScheduleValidationService service, IDialogService dialogs,
    INormativeValidationService normative) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<ValidationIssue> _issues = new();
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private ObservableCollection<NormativeViolation> _normativeViolations = new();
    [ObservableProperty] private string _normativeStatus = "Проверка по нормам РК ещё не выполнена.";
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

    [RelayCommand]
    private async Task ValidateNormsAsync()
    {
        try
        {
            var result = await normative.ValidateAsync();
            NormativeViolations = new(result.Violations);
            var critical = result.Violations.Count(x => x.Severity == NormativeSeverity.Critical);
            var recommendations = result.Violations.Count(x => x.Severity == NormativeSeverity.Recommendation);
            NormativeStatus = $"Нормы РК {result.RuleSet.AcademicYear}, версия {result.RuleSet.Version}: " +
                              $"критических нарушений — {critical}, рекомендаций — {recommendations}.";
        }
        catch (Exception ex) { dialogs.ShowError($"Не удалось выполнить нормативную проверку: {ex.Message}"); }
    }
}
