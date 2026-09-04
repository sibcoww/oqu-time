using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class SchoolViewModel(ISchoolSetupService service, IDialogService dialogs) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<BellScheduleShiftEditor> _shifts = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var model = await service.GetTimeModelAsync();
        Shifts = new(model.Shifts.Select(x => new BellScheduleShiftEditor(x)));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Shifts.All(x => x.IsValid))
        { dialogs.ShowError("Проверьте время: окончание должно быть позже начала, а уроки одной смены не должны пересекаться или повторяться."); return; }
        await service.SaveBellScheduleAsync(Shifts.Select(x => x.ToModel()).ToList());
        dialogs.ShowMessage("Расписание звонков", "Изменения сохранены.");
        await LoadAsync();
    }
}

public partial class BellScheduleShiftEditor : ObservableObject
{
    public int ShiftId { get; }
    public string Name { get; set; }
    public ObservableCollection<BellScheduleRow> Periods { get; } = new();
    [ObservableProperty] private bool _hasZeroLesson;

    public BellScheduleShiftEditor(Shift shift)
    {
        ShiftId = shift.Id; Name = shift.Name;
        foreach (var period in shift.LessonPeriods.OrderBy(x => x.Number)) Periods.Add(new(period));
        _hasZeroLesson = Periods.Any(x => x.Number == 0);
    }

    partial void OnHasZeroLessonChanged(bool value)
    {
        var zero = Periods.FirstOrDefault(x => x.Number == 0);
        if (value && zero is null)
        {
            var first = Periods.OrderBy(x => x.ParsedStartTime).FirstOrDefault();
            var end = first?.ParsedStartTime - TimeSpan.FromMinutes(5) ?? new TimeSpan(7, 55, 0);
            Periods.Insert(0, new BellScheduleRow(0, 0, end - TimeSpan.FromMinutes(45), end));
        }
        else if (!value && zero is not null) Periods.Remove(zero);
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

    public Shift ToModel() => new()
    {
        Id = ShiftId, Name = Name,
        LessonPeriods = Periods.Select(x => new LessonPeriod { Id = x.LessonPeriodId, ShiftId = ShiftId,
            Number = x.Number, StartTime = x.ParsedStartTime, EndTime = x.ParsedEndTime }).ToList()
    };
}

public sealed class BellScheduleRow
{
    public int LessonPeriodId { get; }
    public int Number { get; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public BellScheduleRow(LessonPeriod period) : this(period.Id, period.Number, period.StartTime, period.EndTime) { }
    public BellScheduleRow(int id, int number, TimeSpan start, TimeSpan end)
    { LessonPeriodId = id; Number = number; StartTime = start.ToString("hh\\:mm"); EndTime = end.ToString("hh\\:mm"); }
    public TimeSpan ParsedStartTime => TimeSpan.ParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture);
    public TimeSpan ParsedEndTime => TimeSpan.ParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture);
    public bool IsValid => TimeSpan.TryParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture, out var start) &&
        TimeSpan.TryParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture, out var end) && end > start;
}
