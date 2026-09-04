using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    BellSchedule = 4,
    Finalize = 5
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
    public ObservableCollection<BellShiftEditor> BellShifts { get; } = new();

    public SetupWizardViewModel(ISchoolSetupService setupService, IDialogService dialogService)
    {
        _setupService = setupService;
        _dialogService = dialogService;
        RebuildBellSchedule();
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

            var shifts = BellShifts.Select(x => new Shift
            {
                Name = x.Name,
                LessonPeriods = x.Periods.Select(p => new LessonPeriod
                    { Number = p.Number, StartTime = p.ParsedStartTime, EndTime = p.ParsedEndTime }).ToList()
            }).ToList();
            await _setupService.SaveSetupAsync(school, year, shifts);

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

        if (CurrentStep == WizardStep.ShiftsCount)
        {
            if (ShiftsCount < 1 || LessonsPerShift < 1) { _dialogService.ShowError("Укажите смены и количество уроков."); return false; }
            RebuildBellSchedule();
        }
        if (CurrentStep == WizardStep.BellSchedule && !BellShifts.All(x => x.IsValid))
        { _dialogService.ShowError("Для каждого урока укажите время в формате ЧЧ:ММ; окончание должно быть позже начала."); return false; }

        return true;
    }

    partial void OnShiftsCountChanged(int value) => RebuildBellSchedule();
    partial void OnLessonsPerShiftChanged(int value) => RebuildBellSchedule();

    private void RebuildBellSchedule()
    {
        if (ShiftsCount < 1 || LessonsPerShift < 1) return;
        var oldZero = BellShifts.ToDictionary(x => x.Index, x => x.HasZeroLesson);
        var old = BellShifts.SelectMany(s => s.Periods.Select(p => ((s.Index, p.Number), p))).ToDictionary(x => x.Item1, x => x.p);
        BellShifts.Clear();
        for (var shiftIndex = 1; shiftIndex <= ShiftsCount; shiftIndex++)
        {
            var baseTime = new TimeSpan(8 + (shiftIndex - 1) * 6, 0, 0);
            var shift = new BellShiftEditor(shiftIndex, $"Смена {shiftIndex}", baseTime);
            for (var lesson = 1; lesson <= LessonsPerShift; lesson++)
            {
                if (old.TryGetValue((shiftIndex, lesson), out var existing)) shift.Periods.Add(existing);
                else
                {
                    var start = baseTime + TimeSpan.FromMinutes((lesson - 1) * 50);
                    shift.Periods.Add(new BellPeriodEditor(lesson, start, start + TimeSpan.FromMinutes(45)));
                }
            }
            if (oldZero.GetValueOrDefault(shiftIndex)) shift.HasZeroLesson = true;
            BellShifts.Add(shift);
        }
    }
}

public partial class BellShiftEditor : ObservableObject
{
    private readonly TimeSpan _firstLessonStart;
    public int Index { get; }
    public string Name { get; set; }
    public ObservableCollection<BellPeriodEditor> Periods { get; } = new();
    [ObservableProperty] private bool _hasZeroLesson;
    public BellShiftEditor(int index, string name, TimeSpan firstLessonStart)
    { Index = index; Name = name; _firstLessonStart = firstLessonStart; }
    partial void OnHasZeroLessonChanged(bool value)
    {
        var existing = Periods.FirstOrDefault(x => x.Number == 0);
        if (value && existing is null)
            Periods.Insert(0, new BellPeriodEditor(0, _firstLessonStart - TimeSpan.FromMinutes(50), _firstLessonStart - TimeSpan.FromMinutes(5)));
        else if (!value && existing is not null) Periods.Remove(existing);
    }
    public bool IsValid
    {
        get
        {
            if (Periods.Any(x => !x.IsValid) || Periods.Select(x => x.Number).Distinct().Count() != Periods.Count) return false;
            var ordered = Periods.OrderBy(x => x.ParsedStartTime).ToList();
            return !ordered.Zip(ordered.Skip(1), (a, b) => a.ParsedEndTime > b.ParsedStartTime).Any(x => x);
        }
    }
}

public sealed class BellPeriodEditor(int number, TimeSpan startTime, TimeSpan endTime)
{
    public int Number { get; } = number;
    public string StartTime { get; set; } = startTime.ToString("hh\\:mm");
    public string EndTime { get; set; } = endTime.ToString("hh\\:mm");
    public TimeSpan ParsedStartTime => TimeSpan.ParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture);
    public TimeSpan ParsedEndTime => TimeSpan.ParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture);
    public bool IsValid => TimeSpan.TryParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture, out var start) &&
        TimeSpan.TryParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture, out var end) && end > start;
}
