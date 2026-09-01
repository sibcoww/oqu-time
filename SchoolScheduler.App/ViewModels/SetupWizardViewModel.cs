using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public enum WizardStep
{
    SchoolInfo = 0,
    AcademicYear = 1,
    WorkWeek = 2,
    ShiftsCount = 3,
    // Bell schedule can be added later or as step 5, keeping simple for this iteration
    Finalize = 4
}

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly ISchoolSetupService _setupService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private WizardStep _currentStep = WizardStep.SchoolInfo;

    [ObservableProperty]
    private string _schoolName = string.Empty;

    [ObservableProperty]
    private string _region = "KZ";

    [ObservableProperty]
    private bool _useRegionalNorms = false;

    [ObservableProperty]
    private string _academicYearName = "2026-2027";

    [ObservableProperty]
    private int _daysPerWeek = 5;

    [ObservableProperty]
    private int _shiftsCount = 2;

    [ObservableProperty]
    private int _lessonsPerShift = 6;

    public List<int> DaysPerWeekOptions { get; } = new() { 5, 6 };
    public List<int> ShiftsCountOptions { get; } = new() { 1, 2, 3 };

    public SetupWizardViewModel(ISchoolSetupService setupService, IDialogService dialogService)
    {
        _setupService = setupService;
        _dialogService = dialogService;
    }

    public bool IsFirstStep => CurrentStep == WizardStep.SchoolInfo;
    public bool IsLastStep => CurrentStep == WizardStep.Finalize;
    public bool IsNotLastStep => !IsLastStep;

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < WizardStep.Finalize)
        {
            if (!ValidateCurrentStep()) return;

            CurrentStep++;
            OnPropertyChanged(nameof(IsFirstStep));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(IsNotLastStep));
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > WizardStep.SchoolInfo)
        {
            CurrentStep--;
            OnPropertyChanged(nameof(IsFirstStep));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(IsNotLastStep));
        }
    }

    [RelayCommand]
    private async Task FinishAsync(Window window)
    {
        try
        {
            var school = new School
            {
                Name = SchoolName,
                Region = Region,
                UseRegionalNorms = UseRegionalNorms,
                DaysPerWeek = DaysPerWeek
            };

            var year = new AcademicYear
            {
                Name = AcademicYearName
            };

            await _setupService.SaveSetupAsync(school, year, ShiftsCount, LessonsPerShift);

            window.DialogResult = true;
            window.Close();
        }
        catch (System.Exception ex)
        {
            _dialogService.ShowError($"Ошибка сохранения:\n{ex.Message}");
        }
    }

    private bool ValidateCurrentStep()
    {
        if (CurrentStep == WizardStep.SchoolInfo && string.IsNullOrWhiteSpace(SchoolName))
        {
            _dialogService.ShowError("Введите название школы.");
            return false;
        }

        if (CurrentStep == WizardStep.AcademicYear && string.IsNullOrWhiteSpace(AcademicYearName))
        {
            _dialogService.ShowError("Введите название учебного года.");
            return false;
        }

        return true;
    }
}