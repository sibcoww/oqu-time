using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolScheduler.App.Services;
using SchoolScheduler.Core.Models;

namespace SchoolScheduler.App.ViewModels;

public partial class SchoolViewModel(ISchoolSetupService service, IDialogService dialogs) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<BellScheduleRow> _periods = new();
    [RelayCommand] private async Task LoadAsync()
    {
        var model = await service.GetTimeModelAsync();
        Periods = new(model.Shifts.SelectMany(s => s.LessonPeriods.OrderBy(p => p.Number)
            .Select(p => new BellScheduleRow(s.Id, s.Name, p.Id, p.Number, p.StartTime, p.EndTime))));
    }
    [RelayCommand] private async Task SaveAsync()
    {
        if (Periods.Any(x => !x.IsValid)) { dialogs.ShowError("Проверьте время всех уроков: окончание должно быть позже начала."); return; }
        var shifts = Periods.GroupBy(x => new { x.ShiftId, x.ShiftName }).Select(g => new Shift
        {
            Id = g.Key.ShiftId, Name = g.Key.ShiftName,
            LessonPeriods = g.Select(x => new LessonPeriod { Id = x.LessonPeriodId, ShiftId = x.ShiftId,
                Number = x.Number, StartTime = x.ParsedStartTime, EndTime = x.ParsedEndTime }).ToList()
        }).ToList();
        await service.SaveBellScheduleAsync(shifts);
        dialogs.ShowMessage("Расписание звонков", "Изменения сохранены.");
    }
}

public sealed class BellScheduleRow(int shiftId, string shiftName, int lessonPeriodId, int number,
    TimeSpan startTime, TimeSpan endTime)
{
    public int ShiftId { get; } = shiftId;
    public string ShiftName { get; set; } = shiftName;
    public int LessonPeriodId { get; } = lessonPeriodId;
    public int Number { get; } = number;
    public string StartTime { get; set; } = startTime.ToString("hh\\:mm");
    public string EndTime { get; set; } = endTime.ToString("hh\\:mm");
    public TimeSpan ParsedStartTime => TimeSpan.ParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture);
    public TimeSpan ParsedEndTime => TimeSpan.ParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture);
    public bool IsValid => TimeSpan.TryParseExact(StartTime, @"h\:mm", CultureInfo.InvariantCulture, out var start) &&
        TimeSpan.TryParseExact(EndTime, @"h\:mm", CultureInfo.InvariantCulture, out var end) && end > start;
}
