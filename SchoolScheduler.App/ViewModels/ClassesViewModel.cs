using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class ClassesViewModel : ViewModelBase
{
    private readonly ISchoolClassService _classService;
    private readonly IDialogService _dialogService;

    // Using a factory or service locator would be better for child windows, but for simplicity here we assume DI can handle window creation.
    // However, since we are doing MVVM without extensive framework, we will pass a factory action or use explicit view showing. To keep it testable/MVVM, we should use a window service, but we will wire it up in the View's code-behind or through an interface if needed. For now, we will add a method that View can hook into or use IDialogService to show the bulk window.

    private readonly System.Action _showBulkCreateWindow;

    [ObservableProperty]
    private ObservableCollection<SchoolClass> _classes = new();

    [ObservableProperty]
    private ObservableCollection<Shift> _shifts = new();

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    public ClassesViewModel(ISchoolClassService classService, IDialogService dialogService, System.Action showBulkCreateWindow = null!)
    {
        _classService = classService;
        _dialogService = dialogService;
        _showBulkCreateWindow = showBulkCreateWindow;

        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    public IAsyncRelayCommand LoadDataCommand { get; }

    public async Task LoadDataAsync()
    {
        var shifts = await _classService.GetShiftsAsync();
        Shifts.Clear();
        foreach(var s in shifts) Shifts.Add(s);

        var classes = await _classService.GetAllClassesAsync();
        Classes.Clear();
        foreach (var c in classes.OrderBy(x => x.Parallel).ThenBy(x => x.Letter))
        {
            Classes.Add(c);
        }
    }

    [RelayCommand]
    private async Task SaveClassAsync()
    {
        if (SelectedClass == null) return;

        if (string.IsNullOrWhiteSpace(SelectedClass.Letter) || SelectedClass.Parallel <= 0)
        {
            _dialogService.ShowError("Параллель должна быть больше 0, и литера должна быть указана.");
            return;
        }

        // Auto-generate name if empty
        SelectedClass.Name = $"{SelectedClass.Parallel}{SelectedClass.Letter}";

        try
        {
            if (SelectedClass.Id == 0)
            {
                if (await _classService.ClassExistsAsync(SelectedClass.Parallel, SelectedClass.Letter))
                {
                    _dialogService.ShowError("Класс с такой параллелью и литерой уже существует.");
                    return;
                }
                await _classService.AddClassAsync(SelectedClass);
                Classes.Add(SelectedClass);
            }
            else
            {
                if (await _classService.ClassExistsAsync(SelectedClass.Parallel, SelectedClass.Letter, SelectedClass.Id))
                {
                    _dialogService.ShowError("Класс с такой параллелью и литерой уже существует.");
                    return;
                }
                await _classService.UpdateClassAsync(SelectedClass);
            }

            // Refresh list to keep sorting
            await LoadDataAsync();
        }
        catch (System.Exception ex)
        {
            _dialogService.ShowError($"Ошибка сохранения:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void AddNewClass()
    {
        SelectedClass = new SchoolClass 
        { 
            Parallel = 1, 
            Letter = "А", 
            MaxLessonsPerDay = 6, 
            ShiftId = Shifts.FirstOrDefault()?.Id ?? 0,
            IsActive = true
        };
    }

    [RelayCommand]
    private async Task ArchiveClassAsync()
    {
        if (SelectedClass != null && SelectedClass.Id > 0)
        {
            await _classService.ArchiveClassAsync(SelectedClass.Id);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void ShowBulkCreate()
    {
        _showBulkCreateWindow?.Invoke();
    }
}
