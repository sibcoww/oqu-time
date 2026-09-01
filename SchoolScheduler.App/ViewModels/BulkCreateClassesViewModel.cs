using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class BulkCreateClassesViewModel : ViewModelBase
{
    private readonly ISchoolClassService _classService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int _startParallel = 1;

    [ObservableProperty]
    private int _endParallel = 11;

    [ObservableProperty]
    private string _letters = "А, Б, В";

    [ObservableProperty]
    private int _maxLessonsPerDay = 6;

    [ObservableProperty]
    private ObservableCollection<Shift> _shifts = new();

    [ObservableProperty]
    private Shift? _selectedShift;

    public BulkCreateClassesViewModel(ISchoolClassService classService, IDialogService dialogService)
    {
        _classService = classService;
        _dialogService = dialogService;
        LoadShiftsCommand = new AsyncRelayCommand(LoadShiftsAsync);
    }

    public IAsyncRelayCommand LoadShiftsCommand { get; }

    private async Task LoadShiftsAsync()
    {
        var shifts = await _classService.GetShiftsAsync();
        Shifts.Clear();
        foreach (var s in shifts) Shifts.Add(s);

        SelectedShift = Shifts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task GenerateAsync(Window window)
    {
        if (SelectedShift == null)
        {
            _dialogService.ShowError("Выберите смену по умолчанию.");
            return;
        }

        if (StartParallel <= 0 || EndParallel < StartParallel)
        {
            _dialogService.ShowError("Некорректный диапазон параллелей.");
            return;
        }

        var letterList = Letters.Split(',')
                                .Select(l => l.Trim().ToUpper())
                                .Where(l => !string.IsNullOrEmpty(l))
                                .ToList();

        if (!letterList.Any())
        {
            _dialogService.ShowError("Укажите хотя бы одну литеру.");
            return;
        }

        try
        {
            await _classService.BulkCreateClassesAsync(StartParallel, EndParallel, letterList, SelectedShift.Id, MaxLessonsPerDay);
            _dialogService.ShowMessage("Успех", "Массовое создание классов завершено.");
            window.DialogResult = true;
            window.Close();
        }
        catch (System.Exception ex)
        {
            _dialogService.ShowError($"Ошибка: {ex.Message}");
        }
    }
}